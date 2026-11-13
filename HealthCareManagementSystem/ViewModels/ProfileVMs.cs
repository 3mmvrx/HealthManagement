using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.ViewModels
{
    /// <summary>The profile page holds two independent forms; each one posts its own part of this model.</summary>
    public class ProfilePageVM
    {
        public ProfileVM Profile { get; set; } = new();

        public ChangePasswordVM Password { get; set; } = new();

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        /// <summary>File name of the current profile picture.</summary>
        public string? ImageName { get; set; }

        public bool IsPatient { get; set; }

        /// <summary>False for a patient who registered before patient records existed; they must add gender and birth date.</summary>
        public bool HasPatientRecord { get; set; }
    }

    public class ProfileVM
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your address.")]
        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Phone(ErrorMessage = "This is not a valid phone number.")]
        [StringLength(30)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Profile picture")]
        public IFormFile? Image { get; set; }

        // The two fields below are only used for patients.
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Please choose your gender.")]
        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }
    }

    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Please enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please repeat the new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
