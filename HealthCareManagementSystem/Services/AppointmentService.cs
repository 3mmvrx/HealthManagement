using System.Globalization;
using System.Net;
using HealthCareManagementSystem.Data;
using HealthCareManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthCareManagementSystem.Services
{
    public record ServiceResult(bool Succeeded, string? Error)
    {
        public static ServiceResult Ok() => new(true, null);

        public static ServiceResult Fail(string error) => new(false, error);
    }

    public record BookingResult(bool Succeeded, Appointment? Appointment, string? Error)
    {
        public static BookingResult Ok(Appointment appointment) => new(true, appointment, null);

        public static BookingResult Fail(string error) => new(false, null, error);
    }

    /// <summary>Filters of the staff appointment list; every property is optional.</summary>
    public class AppointmentFilter
    {
        /// <summary>Part of the patient's or the doctor's name.</summary>
        public string? Query { get; set; }

        public AppointmentStatus? Status { get; set; }

        public int? DoctorId { get; set; }

        public DateOnly? From { get; set; }

        public DateOnly? To { get; set; }
    }

    public interface IAppointmentService
    {
        /// <summary>All slots of one day, each marked as free or not.</summary>
        Task<IReadOnlyList<SlotInfo>> GetSlotsAsync(int doctorId, DateOnly date, CancellationToken cancellationToken = default);

        /// <summary>The next days (see Appointments:BookingWindowDays) that have at least one slot.</summary>
        Task<IReadOnlyList<SlotDay>> GetUpcomingSlotDaysAsync(int doctorId, CancellationToken cancellationToken = default);

        /// <summary>Books a slot for a patient. Never throws for "normal" failures; read <see cref="BookingResult.Error"/>.</summary>
        Task<BookingResult> BookAsync(int doctorId, int patientId, DateTime slotStart, string? reason, CancellationToken cancellationToken = default);

        /// <summary>Lets a patient cancel their own appointment (not too close to the visit).</summary>
        Task<ServiceResult> CancelByPatientAsync(int appointmentId, int patientId, CancellationToken cancellationToken = default);

        /// <summary>Staff action: confirm, complete or cancel an appointment.</summary>
        Task<ServiceResult> ChangeStatusAsync(int appointmentId, AppointmentStatus newStatus, CancellationToken cancellationToken = default);

        /// <summary>A patient's appointments, newest first, with doctor and clinic.</summary>
        Task<IReadOnlyList<Appointment>> GetForPatientAsync(int patientId, CancellationToken cancellationToken = default);

        /// <summary>One appointment with doctor, clinic, patient and medical records. Pass <paramref name="patientId"/> to make sure it belongs to that patient.</summary>
        Task<Appointment?> GetDetailsAsync(int appointmentId, int? patientId = null, CancellationToken cancellationToken = default);

        Task<PagedResult<Appointment>> SearchAsync(AppointmentFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    }

    public class AppointmentService : IAppointmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IClinicClock _clock;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(
            ApplicationDbContext db,
            IClinicClock clock,
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<AppointmentService> logger)
        {
            _db = db;
            _clock = clock;
            _emailSender = emailSender;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>How many days ahead patients can book.</summary>
        private int BookingWindowDays => Math.Clamp(_configuration.GetValue("Appointments:BookingWindowDays", 21), 1, 180);

        /// <summary>Patients cannot cancel online when the visit is closer than this many hours.</summary>
        private int CancelCutoffHours => Math.Max(0, _configuration.GetValue("Appointments:CancelCutoffHours", 2));

        /// <summary>When true, new appointments skip the "pending" step.</summary>
        private bool AutoConfirm => _configuration.GetValue("Appointments:AutoConfirm", false);

        // ------------------------------------------------------------------ slots

        public async Task<IReadOnlyList<SlotInfo>> GetSlotsAsync(
            int doctorId, DateOnly date, CancellationToken cancellationToken = default)
        {
            var day = date.DayOfWeek;

            var schedules = await _db.DoctorSchedules
                .AsNoTracking()
                .Where(s => s.DoctorId == doctorId && s.DayOfWeek == day)
                .ToListAsync(cancellationToken);

            if (schedules.Count == 0)
            {
                return Array.Empty<SlotInfo>();
            }

            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);

            var booked = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                         && a.Status != AppointmentStatus.Cancelled
                         && a.AppointmentDate >= dayStart
                         && a.AppointmentDate < dayEnd)
                .Select(a => a.AppointmentDate)
                .ToListAsync(cancellationToken);

            return SlotCalculator.GetSlots(schedules, date, booked, _clock.Now);
        }

        public async Task<IReadOnlyList<SlotDay>> GetUpcomingSlotDaysAsync(
            int doctorId, CancellationToken cancellationToken = default)
        {
            var schedules = await _db.DoctorSchedules
                .AsNoTracking()
                .Where(s => s.DoctorId == doctorId)
                .ToListAsync(cancellationToken);

            if (schedules.Count == 0)
            {
                return Array.Empty<SlotDay>();
            }

            var now = _clock.Now;
            var today = DateOnly.FromDateTime(now);
            var last = today.AddDays(BookingWindowDays);

            var from = today.ToDateTime(TimeOnly.MinValue);
            var to = last.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var booked = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId
                         && a.Status != AppointmentStatus.Cancelled
                         && a.AppointmentDate >= from
                         && a.AppointmentDate < to)
                .Select(a => a.AppointmentDate)
                .ToListAsync(cancellationToken);

            var days = new List<SlotDay>();

            for (var date = today; date <= last; date = date.AddDays(1))
            {
                var slots = SlotCalculator.GetSlots(schedules, date, booked, now);

                if (slots.Count > 0)
                {
                    days.Add(new SlotDay(date, slots));
                }
            }

            return days;
        }

        // ------------------------------------------------------------------ booking

        public async Task<BookingResult> BookAsync(
            int doctorId, int patientId, DateTime slotStart, string? reason, CancellationToken cancellationToken = default)
        {
            var now = _clock.Now;

            // Slots are whole minutes; drop seconds a hand-made request might carry.
            var start = new DateTime(slotStart.Year, slotStart.Month, slotStart.Day, slotStart.Hour, slotStart.Minute, 0);

            if (start <= now)
            {
                return BookingResult.Fail("That time has already passed. Please choose another one.");
            }

            if (DateOnly.FromDateTime(start) > DateOnly.FromDateTime(now).AddDays(BookingWindowDays))
            {
                return BookingResult.Fail($"Appointments can be booked up to {BookingWindowDays} days ahead.");
            }

            var doctor = await _db.Doctors
                .Include(d => d.Clinic)
                .FirstOrDefaultAsync(d => d.Id == doctorId, cancellationToken);

            if (doctor == null)
            {
                return BookingResult.Fail("This doctor could not be found.");
            }

            var patient = await _db.Patients
                .Include(p => p.ApplicationUser)
                .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

            if (patient == null)
            {
                return BookingResult.Fail("Your patient profile could not be found. Please complete your profile first.");
            }

            var slots = await GetSlotsAsync(doctorId, DateOnly.FromDateTime(start), cancellationToken);
            var slot = slots.FirstOrDefault(s => s.Start == start);

            if (slot == null)
            {
                return BookingResult.Fail("That time is not part of the doctor's schedule.");
            }

            if (!slot.IsAvailable)
            {
                return BookingResult.Fail("That time has just been taken. Please choose another one.");
            }

            var dayStart = start.Date;
            var dayEnd = dayStart.AddDays(1);

            var sameDayWithDoctor = await _db.Appointments.AnyAsync(
                a => a.PatientId == patientId
                  && a.DoctorId == doctorId
                  && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                  && a.AppointmentDate >= dayStart
                  && a.AppointmentDate < dayEnd,
                cancellationToken);

            if (sameDayWithDoctor)
            {
                return BookingResult.Fail("You already have an appointment with this doctor on that day.");
            }

            var sameTime = await _db.Appointments.AnyAsync(
                a => a.PatientId == patientId
                  && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)
                  && a.AppointmentDate == start,
                cancellationToken);

            if (sameTime)
            {
                return BookingResult.Fail("You already have another appointment at that time.");
            }

            var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

            if (cleanReason is { Length: > 500 })
            {
                cleanReason = cleanReason[..500];
            }

            var appointment = new Appointment
            {
                DoctorId = doctorId,
                PatientId = patientId,
                AppointmentDate = start,
                Status = AutoConfirm ? AppointmentStatus.Confirmed : AppointmentStatus.Pending,
                Reason = cleanReason,
                CreatedAt = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // The unique index on (doctor, time) caught two patients booking the same slot at once.
                _logger.LogInformation(ex, "Slot {Start} of doctor {DoctorId} was taken by someone else.", start, doctorId);
                _db.Entry(appointment).State = EntityState.Detached;

                return BookingResult.Fail("That time has just been taken. Please choose another one.");
            }

            var intro = appointment.Status == AppointmentStatus.Confirmed
                ? "Your appointment is confirmed."
                : "We received your appointment request. You will get another e-mail as soon as the clinic confirms it.";

            await TrySendAsync(
                patient.ApplicationUser?.Email,
                "Your appointment request",
                BuildMessage(patient.Name, intro, appointment, doctor));

            return BookingResult.Ok(appointment);
        }

        public async Task<ServiceResult> CancelByPatientAsync(
            int appointmentId, int patientId, CancellationToken cancellationToken = default)
        {
            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId, cancellationToken);

            if (appointment == null)
            {
                return ServiceResult.Fail("This appointment could not be found.");
            }

            if (!AppointmentRules.IsActive(appointment.Status))
            {
                return ServiceResult.Fail("This appointment can no longer be cancelled.");
            }

            if (appointment.AppointmentDate <= _clock.Now.AddHours(CancelCutoffHours))
            {
                return ServiceResult.Fail(
                    $"Appointments can only be cancelled online at least {CancelCutoffHours} hours in advance. Please call the clinic.");
            }

            appointment.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync(cancellationToken);

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> ChangeStatusAsync(
            int appointmentId, AppointmentStatus newStatus, CancellationToken cancellationToken = default)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.ApplicationUser)
                .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

            if (appointment == null)
            {
                return ServiceResult.Fail("This appointment could not be found.");
            }

            if (!AppointmentRules.CanTransition(appointment.Status, newStatus))
            {
                return ServiceResult.Fail(
                    $"A {appointment.Status.ToString().ToLowerInvariant()} appointment cannot be changed to {newStatus.ToString().ToLowerInvariant()}.");
            }

            if (newStatus == AppointmentStatus.Completed && appointment.AppointmentDate > _clock.Now)
            {
                return ServiceResult.Fail("A visit can only be completed once its time has come.");
            }

            appointment.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);

            if (newStatus == AppointmentStatus.Confirmed)
            {
                await TrySendAsync(
                    appointment.Patient.ApplicationUser?.Email,
                    "Your appointment is confirmed",
                    BuildMessage(appointment.Patient.Name, "Good news - your appointment is confirmed.", appointment, appointment.Doctor));
            }
            else if (newStatus == AppointmentStatus.Cancelled)
            {
                await TrySendAsync(
                    appointment.Patient.ApplicationUser?.Email,
                    "Your appointment was cancelled",
                    BuildMessage(appointment.Patient.Name, "Unfortunately the clinic had to cancel your appointment. You are welcome to book another time.", appointment, appointment.Doctor));
            }

            return ServiceResult.Ok();
        }

        // ------------------------------------------------------------------ reading

        public async Task<IReadOnlyList<Appointment>> GetForPatientAsync(
            int patientId, CancellationToken cancellationToken = default)
        {
            return await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Clinic)
                .Where(a => a.PatientId == patientId)
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<Appointment?> GetDetailsAsync(
            int appointmentId, int? patientId = null, CancellationToken cancellationToken = default)
        {
            return await _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Clinic)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.ApplicationUser)
                .Include(a => a.MedicalRecords)
                    .ThenInclude(m => m.Prescriptions)
                .FirstOrDefaultAsync(
                    a => a.Id == appointmentId && (patientId == null || a.PatientId == patientId),
                    cancellationToken);
        }

        public async Task<PagedResult<Appointment>> SearchAsync(
            AppointmentFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<Appointment> query = _db.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                .Include(a => a.Patient);

            if (filter.Status is { } status)
            {
                query = query.Where(a => a.Status == status);
            }

            if (filter.DoctorId is { } doctorId)
            {
                query = query.Where(a => a.DoctorId == doctorId);
            }

            if (filter.From is { } from)
            {
                var fromDate = from.ToDateTime(TimeOnly.MinValue);
                query = query.Where(a => a.AppointmentDate >= fromDate);
            }

            if (filter.To is { } to)
            {
                var beforeDate = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
                query = query.Where(a => a.AppointmentDate < beforeDate);
            }

            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                var text = filter.Query.Trim();
                query = query.Where(a => a.Patient.Name.Contains(text) || a.Doctor.Name.Contains(text));
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(a => a.AppointmentDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Appointment>(items, total, page, pageSize);
        }

        // ------------------------------------------------------------------ e-mail

        private static string BuildMessage(string patientName, string intro, Appointment appointment, Doctor doctor)
        {
            var when = appointment.AppointmentDate.ToString("dddd, d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);

            return $"<p>Hello {WebUtility.HtmlEncode(patientName)},</p>"
                 + $"<p>{WebUtility.HtmlEncode(intro)}</p>"
                 + "<p>"
                 + $"<strong>Doctor:</strong> {WebUtility.HtmlEncode(doctor.Name)} ({WebUtility.HtmlEncode(doctor.Specialization)})<br />"
                 + $"<strong>When:</strong> {WebUtility.HtmlEncode(when)}"
                 + "</p>"
                 + "<p>HealthCare Management System</p>";
        }

        /// <summary>A failing mail server must never make a booking or a status change fail.</summary>
        private async Task TrySendAsync(string? to, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            try
            {
                await _emailSender.SendEmailAsync(to, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "The e-mail '{Subject}' could not be sent.", subject);
            }
        }
    }
}
