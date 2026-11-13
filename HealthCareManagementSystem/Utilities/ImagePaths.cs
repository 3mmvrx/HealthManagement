namespace HealthCareManagementSystem.Utilities
{
    /// <summary>
    /// App-relative URLs (for <c>Url.Content</c>) of uploaded pictures, with a default picture when none was uploaded.
    /// Lower-case folder names are used everywhere so the paths also work on case-sensitive file systems.
    /// </summary>
    public static class ImagePaths
    {
        public const string DefaultClinic = "~/images/defaults/clinic.svg";
        public const string DefaultDoctor = "~/images/defaults/doctor.svg";
        public const string DefaultUser = "~/images/defaults/user.svg";

        public static string Clinic(string? fileName) => Build("Clinics", fileName, DefaultClinic);

        public static string Doctor(string? fileName) => Build("Doctors", fileName, DefaultDoctor);

        public static string User(string? fileName) => Build("Profiles", fileName, DefaultUser);

        private static string Build(string folder, string? fileName, string fallback)
        {
            return string.IsNullOrWhiteSpace(fileName)
                ? fallback
                : $"~/images/{folder}/{Path.GetFileName(fileName)}";
        }
    }
}
