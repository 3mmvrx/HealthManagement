using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthCareManagementSystem.PatientArea.Controllers
{
    [Area(CD.PATIENT_AREA)]
    [Authorize(Roles = CD.PATIENT_ROLE)]
    public class DoctorController : Controller
    {
        private readonly IRepository<Doctor> _doctorRepository;
        private readonly IRepository<Patient> _patientRepository;
        private readonly IAppointmentService _appointmentService;

        public DoctorController(
            IRepository<Doctor> doctorRepository,
            IRepository<Patient> patientRepository,
            IAppointmentService appointmentService)
        {
            _doctorRepository = doctorRepository;
            _patientRepository = patientRepository;
            _appointmentService = appointmentService;
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, DateOnly? date, CancellationToken cancellationToken)
        {
            var doctor = await _doctorRepository.GetOneAsync(
                d => d.Id == id,
                includes: [d => d.Clinic],
                tracked: false);

            if (doctor == null)
            {
                return NotFound();
            }

            var days = await _appointmentService.GetUpcomingSlotDaysAsync(id, cancellationToken);
            var availableDays = days.Where(day => day.FreeCount > 0).ToList();
            var selectedDay = date is { } selectedDate
                ? availableDays.FirstOrDefault(day => day.Date == selectedDate)
                : availableDays.FirstOrDefault();

            return View(new DoctorDetailsVM
            {
                Doctor = doctor,
                AvailableDays = availableDays,
                SelectedDate = selectedDay?.Date,
                AvailableSlots = selectedDay?.Slots.Where(slot => slot.IsAvailable).ToList() ?? [],
                Booking = new BookAppointmentVM
                {
                    DoctorId = doctor.Id,
                    SelectedDate = selectedDay?.Date
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> Book(BookAppointmentVM booking, CancellationToken cancellationToken)
        {
            if (booking.DoctorId <= 0)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                TempData["Error-Notification"] = "Choose an available appointment time.";
                return RedirectToAction(nameof(Details), new { id = booking.DoctorId, date = booking.SelectedDate });
            }

            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var patient = await _patientRepository.GetOneAsync(
                item => item.ApplicationUserId == userId,
                tracked: false);

            if (patient == null)
            {
                TempData["Error-Notification"] = "Your patient profile could not be found. Please contact the clinic.";
                return RedirectToAction("Index", "Profile", new { area = CD.IDENTITY_AREA });
            }

            var result = await _appointmentService.BookAsync(
                booking.DoctorId,
                patient.Id,
                booking.SlotStart!.Value,
                booking.Reason,
                cancellationToken);

            if (!result.Succeeded)
            {
                TempData["Error-Notification"] = result.Error;
                return RedirectToAction(
                    nameof(Details),
                    new { id = booking.DoctorId, date = DateOnly.FromDateTime(booking.SlotStart.Value) });
            }

            TempData["Success-Notification"] = result.Appointment?.Status == AppointmentStatus.Confirmed
                ? "Your appointment is booked and confirmed."
                : "Your appointment request was sent to the clinic.";

            return RedirectToAction("Index", "Appointment", new { area = CD.PATIENT_AREA });
        }
    }
}
