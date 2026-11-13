using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.ViewModels
{
    public class RegisterVM
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your address.")]
        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your e-mail address.")]
        [EmailAddress(ErrorMessage = "This is not a valid e-mail address.")]
        [StringLength(256)]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a user name.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "The user name must be 3 to 50 characters long.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use only letters, numbers, dots, dashes and underscores.")]
        [Display(Name = "User name")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose your gender.")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Please choose your gender.")]
        public string? Gender { get; set; }

        [Required(ErrorMessage = "Please enter your date of birth.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Please choose a password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please repeat the password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginVM
    {
        [Required(ErrorMessage = "Please enter your user name or e-mail.")]
        [Display(Name = "User name or e-mail")]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        /// <summary>Where to go after signing in (set when an anonymous visitor hit a protected page).</summary>
        public string? ReturnUrl { get; set; }
    }

    public class ForgotPasswordVM
    {
        [Required(ErrorMessage = "Please enter your user name or e-mail.")]
        [Display(Name = "User name or e-mail")]
        public string UserNameOrEmail { get; set; } = string.Empty;
    }

    public class ResendEmailConfirmationVM
    {
        [Required(ErrorMessage = "Please enter your user name or e-mail.")]
        [Display(Name = "User name or e-mail")]
        public string UserNameOrEmail { get; set; } = string.Empty;
    }

    public class ValidateOtpVM
    {
        [Required]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the 6-digit code.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "The code has 6 digits.")]
        [Display(Name = "Verification code")]
        public string OTP { get; set; } = string.Empty;
    }

    public class NewPasswordVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please repeat the new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
