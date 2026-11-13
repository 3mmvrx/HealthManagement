using System.Security.Cryptography;
using System.Text;

namespace HealthCareManagementSystem.Utilities
{
    /// <summary>Creates and checks the 6-digit one-time codes used to reset a forgotten password.</summary>
    public static class OtpHelper
    {
        public const int CodeLength = 6;

        /// <summary>After this many wrong guesses the code is burned and a new one must be requested.</summary>
        public const int MaxFailedAttempts = 5;

        /// <summary>At most this many codes can be requested per account in 24 hours.</summary>
        public const int MaxCodesPerDay = 5;

        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        /// <summary>A cryptographically random code such as "042917".</summary>
        public static string GenerateCode()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        public static string Hash(Guid otpId, string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{otpId:N}:{code}"));

            return Convert.ToHexString(bytes);
        }

        public static bool Verify(Guid otpId, string code, string expectedHash)
        {
            var actual = Encoding.ASCII.GetBytes(Hash(otpId, code));
            var expected = Encoding.ASCII.GetBytes(expectedHash ?? string.Empty);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
