namespace HealthCareManagementSystem.Services
{
    /// <summary>One bookable time. <see cref="IsAvailable"/> is false when it is taken or already in the past.</summary>
    public record SlotInfo(DateTime Start, bool IsAvailable);

    /// <summary>All slots of one calendar day.</summary>
    public record SlotDay(DateOnly Date, IReadOnlyList<SlotInfo> Slots)
    {
        public int FreeCount => Slots.Count(slot => slot.IsAvailable);
    }

    /// <summary>
    /// Turns a doctor's weekly schedule blocks into concrete appointment slots.
    /// Pure logic (no database, no clock) so it can be unit tested.
    /// </summary>
    public static class SlotCalculator
    {
        public const int MinimumSlotMinutes = 5;

        public static IReadOnlyList<SlotInfo> GetSlots(
            IEnumerable<DoctorSchedule> schedules,
            DateOnly date,
            IEnumerable<DateTime> bookedStarts,
            DateTime now)
        {
            var booked = new HashSet<DateTime>(bookedStarts);

            // A sorted set merges overlapping blocks and keeps the slots in time order.
            var starts = new SortedSet<DateTime>();

            foreach (var schedule in schedules.Where(s => s.DayOfWeek == date.DayOfWeek))
            {
                var length = TimeSpan.FromMinutes(Math.Max(MinimumSlotMinutes, schedule.SlotMinutes));
                var cursor = schedule.StartTime.ToTimeSpan();
                var end = schedule.EndTime.ToTimeSpan();

                // A visit has to fit completely inside the block.
                while (cursor + length <= end)
                {
                    starts.Add(date.ToDateTime(TimeOnly.FromTimeSpan(cursor)));
                    cursor += length;
                }
            }

            return starts
                .Select(start => new SlotInfo(start, start > now && !booked.Contains(start)))
                .ToList();
        }
    }
}
