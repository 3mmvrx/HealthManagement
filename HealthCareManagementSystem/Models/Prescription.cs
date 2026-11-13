namespace HealthCareManagementSystem.Models
{
    public class Prescription
    {
        public int Id { get; set; }
        public string Notes { get; set; }
        public DateTime EndTime { get; set; }

        public int MedicalRecordId { get; set; }
        public MedicalRecord MedicalRecord { get; set; }

    }
}
