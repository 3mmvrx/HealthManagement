using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.Utilities.DbSeeder;

namespace HealthCareManagementSystem
{
    public static class ServiceRegistration
    {
        /// <summary>Registers the application's own services (repositories, e-mail, uploads, seeding).</summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // One open-generic registration covers IRepository<Clinic>, IRepository<Doctor>, ...
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            services.AddScoped<IDbInitializer, DbInitializer>();
            services.AddScoped<IImageService, ImageService>();
            services.AddTransient<IEmailSender, EmailSender>();

            // Appointment booking, slot calculation and the staff dashboard.
            services.AddSingleton<IClinicClock, ClinicClock>();
            services.AddScoped<IAppointmentService, AppointmentService>();
            services.AddScoped<IDashboardService, DashboardService>();

            return services;
        }
    }
}
