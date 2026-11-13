namespace HealthCareManagementSystem.Models
{
    /// <summary>
    /// One block of a doctor's weekly working hours, for example "Monday 09:00-13:00, 30-minute visits".
    /// A doctor can have several blocks per day (for example a morning and an evening shift).
    /// </summary>
    public class DoctorSchedule
    {
        public int Id { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        /// <summary>Length of one appointment slot in minutes.</summary>
        public int SlotMinutes { get; set; } = 30;

        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
    }
}
