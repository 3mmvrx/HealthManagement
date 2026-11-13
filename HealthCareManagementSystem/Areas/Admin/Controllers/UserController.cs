using HealthCareManagementSystem.Data;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthCareManagementSystem.Areas.Admin.Controllers
{
    [Area(CD.ADMIN_AREA)]
    [Authorize(Roles = CD.ADMIN_ROLES)]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IImageService _imageService;
        private readonly IWebHostEnvironment _environment;

        public UserController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db,
            IImageService imageService,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _imageService = imageService;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.OrderBy(user => user.UserName).ToListAsync();
            var rows = new List<AdminUserRowVM>(users.Count);

            foreach (var user in users)
            {
                rows.Add(new AdminUserRowVM
                {
                    User = user,
                    Roles = await _userManager.GetRolesAsync(user),
                    IsLocked = user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow
                });
            }

            return View(new AdminUsersPageVM { Users = rows });
        }

        [HttpGet]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public IActionResult Create()
        {
            PopulateRoles();
            return View(new AdminUserFormVM());
        }

        [HttpPost]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> Create(AdminUserFormVM model)
        {
            if (model.Role == CD.DOCTOR_ROLE || !await IsAllowedRoleAsync(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Choose a valid account role.");
            }

            ValidatePatientFields(model);
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), "A password is required.");
            }

            if (!ModelState.IsValid)
            {
                PopulateRoles(model.Role);
                return View(model);
            }

            var email = model.Email?.Trim() ?? string.Empty;
            var userName = model.UserName?.Trim() ?? string.Empty;
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
                PopulateRoles(model.Role);
                return View(model);
            }

            var user = new ApplicationUser
            {
                Name = model.Name.Trim(),
                Address = model.Address.Trim(),
                Email = email,
                UserName = userName,
                EmailConfirmed = false
            };

            await using var transaction = await _db.Database.BeginTransactionAsync();
            var createResult = await _userManager.CreateAsync(user, model.Password!);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(createResult);
                PopulateRoles(model.Role);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(roleResult);
                PopulateRoles(model.Role);
                return View(model);
            }

            try
            {
                if (model.Role == CD.PATIENT_ROLE)
                {
                    _db.Patients.Add(new Patient
                    {
                        Name = user.Name,
                        Gender = model.Gender!,
                        DateOfBirth = DateOnly.FromDateTime(model.DateOfBirth!.Value),
                        ApplicationUserId = user.Id
                    });
                    await _db.SaveChangesAsync();
                }
                else if (model.Role == CD.EMPLOYEE_ROLE)
                {
                    _db.Receptionists.Add(new Receptionist { ApplicationUserId = user.Id });
                    await _db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "The user profile could not be saved.");
                PopulateRoles(model.Role);
                return View(model);
            }

            await transaction.CommitAsync();
            TempData["Success-Notification"] = "User account created. Confirm the e-mail address when it has been verified.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var model = new AdminUserFormVM
            {
                Id = user.Id,
                Name = user.Name,
                Address = user.Address,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Role = roles.FirstOrDefault() ?? CD.PATIENT_ROLE
            };

            var patient = await _db.Patients.AsNoTracking().SingleOrDefaultAsync(item => item.ApplicationUserId == id);
            if (patient != null)
            {
                model.Gender = patient.Gender;
                model.DateOfBirth = patient.DateOfBirth.ToDateTime(TimeOnly.MinValue);
            }

            var linkedDoctor = await _db.Doctors.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ApplicationUserId == user.Id);
            PopulateRoles(model.Role, linkedDoctor != null);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> Edit(AdminUserFormVM model)
        {
            var user = model.Id == null ? null : await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            var linkedDoctor = await _db.Doctors.SingleOrDefaultAsync(item => item.ApplicationUserId == user.Id);
            if (linkedDoctor != null)
            {
                TempData["Error-Notification"] = "Manage doctor accounts from the doctor profile to preserve the account link.";
                return RedirectToAction(nameof(Index));
            }

            if (!await IsAllowedRoleAsync(model.Role, linkedDoctor != null))
            {
                ModelState.AddModelError(nameof(model.Role), "Choose a valid account role.");
            }

            if (linkedDoctor != null && model.Role != CD.DOCTOR_ROLE)
            {
                ModelState.AddModelError(nameof(model.Role), "Remove this doctor's login from the doctor profile before changing its role.");
            }

            ValidatePatientFields(model);

            if (model.Password != null && model.Password.Length > 0 && model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "The passwords do not match.");
            }

            var email = model.Email?.Trim() ?? string.Empty;
            var username = model.UserName?.Trim() ?? string.Empty;
            if (await _userManager.Users.AnyAsync(other => other.Id != user.Id && other.NormalizedEmail == email.ToUpper()))
            {
                ModelState.AddModelError(nameof(model.Email), "This e-mail address is already registered.");
            }

            if (await _userManager.Users.AnyAsync(other => other.Id != user.Id && other.NormalizedUserName == username.ToUpper()))
            {
                ModelState.AddModelError(nameof(model.UserName), "This user name is already taken.");
            }

            if (!ModelState.IsValid)
            {
                PopulateRoles(model.Role, linkedDoctor != null);
                return View(model);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (linkedDoctor != null && !currentRoles.Contains(CD.DOCTOR_ROLE))
            {
                ModelState.AddModelError(nameof(model.Role), "The linked doctor account must keep the Doctor role.");
                PopulateRoles(model.Role, true);
                return View(model);
            }

            if (currentRoles.Contains(CD.SUPER_ADMIN_ROLE) && model.Role != CD.SUPER_ADMIN_ROLE)
            {
                var superAdmins = await _userManager.GetUsersInRoleAsync(CD.SUPER_ADMIN_ROLE);
                if (superAdmins.Count <= 1)
                {
                    ModelState.AddModelError(nameof(model.Role), "The last SuperAdmin cannot be demoted.");
                    PopulateRoles(model.Role, linkedDoctor != null);
                    return View(model);
                }
            }

            var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
            await using var transaction = await _db.Database.BeginTransactionAsync();
            user.Name = model.Name.Trim();
            user.Address = model.Address.Trim();
            user.Email = email;
            user.UserName = username;
            if (emailChanged)
            {
                user.EmailConfirmed = false;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(updateResult);
                PopulateRoles(model.Role, linkedDoctor != null);
                return View(model);
            }

            var patient = await _db.Patients.SingleOrDefaultAsync(item => item.ApplicationUserId == user.Id);
            try
            {
                if (linkedDoctor != null)
                {
                    linkedDoctor.Name = user.Name;
                    await _db.SaveChangesAsync();
                }

                if (model.Role == CD.PATIENT_ROLE)
                {
                    if (patient == null)
                    {
                        patient = new Patient
                        {
                            ApplicationUserId = user.Id,
                            Name = user.Name,
                            Gender = model.Gender!,
                            DateOfBirth = DateOnly.FromDateTime(model.DateOfBirth!.Value)
                        };
                        _db.Patients.Add(patient);
                    }
                    else
                    {
                        patient.Name = user.Name;
                        patient.Gender = model.Gender!;
                        patient.DateOfBirth = DateOnly.FromDateTime(model.DateOfBirth!.Value);
                    }
                    await _db.SaveChangesAsync();
                }

                if (model.Role == CD.EMPLOYEE_ROLE
                    && !await _db.Receptionists.AnyAsync(item => item.ApplicationUserId == user.Id))
                {
                    _db.Receptionists.Add(new Receptionist { ApplicationUserId = user.Id });
                    await _db.SaveChangesAsync();
                }
                else if (model.Role != CD.EMPLOYEE_ROLE)
                {
                    _db.Receptionists.RemoveRange(
                        _db.Receptionists.Where(item => item.ApplicationUserId == user.Id));
                    await _db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "The user profile could not be saved.");
                PopulateRoles(model.Role, linkedDoctor != null);
                return View(model);
            }

            if (currentRoles.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddIdentityErrors(removeResult);
                    PopulateRoles(model.Role, linkedDoctor != null);
                    return View(model);
                }
            }

            if (linkedDoctor == null)
            {
                var addRoleResult = await _userManager.AddToRoleAsync(user, model.Role);
                if (!addRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddIdentityErrors(addRoleResult);
                    PopulateRoles(model.Role);
                    return View(model);
                }
            }
            else
            {
                var doctorRoleResult = await _userManager.AddToRoleAsync(user, CD.DOCTOR_ROLE);
                if (!doctorRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddIdentityErrors(doctorRoleResult);
                    PopulateRoles(CD.DOCTOR_ROLE, true);
                    return View(model);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.Password);
                if (!passwordResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddIdentityErrors(passwordResult);
                    PopulateRoles(model.Role, linkedDoctor != null);
                    return View(model);
                }
            }

            await transaction.CommitAsync();
            TempData["Success-Notification"] = "User account updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> ConfirmEmail(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (!user.EmailConfirmed)
            {
                var result = await _userManager.ConfirmEmailAsync(
                    user, await _userManager.GenerateEmailConfirmationTokenAsync(user));
                if (!result.Succeeded)
                {
                    TempData["Error-Notification"] = "Could not confirm this e-mail address.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["Success-Notification"] = "E-mail address confirmed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.Id == _userManager.GetUserId(User)
                || (await _userManager.IsInRoleAsync(user, CD.SUPER_ADMIN_ROLE)
                    && (await _userManager.GetUsersInRoleAsync(CD.SUPER_ADMIN_ROLE)).Count <= 1))
            {
                TempData["Error-Notification"] = "This account cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            return View(new AdminUserRowVM
            {
                User = user,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }

        [HttpPost]
        [Authorize(Roles = CD.SUPER_ADMIN_ROLE)]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["Error-Notification"] = "You cannot delete your own signed-in account.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, CD.SUPER_ADMIN_ROLE))
            {
                var superAdmins = await _userManager.GetUsersInRoleAsync(CD.SUPER_ADMIN_ROLE);
                if (superAdmins.Count <= 1)
                {
                    TempData["Error-Notification"] = "The last SuperAdmin account cannot be deleted.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var avatar = user.ImageUrl;
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var patient = await _db.Patients.SingleOrDefaultAsync(item => item.ApplicationUserId == user.Id);
                if (patient != null)
                {
                    var appointmentIds = await _db.Appointments.Where(appointment => appointment.PatientId == patient.Id)
                        .Select(appointment => appointment.Id).ToListAsync();
                    var medicalRecordIds = await _db.MedicalRecords.Where(record => appointmentIds.Contains(record.AppointmentId))
                        .Select(record => record.Id).ToListAsync();

                    _db.Prescriptions.RemoveRange(_db.Prescriptions.Where(item => medicalRecordIds.Contains(item.MedicalRecordId)));
                    _db.MedicalRecords.RemoveRange(_db.MedicalRecords.Where(item => appointmentIds.Contains(item.AppointmentId)));
                    _db.Appointments.RemoveRange(_db.Appointments.Where(item => item.PatientId == patient.Id));
                    _db.MedicalFiles.RemoveRange(_db.MedicalFiles.Where(item => item.PatientId == patient.Id));
                    _db.ClinicPatients.RemoveRange(_db.ClinicPatients.Where(item => item.PatientId == patient.Id));
                    _db.PatientDoctors.RemoveRange(_db.PatientDoctors.Where(item => item.PatientId == patient.Id));
                    _db.Patients.Remove(patient);
                }

                var linkedDoctor = await _db.Doctors.SingleOrDefaultAsync(item => item.ApplicationUserId == user.Id);
                if (linkedDoctor != null)
                {
                    linkedDoctor.ApplicationUserId = null;
                }

                _db.Receptionists.RemoveRange(_db.Receptionists.Where(item => item.ApplicationUserId == user.Id));
                _db.ApplicationUserOtps.RemoveRange(_db.ApplicationUserOtps.Where(item => item.ApplicationUserId == user.Id));
                await _db.SaveChangesAsync();

                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    TempData["Error-Notification"] = "The user account could not be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                TempData["Error-Notification"] = "The user could not be deleted because linked records still exist.";
                return RedirectToAction(nameof(Index));
            }

            _imageService.Delete(avatar, "Profiles");

            TempData["Success-Notification"] = "User account and linked patient records deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> LockUnLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, CD.SUPER_ADMIN_ROLE);
            if (isSuperAdmin)
            {
                TempData["Error-Notification"] = "You cannot change access for a SuperAdmin account.";
                return RedirectToAction(nameof(Index));
            }

            var isLocked = user.LockoutEnd is not null && DateTimeOffset.UtcNow < user.LockoutEnd;
            IdentityResult result;

            if (!isLocked)
            {
                result = await _userManager.SetLockoutEnabledAsync(user, true);
                if (result.Succeeded)
                {
                    result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10));
                }
            }
            else
            {
                result = await _userManager.SetLockoutEndDateAsync(user, null);
            }

            if (!result.Succeeded)
            {
                TempData["Error-Notification"] = "Could not update account access. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success-Notification"] = isLocked
                ? "User unlocked successfully."
                : "User locked for 10 minutes.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> IsAllowedRoleAsync(string role, bool allowDoctor = false) =>
            ((role is CD.SUPER_ADMIN_ROLE or CD.ADMIN_ROLE or CD.EMPLOYEE_ROLE or CD.PATIENT_ROLE)
                || (allowDoctor && role == CD.DOCTOR_ROLE))
            && await _roleManager.RoleExistsAsync(role);

        private void PopulateRoles(string? selected = null, bool includeDoctor = false)
        {
            var roles = new List<string> { CD.PATIENT_ROLE, CD.EMPLOYEE_ROLE, CD.ADMIN_ROLE, CD.SUPER_ADMIN_ROLE };
            if (includeDoctor)
            {
                roles.Insert(1, CD.DOCTOR_ROLE);
            }

            ViewBag.Roles = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                roles, selected);
        }

        private void ValidatePatientFields(AdminUserFormVM model)
        {
            if (model.Role != CD.PATIENT_ROLE)
            {
                return;
            }

            if (model.Gender is not ("Male" or "Female"))
            {
                ModelState.AddModelError(nameof(model.Gender), "Choose a valid gender for the patient profile.");
            }

            if (model.DateOfBirth is not { } dateOfBirth
                || dateOfBirth.Date > DateTime.Today.AddYears(-13)
                || dateOfBirth.Date < DateTime.Today.AddYears(-99))
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "The patient must be between 13 and 99 years old.");
            }
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
