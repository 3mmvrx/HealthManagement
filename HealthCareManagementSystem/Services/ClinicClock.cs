namespace HealthCareManagementSystem.Services
{
    /// <summary>
    /// The current time in the clinic's own time zone. Appointment times are stored as plain local
    /// clinic times, so every "is this in the past?" check must use this clock and not <c>DateTime.Now</c>
    /// (a hosting server is usually set to UTC).
    /// </summary>
    public interface IClinicClock
    {
        DateTime Now { get; }
    }

    public class ClinicClock : IClinicClock
    {
        private readonly TimeZoneInfo _zone;

        public ClinicClock(IConfiguration configuration)
        {
            _zone = FindZone(configuration["Appointments:TimeZone"]);
        }

        public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

        private static TimeZoneInfo FindZone(string? configured)
        {
            // IANA ids work on Linux and macOS (and on Windows with ICU); the Windows id is the fallback.
            foreach (var id in new[] { configured, "Africa/Cairo", "Egypt Standard Time" })
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Local;
        }
    }
}
