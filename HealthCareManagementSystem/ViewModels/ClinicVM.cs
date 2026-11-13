using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.ViewModels
{
    public class ClinicVM
    {
        [Required(ErrorMessage = "Clinic name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number.")]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Photo")]
        public IFormFile? Image { get; set; }

        /// <summary>The picture that is currently stored; shown on the edit form.</summary>
        public string? ImageName { get; set; }
    }
}
