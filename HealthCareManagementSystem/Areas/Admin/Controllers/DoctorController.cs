using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.ViewModels;
using HealthCareManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DoctorProfile = HealthCareManagementSystem.Models.Doctor;

namespace HealthCareManagementSystem.Areas.Admin.Controllers
{
    [Area(CD.ADMIN_AREA)]
    [Authorize(Roles = CD.STAFF_ROLES)]
    public class DoctorController : Controller
    {
        private const string ImageFolder = "Doctors";

        private readonly IRepository<Clinic> _clinicRepository;
        private readonly IRepository<DoctorProfile> _doctorRepository;
        private readonly IRepository<DoctorSchedule> _doctorScheduleRepository;
        private readonly IRepository<Appointment> _appointmentRepository;
        private readonly IImageService _imageService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;

        public DoctorController(
            IRepository<Clinic> clinicRepository,
            IRepository<DoctorProfile> doctorRepository,
            IRepository<DoctorSchedule> doctorScheduleRepository,
            IRepository<Appointment> appointmentRepository,
            IImageService imageService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db)
        {
            _clinicRepository = clinicRepository;
            _doctorRepository = doctorRepository;
            _doctorScheduleRepository = doctorScheduleRepository;
            _appointmentRepository = appointmentRepository;
            _imageService = imageService;
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        #region Index

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var doctors = await _doctorRepository.GetAsync(
                includes: [d => d.Clinic],
                tracked: false,
                orderBy: query => query.OrderBy(d => d.Name));

            return View(doctors);
        }

        #endregion

        #region Create

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!User.IsInRole(CD.SUPER_ADMIN_ROLE))
            {
                return Forbid();
            }

            await PopulateClinicsAsync();

