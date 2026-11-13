using System.Net;
using HealthCareManagementSystem.Repositories;
using HealthCareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HealthCareManagementSystem.Areas.Identity.Controllers
{
    [Area(CD.IDENTITY_AREA)]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly IRepository<ApplicationUserOtp> _otpRepository;
        private readonly IRepository<Patient> _patientRepository;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            IRepository<ApplicationUserOtp> otpRepository,
            IRepository<Patient> patientRepository,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _otpRepository = otpRepository;
            _patientRepository = patientRepository;
            _logger = logger;
        }

        // ------------------------------------------------------------------ register

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home", new { area = string.Empty });
            }

            return View(new RegisterVM());
        }

        [HttpPost]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(RegisterVM model)
        {
            var userName = model.UserName?.Trim() ?? string.Empty;
            var email = model.Email?.Trim() ?? string.Empty;

            if (model.DateOfBirth is { } birthDate
                && (birthDate.Date > DateTime.Today.AddYears(-13)
                    || birthDate.Date < DateTime.Today.AddYears(-99)))
            {
                ModelState.AddModelError(nameof(RegisterVM.DateOfBirth), "You must be between 13 and 99 years old.");
            }

            if (ModelState.IsValid)
            {
                if (await _userManager.FindByNameAsync(userName) != null)
                {
                    ModelState.AddModelError(nameof(RegisterVM.UserName), "This user name is already taken.");
                }

                if (await _userManager.FindByEmailAsync(email) != null)
                {
                    ModelState.AddModelError(nameof(RegisterVM.Email), "This e-mail address is already registered.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                Name = model.Name.Trim(),
                Address = model.Address.Trim(),
                Email = email,
                UserName = userName,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            // The account only counts as created when it has its role and its patient record. If one of
            // those steps fails the user is removed again, so the visitor can simply try again.
            var roleResult = await _userManager.AddToRoleAsync(user, CD.PATIENT_ROLE);

            if (!roleResult.Succeeded)
            {
                _logger.LogError("Could not add the Patient role to a new user: {Errors}",
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));

                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Your account could not be created. Please try again in a moment.");

                return View(model);
            }

            try
            {
                await _patientRepository.AddAsync(new Patient
                {
                    Name = user.Name,
                    Gender = model.Gender!,
                    DateOfBirth = DateOnly.FromDateTime(model.DateOfBirth!.Value),
                    ApplicationUserId = user.Id
                });

                await _patientRepository.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Could not save the patient record of a new user.");

                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Your account could not be created. Please try again in a moment.");

                return View(model);
            }

            TempData["Success-Notification"] = "Your account was created. You can log in now.";

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error-Notification"] = "This confirmation link is not valid.";
                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error-Notification"] = "This confirmation link is not valid.";
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (!result.Succeeded)
            {
                TempData["Error-Notification"] = "This confirmation link has expired or was already used. Request a new one below.";
                return RedirectToAction(nameof(ResendEmailConfirmation));
            }

            TempData["Success-Notification"] = "Your e-mail address is confirmed. You can log in now.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ResendEmailConfirmation()
        {
            return View(new ResendEmailConfirmationVM());
        }

        [HttpPost]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResendEmailConfirmation(ResendEmailConfirmationVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            TempData["Info-Notification"] = "Email confirmation is temporarily disabled. You can log in without confirming your address.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Url.IsLocalUrl(returnUrl)
                    ? LocalRedirect(returnUrl!)
                    : RedirectToAction("Index", "Home", new { area = string.Empty });
            }

            return View(new LoginVM { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await FindUserAsync(model.UserNameOrEmail);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "The user name or e-mail, or the password, is not correct.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (Url.IsLocalUrl(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl!);
                }

                var roles = await _userManager.GetRolesAsync(user);
                return this.RedirectToRoleHome(roles);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Your account is locked. Try again later or contact the clinic.");
            }
            else if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, "Sign-in is currently not allowed for this account.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "The user name or e-mail, or the password, is not correct.");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            TempData["Success-Notification"] = "You have been logged out.";
            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordVM());
        }

        [HttpPost]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var login = model.UserNameOrEmail.Trim();
            var user = await FindUserAsync(login);

            if (user != null && user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email))
            {
                var since = DateTime.UtcNow.AddHours(-24);
                var requestedToday = await _otpRepository.CountAsync(
                    o => o.ApplicationUserId == user.Id && o.CreatedAt >= since);

                if (requestedToday < OtpHelper.MaxCodesPerDay)
                {
                    // Only the newest code may be used.
                    var oldCodes = await _otpRepository.GetAsync(o => o.ApplicationUserId == user.Id && o.IsValid);

                    foreach (var oldCode in oldCodes)
                    {
                        oldCode.IsValid = false;
                    }

                    var code = OtpHelper.GenerateCode();
                    await _otpRepository.AddAsync(new ApplicationUserOtp(user.Id, code));
                    await _otpRepository.CommitAsync();

                    try
                    {
                        await _emailSender.SendEmailAsync(
                            user.Email,
                            "Your password reset code",
                            $"<p>Hello {WebUtility.HtmlEncode(user.Name)},</p>"
                            + $"<p>Your verification code is <strong style=\"font-size:1.4em;letter-spacing:3px\">{code}</strong></p>"
                            + $"<p>It is valid for {(int)OtpHelper.Lifetime.TotalMinutes} minutes. If you did not ask for it, you can ignore this e-mail.</p>");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "The password reset e-mail could not be sent.");
                    }
                }
            }

            // Same answer in every case: no hint whether the account exists.
            TempData["Info-Notification"] =
                "If an eligible account matches, a 6-digit reset code may have been sent to its e-mail address. Enter it below or request another code.";

            return RedirectToAction(nameof(ValidateOtp), new { userNameOrEmail = login });
        }

        [HttpGet]
        public IActionResult ValidateOtp(string? userNameOrEmail)
        {
            if (string.IsNullOrWhiteSpace(userNameOrEmail))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ValidateOtpVM { UserNameOrEmail = userNameOrEmail });
        }

        [HttpPost]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ValidateOtp(ValidateOtpVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            const string failure = "The code is wrong or has expired. Check your e-mail, or request a new code.";

            var user = await FindUserAsync(model.UserNameOrEmail);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, failure);
                return View(model);
            }

            var codes = await _otpRepository.GetAsync(
                o => o.ApplicationUserId == user.Id && o.IsValid,
                orderBy: query => query.OrderByDescending(o => o.CreatedAt));

            var otp = codes.FirstOrDefault();

            if (otp == null || otp.ValidTo < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, failure);
                return View(model);
            }

            if (!OtpHelper.Verify(otp.Id, model.OTP, otp.Code))
            {
                otp.FailedAttempts++;

                if (otp.FailedAttempts >= OtpHelper.MaxFailedAttempts)
                {
                    otp.IsValid = false;
                }

                await _otpRepository.CommitAsync();

                ModelState.AddModelError(string.Empty, failure);
                return View(model);
            }

            otp.IsValid = false;
            await _otpRepository.CommitAsync();

            TempData["ResetToken"] = await _userManager.GeneratePasswordResetTokenAsync(user);

            return RedirectToAction(nameof(NewPassword), new { userId = user.Id });
        }

        [HttpGet]
        public IActionResult NewPassword(string? userId)
        {
            var token = TempData["ResetToken"] as string;

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error-Notification"] = "Your reset session has expired. Please start again.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new NewPasswordVM { UserId = userId, Token = token });
        }

        [HttpPost]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> NewPassword(NewPasswordVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
            {
                TempData["Error-Notification"] = "Your reset session has expired. Please start again.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(nameof(NewPasswordVM.Password), error.Description);
                }

                return View(model);
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            TempData["Success-Notification"] = "Your password was changed. You can log in with it now.";
            return RedirectToAction(nameof(Login));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Finds an account by e-mail (when the text contains an @) or by user name.</summary>
        private Task<ApplicationUser?> FindUserAsync(string userNameOrEmail)
        {
            var text = userNameOrEmail.Trim();

            return text.Contains('@')
                ? _userManager.FindByEmailAsync(text)
                : _userManager.FindByNameAsync(text);
        }

    }
}
