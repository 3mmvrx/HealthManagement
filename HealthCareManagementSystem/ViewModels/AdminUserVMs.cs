using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.ViewModels
{
    public class AdminUserFormVM
    {
        public string? Id { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[A-Za-z0-9._-]+$")]
        [Display(Name = "User name")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = CD.PATIENT_ROLE;

        [RegularExpression("^(Male|Female)$")]
        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }
    }

    public class AdminUserRowVM
    {
        public ApplicationUser User { get; set; } = null!;
        public IList<string> Roles { get; set; } = [];
        public bool IsLocked { get; set; }
    }

    public class AdminUsersPageVM
    {
        public IReadOnlyList<AdminUserRowVM> Users { get; set; } = [];
    }
}
