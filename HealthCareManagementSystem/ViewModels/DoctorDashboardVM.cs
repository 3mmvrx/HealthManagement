namespace HealthCareManagementSystem.ViewModels
{
    public class DoctorDashboardVM
    {
        public Doctor Doctor { get; set; } = null!;
        public IReadOnlyList<Appointment> Appointments { get; set; } = [];
    }
}
