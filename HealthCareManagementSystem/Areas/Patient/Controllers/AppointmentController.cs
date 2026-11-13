using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthCareManagementSystem.PatientArea.Controllers
{
    [Area(CD.PATIENT_AREA)]
    [Authorize(Roles = CD.PATIENT_ROLE)]
    public class AppointmentController : Controller
    {
        private readonly IRepository<Patient> _patientRepository;
        private readonly IAppointmentService _appointmentService;

        public AppointmentController(
            IRepository<Patient> patientRepository,
            IAppointmentService appointmentService)
        {
            _patientRepository = patientRepository;
            _appointmentService = appointmentService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                TempData["Error-Notification"] = "Your patient profile could not be found. Please contact the clinic.";
                return RedirectToAction("Index", "Profile", new { area = CD.IDENTITY_AREA });
            }

            var appointments = await _appointmentService.GetForPatientAsync(patient.Id, cancellationToken);

            return View(appointments);
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                TempData["Error-Notification"] = "Your patient profile could not be found. Please contact the clinic.";
                return RedirectToAction("Index", "Profile", new { area = CD.IDENTITY_AREA });
            }

            var result = await _appointmentService.CancelByPatientAsync(id, patient.Id, cancellationToken);
            TempData[result.Succeeded ? "Success-Notification" : "Error-Notification"] = result.Succeeded
                ? "Your appointment was cancelled."
                : result.Error;

            return RedirectToAction(nameof(Index));
        }

        private Task<Patient?> GetCurrentPatientAsync()
        {
            var userId = User.GetUserId();

            return string.IsNullOrWhiteSpace(userId)
                ? Task.FromResult<Patient?>(null)
                : _patientRepository.GetOneAsync(patient => patient.ApplicationUserId == userId, tracked: false);
        }
    }
}
