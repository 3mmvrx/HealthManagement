namespace HealthCareManagementSystem.ViewModels
{
    public class HomeIndexVM
    {
        public int ClinicCount { get; set; }

        public int DoctorCount { get; set; }

        public IReadOnlyList<Clinic> Clinics { get; set; } = Array.Empty<Clinic>();

        public IReadOnlyList<Doctor> Doctors { get; set; } = Array.Empty<Doctor>();
    }
}
