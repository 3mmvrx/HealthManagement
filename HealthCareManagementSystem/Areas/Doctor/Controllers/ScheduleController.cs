using HealthCareManagementSystem.Data;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoctorProfile = HealthCareManagementSystem.Models.Doctor;

namespace HealthCareManagementSystem.Areas.Doctor.Controllers
{
    [Area(CD.DOCTOR_AREA)]
    [Authorize(Roles = CD.DOCTOR_ROLE)]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ScheduleController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                return Forbid();
            }

            return View(new DoctorSchedulesPageVM
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Schedules = await _db.DoctorSchedules.AsNoTracking()
                    .Where(item => item.DoctorId == doctor.Id)
                    .OrderBy(item => item.DayOfWeek).ThenBy(item => item.StartTime)
                    .ToListAsync(),
                NewSchedule = new DoctorScheduleFormVM { DoctorId = doctor.Id }
            });
        }

        [HttpPost]
        public async Task<IActionResult> Save(DoctorScheduleFormVM model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                return Forbid();
            }

            model.DoctorId = doctor.Id;
            if (model.DayOfWeek is { } day && !Enum.IsDefined(day))
            {
                ModelState.AddModelError(nameof(model.DayOfWeek), "Choose a valid day.");
            }

            if (model.StartTime is { } start && model.EndTime is { } end && end <= start)
            {
                ModelState.AddModelError(nameof(model.EndTime), "The end time must be later than the start time.");
            }

            if (model.DayOfWeek is { } scheduleDay
                && model.StartTime is { } scheduleStart
                && model.EndTime is { } scheduleEnd
                && await _db.DoctorSchedules.AnyAsync(item => item.DoctorId == doctor.Id
                    && item.Id != model.ScheduleId
                    && item.DayOfWeek == scheduleDay
                    && scheduleStart < item.EndTime
                    && scheduleEnd > item.StartTime))
            {
                ModelState.AddModelError(nameof(model.StartTime), "This block overlaps another schedule block.");
            }

            if (!ModelState.IsValid)
            {
                return await BuildPageAsync(doctor, model);
            }

            DoctorSchedule? schedule;
            if (model.ScheduleId.HasValue)
            {
                schedule = await _db.DoctorSchedules.FirstOrDefaultAsync(item =>
                    item.Id == model.ScheduleId && item.DoctorId == doctor.Id);
                if (schedule == null)
                {
                    return NotFound();
                }
            }
            else
            {
                schedule = new DoctorSchedule { DoctorId = doctor.Id };
                _db.DoctorSchedules.Add(schedule);
            }

            schedule.DayOfWeek = model.DayOfWeek!.Value;
            schedule.StartTime = model.StartTime!.Value;
            schedule.EndTime = model.EndTime!.Value;
            schedule.SlotMinutes = model.SlotMinutes;
            await _db.SaveChangesAsync();

            TempData["Success-Notification"] = "Your availability was saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int scheduleId)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null)
            {
                return Forbid();
            }

            var schedule = await _db.DoctorSchedules.FirstOrDefaultAsync(item =>
                item.Id == scheduleId && item.DoctorId == doctor.Id);
            if (schedule == null)
            {
                return NotFound();
            }

            _db.DoctorSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            TempData["Success-Notification"] = "Availability block removed.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<DoctorProfile?> GetCurrentDoctorAsync()
        {
            var userId = User.GetUserId();
            return string.IsNullOrWhiteSpace(userId)
                ? null
                : await _db.Doctors.FirstOrDefaultAsync(item => item.ApplicationUserId == userId);
        }

        private async Task<ViewResult> BuildPageAsync(DoctorProfile doctor, DoctorScheduleFormVM form)
        {
            return View(nameof(Index), new DoctorSchedulesPageVM
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Schedules = await _db.DoctorSchedules.AsNoTracking()
                    .Where(item => item.DoctorId == doctor.Id)
                    .OrderBy(item => item.DayOfWeek).ThenBy(item => item.StartTime)
                    .ToListAsync(),
                NewSchedule = form
            });
        }
    }
}
