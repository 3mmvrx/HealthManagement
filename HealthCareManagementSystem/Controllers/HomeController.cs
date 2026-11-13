using System.Diagnostics;
using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace HealthCareManagementSystem.Controllers
{
    /// <summary>The public part of the site: landing page, privacy page and the error page.</summary>
    public class HomeController : Controller
    {
        private readonly IRepository<Clinic> _clinicRepository;
        private readonly IRepository<Doctor> _doctorRepository;

        public HomeController(IRepository<Clinic> clinicRepository, IRepository<Doctor> doctorRepository)
        {
            _clinicRepository = clinicRepository;
            _doctorRepository = doctorRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var clinics = await _clinicRepository.GetAsync(
                tracked: false,
                orderBy: query => query.OrderBy(c => c.Name),
                take: 6);

            var doctors = await _doctorRepository.GetAsync(
                includes: [d => d.Clinic],
                tracked: false,
                orderBy: query => query.OrderBy(d => d.Name),
                take: 8);

            var model = new HomeIndexVM
            {
                ClinicCount = await _clinicRepository.CountAsync(),
                DoctorCount = await _doctorRepository.CountAsync(),
                Clinics = clinics.ToList(),
                Doctors = doctors.ToList()
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        // Also reached by UseStatusCodePagesWithReExecute for failed POST requests, so no anti-forgery check.
        [IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode
            };

            return View(model);
        }
    }
}
