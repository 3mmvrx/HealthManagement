using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.ViewModels
{
    public class DoctorVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Doctor name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialization is required.")]
        [StringLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose a clinic.")]
        [Display(Name = "Clinic")]
        public int? ClinicId { get; set; }

        [Display(Name = "Photo")]
        public IFormFile? Image { get; set; }

        /// <summary>The picture that is currently stored; shown on the edit form.</summary>
        public string? ImageName { get; set; }

        public string? ApplicationUserId { get; set; }

        public bool CreateLogin { get; set; }

        [EmailAddress, StringLength(256)]
        [Display(Name = "Doctor account e-mail")]
        public string? Email { get; set; }

        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[A-Za-z0-9._-]+$")]
        [Display(Name = "Doctor account user name")]
        public string? UserName { get; set; }

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }
    }
}
