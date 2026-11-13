using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthCareManagementSystem.Areas.Admin.Controllers
{
    [Area(CD.ADMIN_AREA)]
    [Authorize(Roles = $"{CD.SUPER_ADMIN_ROLE}  , {CD.ADMIN_ROLE} , {CD.EMPLOYEE_ROLE}")]
    
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        
        public IActionResult NotFoundPage()
        {
            return View();
        }
    }
}
