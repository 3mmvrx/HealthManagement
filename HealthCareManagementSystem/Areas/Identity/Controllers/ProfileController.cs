using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.Services;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthCareManagementSystem.Areas.Identity.Controllers
{
    [Area(CD.IDENTITY_AREA)]
    [Authorize]
    public class ProfileController : Controller
    {
        private const string ImageFolder = "Profiles";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IRepository<Patient> _patientRepository;
        private readonly IImageService _imageService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IRepository<Patient> patientRepository,
            IImageService imageService,
            ILogger<ProfileController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _patientRepository = patientRepository;
            _imageService = imageService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            return View(await BuildPageAsync(user));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = nameof(ProfilePageVM.Profile))] ProfileVM model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var isPatient = await _userManager.IsInRoleAsync(user, CD.PATIENT_ROLE);

            if (isPatient)
            {
                var prefix = nameof(ProfilePageVM.Profile) + ".";

                if (string.IsNullOrWhiteSpace(model.Gender))
                {
                    ModelState.AddModelError(prefix + nameof(ProfileVM.Gender), "Please choose your gender.");
                }

                if (model.DateOfBirth is not { } birthDate)
                {
                    ModelState.AddModelError(prefix + nameof(ProfileVM.DateOfBirth), "Please enter your date of birth.");
                }
                else if (birthDate.Date > DateTime.Today.AddYears(-13)
                    || birthDate.Date < DateTime.Today.AddYears(-99))
                {
                    ModelState.AddModelError(prefix + nameof(ProfileVM.DateOfBirth), "You must be between 13 and 99 years old.");
                }
            }

            string? newImageName = null;

            if (ModelState.IsValid && model.Image is { Length: > 0 })
            {
                var saved = await _imageService.SaveAsync(model.Image, ImageFolder);

                if (saved.Succeeded)
                {
                    newImageName = saved.FileName;
                }
                else
                {
                    ModelState.AddModelError(
                        nameof(ProfilePageVM.Profile) + "." + nameof(ProfileVM.Image),
                        saved.Error ?? "The picture could not be saved.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(nameof(Index), await BuildPageAsync(user, model));
            }

            var oldImageName = user.ImageUrl;

            user.Name = model.Name.Trim();
            user.Address = model.Address.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

            if (newImageName != null)
            {
                user.ImageUrl = newImageName;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _imageService.Delete(newImageName, ImageFolder);

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(nameof(Index), await BuildPageAsync(user, model));
            }

            if (isPatient)
            {
                await SavePatientRecordAsync(user, model);
            }

            if (newImageName != null)
            {
                _imageService.Delete(oldImageName, ImageFolder);
            }

            // The name and picture shown in the menu live in the sign-in cookie.
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success-Notification"] = "Your profile was updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePassword([Bind(Prefix = nameof(ProfilePageVM.Password))] ChangePasswordVM model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return View(nameof(Index), await BuildPageAsync(user, passwordModel: model));
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                var prefix = nameof(ProfilePageVM.Password) + ".";

                foreach (var error in result.Errors)
                {
                    var field = error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                        ? nameof(ChangePasswordVM.CurrentPassword)
                        : nameof(ChangePasswordVM.NewPassword);

                    ModelState.AddModelError(prefix + field, error.Description);
                }

                return View(nameof(Index), await BuildPageAsync(user, passwordModel: model));
            }

            // Changing the password renews the security stamp; sign in again so this session stays valid.
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success-Notification"] = "Your password was changed.";
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------------ helpers

        private async Task<ProfilePageVM> BuildPageAsync(
            ApplicationUser user, ProfileVM? profileModel = null, ChangePasswordVM? passwordModel = null)
        {
            var isPatient = await _userManager.IsInRoleAsync(user, CD.PATIENT_ROLE);
            var patient = isPatient
                ? await _patientRepository.GetOneAsync(p => p.ApplicationUserId == user.Id, tracked: false)
                : null;

            var page = new ProfilePageVM
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                ImageName = user.ImageUrl,
                IsPatient = isPatient,
                HasPatientRecord = patient != null,
                Profile = profileModel ?? new ProfileVM
                {
                    Name = user.Name,
                    Address = user.Address,
                    PhoneNumber = user.PhoneNumber,
                    Gender = patient?.Gender,
                    DateOfBirth = patient?.DateOfBirth.ToDateTime(TimeOnly.MinValue)
                },
                // Passwords are never sent back to the browser.
                Password = new ChangePasswordVM()
            };

            _ = passwordModel;

            return page;
        }

        /// <summary>Updates the patient record that belongs to the user, or creates it for accounts that registered before it existed.</summary>
        private async Task SavePatientRecordAsync(ApplicationUser user, ProfileVM model)
        {
            var patient = await _patientRepository.GetOneAsync(p => p.ApplicationUserId == user.Id);
            var birthDate = DateOnly.FromDateTime(model.DateOfBirth!.Value);

            try
            {
                if (patient == null)
                {
                    await _patientRepository.AddAsync(new Patient
                    {
                        Name = user.Name,
                        Gender = model.Gender!,
                        DateOfBirth = birthDate,
                        ApplicationUserId = user.Id
                    });
                }
                else
                {
                    patient.Name = user.Name;
                    patient.Gender = model.Gender!;
                    patient.DateOfBirth = birthDate;
                    _patientRepository.Update(patient);
                }

                await _patientRepository.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Could not save the patient record of user {UserId}.", user.Id);
                TempData["Warning-Notification"] = "Your profile was saved, but your gender and date of birth could not be stored. Please try again.";
            }
        }
    }
}
