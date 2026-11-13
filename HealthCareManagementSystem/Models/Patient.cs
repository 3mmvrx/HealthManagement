using System.ComponentModel.DataAnnotations.Schema;

namespace HealthCareManagementSystem.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public string? Description { get; set; }
        public DateOnly DateOfBirth { get; set; }

        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }

        /// <summary>Calculated from <see cref="DateOfBirth"/>, so it can never go stale.</summary>
        [NotMapped]
        public int Age
        {
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - DateOfBirth.Year;

                if (DateOfBirth > today.AddYears(-age))
                {
                    age--;
                }

                return age;
            }
        }

        public ICollection<Appointment> Appointments { get; set; }
        public ICollection<PatientDoctor> PatientDoctors { get; set; }
        public ICollection<ClinicPatient> ClinicPatients { get; set; }
        public ICollection<MedicalFile> MedicalFiles { get; set; }
    }
}
