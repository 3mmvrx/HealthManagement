using HealthCareManagementSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HealthCareManagementSystem.Utilities.DbSeeder
{
    public class DbInitializer : IDbInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DbInitializer> _logger;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public DbInitializer(
            ApplicationDbContext context,
            ILogger<DbInitializer> logger,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _roleManager = roleManager;
            _userManager = userManager;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Apply pending migrations automatically in Development, or when explicitly enabled
                // with "Database:AutoMigrate": true (for example on a first deployment).
                if (_environment.IsDevelopment() || _configuration.GetValue<bool>("Database:AutoMigrate"))
                {
                    await _context.Database.MigrateAsync();
                }

                await SeedRolesAsync();
                await SeedSampleDoctorsAsync();
                await SeedSuperAdminAsync();
            }
            catch (Exception ex)
            {
                // Fail fast: running with a half-initialised database only produces confusing errors later.
                _logger.LogError(ex, "Database initialisation failed.");
                throw;
            }
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[] { CD.SUPER_ADMIN_ROLE, CD.ADMIN_ROLE, CD.EMPLOYEE_ROLE, CD.PATIENT_ROLE, CD.DOCTOR_ROLE };

            foreach (var role in roles)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    continue;
                }

                var result = await _roleManager.CreateAsync(new IdentityRole(role));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create role '{role}': " + string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        private async Task SeedSampleDoctorsAsync()
        {
            var clinic = await _context.Clinics.OrderBy(c => c.Id).FirstOrDefaultAsync();

            if (clinic == null)
            {
                clinic = new Clinic
                {
                    Name = "Community Health Clinic",
                    Address = "100 Wellness Way",
                    PhoneNumber = "555-0100",
                    Description = "A community clinic for sample doctor records."
                };

                _context.Clinics.Add(clinic);
                await _context.SaveChangesAsync();
            }

            var sampleDoctors = new[]
            {
                (Name: "Avery Bennett", Specialization: "Cardiology"),
                (Name: "Jordan Patel", Specialization: "Family Medicine"),
                (Name: "Morgan Reyes", Specialization: "Pediatrics"),
                (Name: "Taylor Kim", Specialization: "Dermatology"),
                (Name: "Cameron Brooks", Specialization: "Neurology")
            };

            var existingNames = (await _context.Doctors
                .Select(doctor => doctor.Name)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var sampleDoctor in sampleDoctors)
            {
                if (existingNames.Add(sampleDoctor.Name))
                {
                    _context.Doctors.Add(new Doctor
                    {
                        Name = sampleDoctor.Name,
                        Specialization = sampleDoctor.Specialization,
                        ClinicId = clinic.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedSuperAdminAsync()
        {
            var existingSuperAdmins = await _userManager.GetUsersInRoleAsync(CD.SUPER_ADMIN_ROLE);

            if (existingSuperAdmins.Count > 0)
            {
                return;
            }

            var userName = _configuration["SeedAdmin:UserName"];
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = "SuperAdmin";
            }

            var email = _configuration["SeedAdmin:Email"];
            if (string.IsNullOrWhiteSpace(email))
            {
                email = "superadmin@example.com";
            }

            var password = _configuration["SeedAdmin:Password"];
            var passwordWasGenerated = false;

            if (string.IsNullOrWhiteSpace(password))
            {
                if (!_environment.IsDevelopment())
                {
                    _logger.LogWarning(
                        "No SuperAdmin exists and 'SeedAdmin:Password' is not configured, so no SuperAdmin was created.");
                    return;
                }

                password = GeneratePassword();
                passwordWasGenerated = true;
            }

            // Never promote an account that already exists (it could have been self-registered).
            if (await _userManager.FindByNameAsync(userName) != null)
            {
                _logger.LogError(
                    "Cannot seed the SuperAdmin: a user named '{UserName}' already exists and is not a SuperAdmin.",
                    userName);
                return;
            }

            var user = new ApplicationUser
            {
                Name = "Super Admin",
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                Address = "N/A"
            };

            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                _logger.LogError(
                    "Could not create the SuperAdmin user: {Errors}",
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            await _userManager.AddToRoleAsync(user, CD.SUPER_ADMIN_ROLE);

            if (passwordWasGenerated)
            {
                _logger.LogWarning(
                    "Development SuperAdmin created. UserName: {UserName}  Password: {Password}  (generated, shown only once; " +
                    "set SeedAdmin:Password in user-secrets to choose your own)",
                    userName, password);
            }
            else
            {
                _logger.LogInformation("SuperAdmin '{UserName}' created from configuration.", userName);
            }
        }

        private static string GeneratePassword()
        {
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string digits = "23456789";
            const string symbols = "!@#$%*?";
            const string all = lower + upper + digits + symbols;

            var chars = new List<char>
            {
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };

            while (chars.Count < 16)
            {
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
            }

            // Fisher-Yates shuffle so the guaranteed characters are not always first.
            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }
    }
}
