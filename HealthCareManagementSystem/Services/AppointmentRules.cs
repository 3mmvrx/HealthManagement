namespace HealthCareManagementSystem.Services
{
    /// <summary>The rules for moving an appointment from one status to the next.</summary>
    public static class AppointmentRules
    {
        /// <summary>Pending and confirmed appointments still occupy their time slot.</summary>
        public static bool IsActive(AppointmentStatus status)
        {
            return status == AppointmentStatus.Pending || status == AppointmentStatus.Confirmed;
        }

        public static bool CanTransition(AppointmentStatus from, AppointmentStatus to)
        {
            return (from, to) switch
            {
                (AppointmentStatus.Pending, AppointmentStatus.Confirmed) => true,
                (AppointmentStatus.Pending, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Completed) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,
                _ => false
            };
        }

        /// <summary>The statuses staff can choose next, in the order the buttons are shown.</summary>
        public static IReadOnlyList<AppointmentStatus> NextStatuses(AppointmentStatus from)
        {
            return Enum.GetValues<AppointmentStatus>()
                .Where(to => CanTransition(from, to))
                .OrderBy(to => to == AppointmentStatus.Cancelled ? 1 : 0)
                .ToList();
        }

        /// <summary>CSS class of the coloured status badge (see site.css).</summary>
        public static string BadgeClass(AppointmentStatus status)
        {
            return "hc-status-" + status.ToString().ToLowerInvariant();
        }

        /// <summary>The label of the button that moves an appointment to <paramref name="to"/>.</summary>
        public static string ActionLabel(AppointmentStatus to)
        {
            return to switch
            {
                AppointmentStatus.Confirmed => "Confirm",
                AppointmentStatus.Completed => "Mark as completed",
                AppointmentStatus.Cancelled => "Cancel",
                _ => to.ToString()
            };
        }
    }
}
