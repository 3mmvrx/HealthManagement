using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HealthCareManagementSystem.Services
{
    /// <summary>
    /// Adds the user's display name and picture to the sign-in cookie so the layouts can show them
    /// without a database query on every request. Call <c>SignInManager.RefreshSignInAsync</c> after
    /// the profile changes to refresh these values.
    /// </summary>
    public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public AppClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            identity.AddClaim(new Claim(
                AppClaimTypes.FullName,
                string.IsNullOrWhiteSpace(user.Name) ? user.UserName ?? string.Empty : user.Name));

            if (!string.IsNullOrWhiteSpace(user.ImageUrl))
            {
                identity.AddClaim(new Claim(AppClaimTypes.Avatar, user.ImageUrl));
            }

            return identity;
        }
    }
}
