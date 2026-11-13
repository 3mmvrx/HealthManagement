namespace HealthCareManagementSystem.Models
{
    public class Clinic
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string? Description { get; set; }
        public string Address { get; set; }

        public string PhoneNumber { get; set; }


        public string? ImageName { get; set; }

        public ICollection<ClinicPatient> ClinicPatients { get; set; } = new List<ClinicPatient>();
    }
}
