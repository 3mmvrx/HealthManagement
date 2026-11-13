using System.ComponentModel.DataAnnotations;
using HealthCareManagementSystem.Services;

namespace HealthCareManagementSystem.ViewModels
{
    public class DoctorDetailsVM
    {
        public Doctor Doctor { get; set; } = null!;

        public IReadOnlyList<SlotDay> AvailableDays { get; set; } = [];

        public DateOnly? SelectedDate { get; set; }

        public IReadOnlyList<SlotInfo> AvailableSlots { get; set; } = [];

        public BookAppointmentVM Booking { get; set; } = new();
    }

    public class BookAppointmentVM
    {
        [Range(1, int.MaxValue)]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Choose an available appointment time.")]
        public DateTime? SlotStart { get; set; }

        public DateOnly? SelectedDate { get; set; }

        [StringLength(500)]
        [Display(Name = "Reason for visit (optional)")]
        public string? Reason { get; set; }
    }
}
