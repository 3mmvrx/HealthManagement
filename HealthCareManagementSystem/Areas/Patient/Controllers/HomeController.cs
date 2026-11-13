using Microsoft.AspNetCore.Mvc;

namespace HealthCareManagementSystem.Areas.Patinet.Controllers
{
    [Area(CD.PATIENT_AREA)]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
