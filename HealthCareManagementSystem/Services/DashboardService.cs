using HealthCareManagementSystem.Data;
using HealthCareManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthCareManagementSystem.Services
{
    public interface IDashboardService
    {
        Task<DashboardVM> GetAsync(CancellationToken cancellationToken = default);
    }

    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;
        private readonly IClinicClock _clock;

        public DashboardService(ApplicationDbContext db, IClinicClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<DashboardVM> GetAsync(CancellationToken cancellationToken = default)
        {
            var now = _clock.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var weekEnd = today.AddDays(7);

            // One query at a time: a DbContext cannot run two queries concurrently.
            var model = new DashboardVM
            {
                ClinicCount = await _db.Clinics.CountAsync(cancellationToken),
                DoctorCount = await _db.Doctors.CountAsync(cancellationToken),
                PatientCount = await _db.Patients.CountAsync(cancellationToken),

                TodayCount = await _db.Appointments.CountAsync(
                    a => a.AppointmentDate >= today
                      && a.AppointmentDate < tomorrow
                      && a.Status != AppointmentStatus.Cancelled,
                    cancellationToken),

                PendingCount = await _db.Appointments.CountAsync(
                    a => a.Status == AppointmentStatus.Pending,
                    cancellationToken),

                UpcomingCount = await _db.Appointments.CountAsync(
                    a => (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                      && a.AppointmentDate >= now,
                    cancellationToken)
            };

            model.NeedsAttention = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Where(a => a.Status == AppointmentStatus.Pending)
                .OrderBy(a => a.AppointmentDate)
                .Take(8)
                .ToListAsync(cancellationToken);

            model.NextUp = await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Where(a => (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                         && a.AppointmentDate >= now)
                .OrderBy(a => a.AppointmentDate)
                .Take(8)
                .ToListAsync(cancellationToken);

            var weekDates = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.Status != AppointmentStatus.Cancelled
                         && a.AppointmentDate >= today
                         && a.AppointmentDate < weekEnd)
                .Select(a => a.AppointmentDate)
                .ToListAsync(cancellationToken);

            model.Week = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var day = today.AddDays(offset);
                    return new DayCount(day, weekDates.Count(date => date.Date == day));
                })
                .ToList();

            var byStatus = await _db.Appointments
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            model.ByStatus = byStatus.ToDictionary(item => item.Status, item => item.Count);

            return model;
        }
    }
}
