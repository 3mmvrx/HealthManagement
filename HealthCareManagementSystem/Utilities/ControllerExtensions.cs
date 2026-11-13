using Microsoft.AspNetCore.Mvc;

namespace HealthCareManagementSystem.Utilities
{
    public static class ControllerExtensions
    {
        /// <summary>
        /// Sends a signed-in user to the home page of the area their role belongs to.
        /// </summary>
        public static IActionResult RedirectToRoleHome(this ControllerBase controller, IEnumerable<string> roles)
        {
            var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);

            if (roleSet.Contains(CD.SUPER_ADMIN_ROLE)
                || roleSet.Contains(CD.ADMIN_ROLE)
                || roleSet.Contains(CD.EMPLOYEE_ROLE))
            {
                return controller.RedirectToAction("Index", "Home", new { area = CD.ADMIN_AREA });
            }

            if (roleSet.Contains(CD.PATIENT_ROLE))
            {
                return controller.RedirectToAction("Index", "Home", new { area = CD.PATIENT_AREA });
            }

            if (roleSet.Contains(CD.DOCTOR_ROLE))
            {
                return controller.RedirectToAction("Index", "Home", new { area = CD.DOCTOR_AREA });
            }

            return controller.RedirectToAction("Index", "Profile", new { area = CD.IDENTITY_AREA });
        }
    }
}
