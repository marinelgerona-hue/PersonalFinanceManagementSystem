using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;
using PersonalFinanceManagementSystem.Services;

namespace PersonalFinanceManagementSystem.Controllers;

/// <summary>
/// Create account, login and logout.
/// </summary>
public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly IEmailService _email;
    private readonly ILogger<AccountController> _logger;
    private readonly EmailSettings _emailSettings;

    public AccountController(
        IAuthService auth,
        IEmailService email,
        ILogger<AccountController> logger,
        IOptions<EmailSettings> emailSettings)
    {
        _auth = auth;
        _email = email;
        _logger = logger;
        _emailSettings = emailSettings.Value;
    }

    // ------------------------------------------------------------
    // LOGIN - GET
    // ------------------------------------------------------------

    [HttpGet]
    public IActionResult Login(string? role)
    {
        var requestedRole = NormalizeRole(role) ?? UserRoles.User;
        var currentUser = _auth.GetCurrentUser(HttpContext);

        // If the user is already logged in,
        // don't show the login page again.
        if (currentUser is not null)
        {
            return RedirectForUser(currentUser);
        }

        return View(new LoginViewModel { Role = requestedRole });
    }

    // ------------------------------------------------------------
    // LOGIN - POST
    // ------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        var requestedRole = NormalizeRole(model.Role);
        if (requestedRole is null)
        {
            ModelState.AddModelError(nameof(model.Role), "Please choose a valid sign-in role.");
        }
        else
        {
            model.Role = requestedRole;
        }

        // Check validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Check email and password from the database
        var (success, error) =
            _auth.Login(
                model.Email,
                model.Password,
                model.Role,
                model.RememberMe,
                HttpContext);

        // Login failed
        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                error);

            return View(model);
        }

        // Login successful. Redirect using the role read from the database,
        // not the role value posted by the browser.
        var signedIn = _auth.GetCurrentUser(HttpContext);
        return signedIn is null
            ? RedirectToAction("Index", "Dashboard")
            : RedirectForUser(signedIn);
    }

    // ------------------------------------------------------------
    // REGISTER / CREATE ACCOUNT - GET
    // ------------------------------------------------------------

    [HttpGet]
    public IActionResult Register()
    {
        // If already logged in, go to Dashboard
        if (_auth.GetCurrentUser(HttpContext) is User currentUser)
        {
            return RedirectForUser(currentUser);
        }

        return View(new RegisterViewModel());
    }

    // ------------------------------------------------------------
    // REGISTER / CREATE ACCOUNT - POST
    // ------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        // Check validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Create the account and save it to MySQL
        var (success, error) =
            _auth.Register(
                model,
                HttpContext);

        // Registration failed
        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                error);

            return View(model);
        }

        // Registration successful: AuthService has already established the
        // session for this newly-created regular user account.
        TempData["ToastTitle"] = "Account created";
        TempData["Success"] =
            "Welcome to Finance Management.";

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult ForgotPassword(string? role)
    {
        var requestedRole = NormalizeRole(role) ?? UserRoles.User;
        return View(new ForgotPasswordViewModel { Role = requestedRole });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        model.Role = NormalizeRole(model.Role) ?? UserRoles.User;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = _auth.CreatePasswordResetOtp(model.Email, model.Role);
        if (result.Success && result.Otp is not null)
        {
            try
            {
                await _email.SendPasswordResetOtpAsync(
                    result.RecipientEmail!,
                    result.Otp,
                    HttpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                _auth.CancelPasswordResetOtp(model.Email, result.ResetRole ?? model.Role);

                if (HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                {
                    _logger.LogError(
                        exception,
                        "Password reset email delivery failed for role {Role}. Exception type: {ExceptionType}.",
                        model.Role,
                        exception.GetType().Name);
                }

                ModelState.AddModelError(
                    string.Empty,
                    $"We couldn't send the password reset email: {exception.Message}");
                return View(model);
            }

            TempData["ToastTitle"] = "Email sent";
            TempData["Success"] = $"A verification code has been sent to {_emailSettings.RecoveryEmail.Trim()}. Check that email to continue.";
            return RedirectToAction(nameof(VerifyOtp), new
            {
                userId = result.UserId,
                email = result.RecipientEmail,
                role = result.ResetRole ?? model.Role
            });
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        // Keep the response generic when no active account matches the email.
        // Do not reveal whether the address exists.
        ViewData["ResetNotice"] =
            "If an account matches those details, a verification code may be sent shortly.";
        return View(model);
    }

    [HttpGet]
    public IActionResult VerifyOtp(int userId, string email, string? role)
    {
        var requestedRole = NormalizeRole(role) ?? UserRoles.User;
        // The OTP is delivered to the recovery email configured in User Secrets,
        // never to an address entered on the form.
        ViewData["RecoveryEmail"] = _emailSettings.RecoveryEmail.Trim();
        return View(new VerifyOtpViewModel
        {
            UserId = userId,
            Email = email.Trim(),
            Role = requestedRole
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyOtp(VerifyOtpViewModel model)
    {
        model.Role = NormalizeRole(model.Role) ?? UserRoles.User;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = _auth.VerifyPasswordResetOtp(model.UserId, model.Role, model.Otp);
        if (!result.Success || result.Token is null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        return RedirectToAction(
            nameof(ResetPassword),
            new { token = result.Token, role = model.Role });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(VerifyOtpViewModel model)
    {
        model.Role = NormalizeRole(model.Role) ?? UserRoles.User;
        if (model.UserId <= 0 || string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(string.Empty, "This verification session is invalid or has expired.");
            return View("VerifyOtp", model);
        }

        var result = _auth.CreatePasswordResetOtp(model.Email, model.Role);
        if (!result.Success || result.Otp is null || result.UserId != model.UserId)
        {
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrEmpty(result.Error)
                    ? "This verification session is invalid or has expired."
                    : result.Error);
            return View("VerifyOtp", model);
        }

        try
        {
            await _email.SendPasswordResetOtpAsync(
                result.RecipientEmail!,
                result.Otp,
                HttpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            _auth.CancelPasswordResetOtp(model.Email, model.Role);
            _logger.LogError(exception, "Password reset OTP resend failed for role {Role}.", model.Role);
            ModelState.AddModelError(string.Empty, $"We couldn't send the verification email: {exception.Message}");
            return View("VerifyOtp", model);
        }

        TempData["Success"] = $"A new verification code was sent to {_emailSettings.RecoveryEmail.Trim()}.";
        return RedirectToAction(nameof(VerifyOtp), new
        {
            userId = result.UserId,
            email = result.RecipientEmail,
            role = result.ResetRole ?? model.Role
        });
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token, string? role)
    {
        var requestedRole = NormalizeRole(role);
        if (string.IsNullOrWhiteSpace(token) || requestedRole is null)
        {
            TempData["Error"] = "This password reset session is invalid or has expired.";
            return RedirectToAction(nameof(Login), new { role = requestedRole ?? UserRoles.User });
        }

        return View(new ResetPasswordViewModel { Token = token, Role = requestedRole });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(ResetPasswordViewModel model)
    {
        model.Role = NormalizeRole(model.Role) ?? UserRoles.User;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = _auth.ResetPassword(
            model.Token,
            model.Role,
            model.NewPassword,
            HttpContext);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        TempData["ToastTitle"] = "Password updated";
        TempData["Success"] = "Your password was reset successfully. You are now signed in.";

        var signedIn = _auth.GetCurrentUser(HttpContext);
        return signedIn is null
            ? RedirectToAction(nameof(Login), new { role = result.Role ?? model.Role })
            : RedirectForUser(signedIn);
    }

    // ------------------------------------------------------------
    // LOGOUT
    // ------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _auth.Logout(HttpContext);

        TempData["ToastTitle"] = "Signed out";
        TempData["Success"] =
            "You've been signed out securely.";

        // Both users and administrators land on the public welcome page.
        return RedirectToAction("Index", "Home");
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.Equals(role, UserRoles.User, StringComparison.OrdinalIgnoreCase))
            return UserRoles.User;

        if (string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return UserRoles.Admin;

        return null;
    }

    private IActionResult RedirectForUser(User user)
        => user.Role == UserRoles.Admin
            ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
            : RedirectToAction("Index", "Dashboard");
}