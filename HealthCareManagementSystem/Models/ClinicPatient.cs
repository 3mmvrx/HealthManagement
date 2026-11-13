using Microsoft.EntityFrameworkCore;

namespace HealthCareManagementSystem.Models
{
    [PrimaryKey(nameof(ClinicId), nameof(PatientId))]
    public class ClinicPatient
    {
        public int ClinicId { get; set; }
        public Clinic Clinic { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; }
    }
}
