namespace HealthCareManagementSystem.ViewModels
{
    /// <summary>Appointments on one day, used for the small bar chart on the dashboard.</summary>
    public record DayCount(DateTime Date, int Count);

    public class DashboardVM
    {
        public int ClinicCount { get; set; }

        public int DoctorCount { get; set; }

        public int PatientCount { get; set; }

        /// <summary>Appointments today that are not cancelled.</summary>
        public int TodayCount { get; set; }

        /// <summary>Appointments waiting for a staff member to confirm them.</summary>
        public int PendingCount { get; set; }

        /// <summary>Pending or confirmed appointments from now on.</summary>
        public int UpcomingCount { get; set; }

        /// <summary>Oldest pending requests first.</summary>
        public IReadOnlyList<Appointment> NeedsAttention { get; set; } = Array.Empty<Appointment>();

        /// <summary>The next appointments in time order.</summary>
        public IReadOnlyList<Appointment> NextUp { get; set; } = Array.Empty<Appointment>();

        /// <summary>Today and the six days after it.</summary>
        public IReadOnlyList<DayCount> Week { get; set; } = Array.Empty<DayCount>();

        public IReadOnlyDictionary<AppointmentStatus, int> ByStatus { get; set; } = new Dictionary<AppointmentStatus, int>();
    }
}