            return View(new DoctorVM { CreateLogin = true });
        }

        [HttpPost]
        public async Task<IActionResult> Create(DoctorVM doctorVM)
        {
            if (!User.IsInRole(CD.SUPER_ADMIN_ROLE))
            {
                return Forbid();
            }

            if (doctorVM.CreateLogin && (string.IsNullOrWhiteSpace(doctorVM.Email)
                || string.IsNullOrWhiteSpace(doctorVM.UserName)
                || string.IsNullOrWhiteSpace(doctorVM.Password)
                || doctorVM.Password != doctorVM.ConfirmPassword))
            {
                ModelState.AddModelError(string.Empty, "Complete the doctor login fields and ensure the passwords match.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateClinicsAsync(doctorVM.ClinicId);
                return View(doctorVM);
            }

            var clinicId = doctorVM.ClinicId!.Value;

            if (!await _clinicRepository.AnyAsync(c => c.Id == clinicId))
            {
                ModelState.AddModelError(nameof(DoctorVM.ClinicId), "The selected clinic does not exist.");
                await PopulateClinicsAsync();
                return View(doctorVM);
            }

            string? imageName = null;

            if (doctorVM.Image is { Length: > 0 })
            {
                var saved = await _imageService.SaveAsync(doctorVM.Image, ImageFolder);

                if (!saved.Succeeded)
                {
                    ModelState.AddModelError(nameof(DoctorVM.Image), saved.Error ?? "The image could not be saved.");
                    await PopulateClinicsAsync(clinicId);
                    return View(doctorVM);
                }

                imageName = saved.FileName;
            }

            var doctor = new DoctorProfile
            {
                Name = doctorVM.Name.Trim(),
                Specialization = doctorVM.Specialization.Trim(),
                ClinicId = clinicId,
                ImageName = imageName
            };

            try
            {
                if (doctorVM.CreateLogin)
                {
                    var email = doctorVM.Email!.Trim();
                    var userName = doctorVM.UserName!.Trim();
                    if (await _userManager.FindByEmailAsync(email) != null
                        || await _userManager.FindByNameAsync(userName) != null)
                    {
                        _imageService.Delete(imageName, ImageFolder);
                        ModelState.AddModelError(string.Empty, "That doctor e-mail or user name is already registered.");
                        await PopulateClinicsAsync(clinicId);
                        return View(doctorVM);
                    }

                    await using var transaction = await _db.Database.BeginTransactionAsync();
                    var user = new ApplicationUser
                    {
                        Name = doctor.Name,
                        Address = "N/A",
                        Email = email,
                        UserName = userName,
                        EmailConfirmed = false
                    };
                    var createResult = await _userManager.CreateAsync(user, doctorVM.Password!);
                    if (!createResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        AddIdentityErrors(createResult);
                        _imageService.Delete(imageName, ImageFolder);
                        await PopulateClinicsAsync(clinicId);
                        return View(doctorVM);
                    }

                    var roleResult = await _userManager.AddToRoleAsync(user, CD.DOCTOR_ROLE);
                    if (!roleResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        AddIdentityErrors(roleResult);
                        _imageService.Delete(imageName, ImageFolder);
                        await PopulateClinicsAsync(clinicId);
                        return View(doctorVM);
                    }

                    doctor.ApplicationUserId = user.Id;
                    await _doctorRepository.AddAsync(doctor);
                    await _doctorRepository.CommitAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await _doctorRepository.AddAsync(doctor);
                    await _doctorRepository.CommitAsync();
                }
            }
            catch (DbUpdateException)
            {
                _imageService.Delete(imageName, ImageFolder);
                ModelState.AddModelError(string.Empty, "The doctor could not be saved. Please try again.");
                await PopulateClinicsAsync(clinicId);
                return View(doctorVM);
            }

            TempData["Success-Notification"] = "Doctor added. Add weekly hours to enable appointment booking.";

            return RedirectToAction(nameof(Schedules), new { id = doctor.Id });
        }

        [HttpGet]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> CreateLogin(int id)
        {
            var doctor = await _doctorRepository.GetOneAsync(item => item.Id == id, tracked: false);
            if (doctor == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(doctor.ApplicationUserId))
            {
                TempData["Info-Notification"] = "This doctor already has a login account.";
                return RedirectToAction(nameof(Update), new { id });
            }

            return View(new DoctorAccountVM { DoctorId = id, DoctorName = doctor.Name });
        }

        [HttpPost]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> CreateLogin(DoctorAccountVM model)
        {
            if (!User.IsInRole(CD.SUPER_ADMIN_ROLE))
            {
                return Forbid();
            }

            var doctor = await _doctorRepository.GetOneAsync(item => item.Id == model.DoctorId);
            if (doctor == null)
            {
                return NotFound();
            }

            model.DoctorName = doctor.Name;
            if (!string.IsNullOrWhiteSpace(doctor.ApplicationUserId))
            {
                ModelState.AddModelError(string.Empty, "This doctor is already linked to a login account.");
            }

            var email = model.Email.Trim();
            var userName = model.UserName.Trim();
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "This e-mail address is already registered.");
            }

            if (await _userManager.FindByNameAsync(userName) != null)
            {
                ModelState.AddModelError(nameof(model.UserName), "This user name is already taken.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync(CD.DOCTOR_ROLE))
            {
                ModelState.AddModelError(string.Empty, "The Doctor role is not configured yet. Restart the application and try again.");
                return View(model);
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            var user = new ApplicationUser
            {
                Name = doctor.Name,
                Address = "N/A",
                Email = email,
                UserName = userName,
                EmailConfirmed = false
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(createResult);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, CD.DOCTOR_ROLE);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(roleResult);
                return View(model);
            }

            doctor.ApplicationUserId = user.Id;
            try
            {
                _doctorRepository.Update(doctor);
                await _doctorRepository.CommitAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "The login account could not be linked to this doctor.");
                return View(model);
            }

            TempData["Success-Notification"] = "Doctor login created. Confirm the e-mail before giving the account credentials to the doctor.";
            return RedirectToAction(nameof(Update), new { id = doctor.Id });
        }

        #endregion

        #region Update

        [HttpGet]
        public async Task<IActionResult> Update(int id)
        {
            var doctor = await _doctorRepository.GetOneAsync(d => d.Id == id, tracked: false);

            if (doctor == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            await PopulateClinicsAsync(doctor.ClinicId);

            var doctorVM = new DoctorVM
            {
                Id = doctor.Id,
                Name = doctor.Name,
                Specialization = doctor.Specialization,
                ClinicId = doctor.ClinicId,
                ImageName = doctor.ImageName,
                ApplicationUserId = doctor.ApplicationUserId
            };

            return View(doctorVM);
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, DoctorVM doctorVM)
        {
            if (!User.IsInRole(CD.SUPER_ADMIN_ROLE))
            {
                return Forbid();
            }

            var doctor = await _doctorRepository.GetOneAsync(d => d.Id == id);

            if (doctor == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            doctorVM.Id = doctor.Id;

            // Keeps the current picture visible if the form has to be shown again.
            doctorVM.ImageName = doctor.ImageName;

            if (!ModelState.IsValid)
            {
                await PopulateClinicsAsync(doctorVM.ClinicId);
                return View(doctorVM);
            }

            var clinicId = doctorVM.ClinicId!.Value;
            if (!await _clinicRepository.AnyAsync(c => c.Id == clinicId))
            {
                ModelState.AddModelError(nameof(DoctorVM.ClinicId), "The selected clinic does not exist.");
                await PopulateClinicsAsync();
                return View(doctorVM);
            }

            string? newImageName = null;

            if (doctorVM.Image is { Length: > 0 })
            {
                var saved = await _imageService.SaveAsync(doctorVM.Image, ImageFolder);

                if (!saved.Succeeded)
                {
                    ModelState.AddModelError(nameof(DoctorVM.Image), saved.Error ?? "The image could not be saved.");
                    await PopulateClinicsAsync(clinicId);
                    return View(doctorVM);
                }

                newImageName = saved.FileName;
            }

            var oldImageName = doctor.ImageName;

            doctor.Name = doctorVM.Name.Trim();
            doctor.Specialization = doctorVM.Specialization.Trim();
            doctor.ClinicId = clinicId;

            if (newImageName != null)
            {
                doctor.ImageName = newImageName;
            }

            try
            {
                _doctorRepository.Update(doctor);
                await _doctorRepository.CommitAsync();
            }
            catch (DbUpdateException)
            {
                _imageService.Delete(newImageName, ImageFolder);
                ModelState.AddModelError(string.Empty, "The doctor could not be updated. Please try again.");
                await PopulateClinicsAsync(clinicId);
                return View(doctorVM);
            }

            if (newImageName != null)
            {
                _imageService.Delete(oldImageName, ImageFolder);
            }

            TempData["Success-Notification"] = "Doctor updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Schedules

        [HttpGet]
        public async Task<IActionResult> Schedules(int id)
        {
            var model = await LoadSchedulesPageAsync(id);

            return model == null ? RedirectToAction("NotFoundPage", "Home") : View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveSchedule(DoctorSchedulesPageVM page)
        {
            var form = page.NewSchedule;
            var doctor = form.DoctorId > 0
                ? await _doctorRepository.GetOneAsync(d => d.Id == form.DoctorId, tracked: false)
                : null;

            if (doctor == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            if (form.DayOfWeek is { } day && !Enum.IsDefined(day))
            {
                ModelState.AddModelError("NewSchedule.DayOfWeek", "Choose a valid day of the week.");
            }

            if (form.StartTime is { } start && form.EndTime is { } end && end <= start)
            {
                ModelState.AddModelError("NewSchedule.EndTime", "The end time must be later than the start time.");
            }

            var schedules = await _doctorScheduleRepository.GetAsync(
                filter: schedule => schedule.DoctorId == form.DoctorId,
                tracked: false);

            if (form.DayOfWeek is { } scheduleDay
                && form.StartTime is { } scheduleStart
                && form.EndTime is { } scheduleEnd
                && schedules.Any(schedule => schedule.Id != form.ScheduleId
                    && schedule.DayOfWeek == scheduleDay
                    && scheduleStart < schedule.EndTime
                    && scheduleEnd > schedule.StartTime))
            {
                ModelState.AddModelError("NewSchedule.StartTime", "This schedule overlaps another block on the same day.");
            }

            if (!ModelState.IsValid)
            {
                var invalidPage = await LoadSchedulesPageAsync(form.DoctorId);
                invalidPage!.NewSchedule = form;
                return View("Schedules", invalidPage);
            }

            var scheduleToSave = form.ScheduleId is { } scheduleId
                ? await _doctorScheduleRepository.GetOneAsync(
                    schedule => schedule.Id == scheduleId && schedule.DoctorId == form.DoctorId)
                : new DoctorSchedule { DoctorId = form.DoctorId };

            if (scheduleToSave == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            scheduleToSave.DayOfWeek = form.DayOfWeek!.Value;
            scheduleToSave.StartTime = form.StartTime!.Value;
            scheduleToSave.EndTime = form.EndTime!.Value;
            scheduleToSave.SlotMinutes = form.SlotMinutes;

            try
            {
                if (form.ScheduleId.HasValue)
                {
                    _doctorScheduleRepository.Update(scheduleToSave);
                }
                else
                {
                    await _doctorScheduleRepository.AddAsync(scheduleToSave);
                }

                await _doctorScheduleRepository.CommitAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "The schedule could not be saved. Please try again.");
                var failedPage = await LoadSchedulesPageAsync(form.DoctorId);
                failedPage!.NewSchedule = form;
                return View("Schedules", failedPage);
            }

            TempData["Success-Notification"] = "Weekly availability saved.";

            return RedirectToAction(nameof(Schedules), new { id = form.DoctorId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSchedule(int doctorId, int scheduleId)
        {
            var schedule = await _doctorScheduleRepository.GetOneAsync(
                item => item.Id == scheduleId && item.DoctorId == doctorId);

            if (schedule == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            try
            {
                _doctorScheduleRepository.Delete(schedule);
                await _doctorScheduleRepository.CommitAsync();
                TempData["Success-Notification"] = "Schedule block removed.";
            }
            catch (DbUpdateException)
            {
                TempData["Error-Notification"] = "The schedule block could not be removed.";
            }

            return RedirectToAction(nameof(Schedules), new { id = doctorId });
        }

        private async Task<DoctorSchedulesPageVM?> LoadSchedulesPageAsync(int doctorId)
        {
            var doctor = await _doctorRepository.GetOneAsync(d => d.Id == doctorId, tracked: false);

            if (doctor == null)
            {
                return null;
            }

            var schedules = await _doctorScheduleRepository.GetAsync(
                filter: schedule => schedule.DoctorId == doctorId,
                tracked: false,
                orderBy: query => query.OrderBy(schedule => schedule.DayOfWeek)
                    .ThenBy(schedule => schedule.StartTime));

            return new DoctorSchedulesPageVM
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Schedules = schedules.ToList(),
                NewSchedule = new DoctorScheduleFormVM { DoctorId = doctor.Id }
            };
        }

        #endregion

        #region Delete

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!User.IsInRole(CD.SUPER_ADMIN_ROLE))
            {
                return Forbid();
            }

            var doctor = await _doctorRepository.GetOneAsync(
                d => d.Id == id,
                includes: [d => d.Clinic],
                tracked: false);

            if (doctor == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            return View(doctor);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!User.IsInRole(CD.SUPER_ADMIN_ROLE))
            {
                return Forbid();
            }

            var doctor = await _doctorRepository.GetOneAsync(d => d.Id == id);

            if (doctor == null)
            {
                return RedirectToAction("NotFoundPage", "Home");
            }

            if (await _appointmentRepository.AnyAsync(a => a.DoctorId == id))
            {
                TempData["Error-Notification"] = "This doctor has appointments on record, so they cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _doctorRepository.Delete(doctor);
                await _doctorRepository.CommitAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error-Notification"] = "This doctor is still used by other records, so they cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            _imageService.Delete(doctor.ImageName, ImageFolder);

            TempData["Success-Notification"] = "Doctor deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        private async Task PopulateClinicsAsync(int? selectedClinicId = null)
        {
            var clinics = await _clinicRepository.GetAsync(
                tracked: false,
                orderBy: query => query.OrderBy(c => c.Name));

            ViewBag.Clinics = new SelectList(clinics, nameof(Clinic.Id), nameof(Clinic.Name), selectedClinicId);
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
