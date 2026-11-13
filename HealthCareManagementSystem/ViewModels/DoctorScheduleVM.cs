using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.ViewModels
{
    public class DoctorScheduleFormVM
    {
        [Range(1, int.MaxValue)]
        public int DoctorId { get; set; }

        public int? ScheduleId { get; set; }

        [Required]
        public DayOfWeek? DayOfWeek { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeOnly? StartTime { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeOnly? EndTime { get; set; }

        [Range(5, 240)]
        public int SlotMinutes { get; set; } = 30;
    }

    public class DoctorSchedulesPageVM
    {
        public int DoctorId { get; set; }

        public string DoctorName { get; set; } = string.Empty;

        public IReadOnlyList<DoctorSchedule> Schedules { get; set; } = [];

        public DoctorScheduleFormVM NewSchedule { get; set; } = new();
    }
}
