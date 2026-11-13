using System.ComponentModel.DataAnnotations;

namespace HealthCareManagementSystem.Models
{
    /// <summary>
    /// Life cycle of an appointment: Pending -> Confirmed -> Completed, or Cancelled at any point before it is completed.
    /// The numeric values are stored in the database (and used by the filtered unique index in
    /// <c>ApplicationDbContext</c>), so do not renumber them.
    /// </summary>
    public enum AppointmentStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2,
        Completed = 3
    }

    public class Appointment
    {
        public int Id { get; set; }

        /// <summary>Start of the visit (local clinic time).</summary>
        public DateTime AppointmentDate { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        /// <summary>Why the patient wants to see the doctor (optional).</summary>
        [StringLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; }

        public ICollection<MedicalRecord> MedicalRecords { get; set; }
    }
}
