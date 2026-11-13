namespace HealthCareManagementSystem.Models
{
    public class MedicalRecord
    {
        public int Id { get; set; }
        public string Diagnosis { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        public int AppointmentId { get; set; }
        public Appointment Appointment { get; set; }

        public ICollection<Prescription> Prescriptions { get; set; }
    }
}
