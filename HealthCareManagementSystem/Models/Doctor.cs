namespace HealthCareManagementSystem.Models
{
    public class Doctor
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Specialization { get; set; }
        

        public int ClinicId { get; set; }
        public Clinic Clinic { get; set; }
        public string? ImageName { get; set; }
        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public ICollection<Appointment> Appointments { get; set; }
        public ICollection<DoctorSchedule> DoctorSchedules { get; set; }
        public ICollection<PatientDoctor> PatientDoctors { get; set; }
    }
}
