using HealthCareManagementSystem.Data;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoctorProfile = HealthCareManagementSystem.Models.Doctor;

namespace HealthCareManagementSystem.Areas.Doctor.Controllers
{
    [Area(CD.DOCTOR_AREA)]
    [Authorize(Roles = CD.DOCTOR_ROLE)]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IClinicClock _clock;

        public HomeController(ApplicationDbContext db, IClinicClock clock)
        {
            _db = db;
            _clock = clock;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.GetUserId();
            var doctor = await _db.Doctors.AsNoTracking()
                .FirstOrDefaultAsync(item => item.ApplicationUserId == userId);
            if (doctor == null)
            {
                return Forbid();
            }

            var now = _clock.Now;
            var appointments = await _db.Appointments.AsNoTracking()
                .Include(item => item.Patient)
                .Where(item => item.DoctorId == doctor.Id
                    && item.Status != AppointmentStatus.Cancelled
                    && item.AppointmentDate >= now)
                .OrderBy(item => item.AppointmentDate)
                .ToListAsync();

            return View(new DoctorDashboardVM
            {
                Doctor = doctor,
                Appointments = appointments
            });
        }
    }
}
