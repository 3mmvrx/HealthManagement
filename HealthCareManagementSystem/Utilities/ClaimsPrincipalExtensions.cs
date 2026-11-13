using System.Security.Claims;

namespace HealthCareManagementSystem.Utilities
{
    public static class AppClaimTypes
    {
        public const string FullName = "hc:full-name";
        public const string Avatar = "hc:avatar";
    }

    public static class ClaimsPrincipalExtensions
    {
        /// <summary>The signed-in user's display name (falls back to the user name).</summary>
        public static string GetFullName(this ClaimsPrincipal user)
        {
            var name = user.FindFirstValue(AppClaimTypes.FullName);

            return string.IsNullOrWhiteSpace(name) ? user.Identity?.Name ?? string.Empty : name;
        }

        /// <summary>File name of the signed-in user's profile picture, or null.</summary>
        public static string? GetAvatar(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(AppClaimTypes.Avatar);
        }

        /// <summary>The signed-in user's id (the Identity user id), or null when nobody is signed in.</summary>
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public static bool IsStaff(this ClaimsPrincipal user)
        {
            return user.IsInRole(CD.SUPER_ADMIN_ROLE)
                || user.IsInRole(CD.ADMIN_ROLE)
                || user.IsInRole(CD.EMPLOYEE_ROLE);
        }
    }
}
