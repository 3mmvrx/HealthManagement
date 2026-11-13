using System.ComponentModel.DataAnnotations.Schema;

namespace HealthCareManagementSystem.Models
{
    /// <summary>
    /// A one-time code e-mailed to a user who forgot their password.
    /// Only a hash of the code is stored (see <see cref="OtpHelper"/>).
    /// </summary>
    public class ApplicationUserOtp
    {
        public Guid Id { get; set; }

        public string ApplicationUserId { get; set; }

        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser ApplicationUser { get; set; }

        /// <summary>Hash of the code, never the code itself.</summary>
        public string Code { get; set; }

        public bool IsValid { get; set; }

        /// <summary>How many wrong codes were entered; the code is burned after <see cref="OtpHelper.MaxFailedAttempts"/>.</summary>
        public int FailedAttempts { get; set; }

        public DateTime ValidTo { get; set; }

        public DateTime CreatedAt { get; set; }

        public ApplicationUserOtp()
        {
        }

        public ApplicationUserOtp(string applicationUserId, string plainCode)
        {
            Id = Guid.NewGuid();
            ApplicationUserId = applicationUserId;
            Code = OtpHelper.Hash(Id, plainCode);
            IsValid = true;
            FailedAttempts = 0;
            CreatedAt = DateTime.UtcNow;
            ValidTo = CreatedAt.Add(OtpHelper.Lifetime);
        }
    }
}
