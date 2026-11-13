namespace HealthCareManagementSystem.Utilities
{
    /// <summary>
    /// SMTP settings, bound from the "Email" configuration section.
    /// Never put real credentials in appsettings.json - use user-secrets or environment variables
    /// (Email__UserName, Email__Password, ...).
    /// </summary>
    public class EmailSettings
    {
        public const string SectionName = "Email";

        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? From { get; set; }
    }
}
