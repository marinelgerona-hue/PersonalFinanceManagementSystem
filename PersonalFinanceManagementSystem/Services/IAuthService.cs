using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

public interface IAuthService
{
    /// <summary>Currently logged-in user for this browser session, or null.</summary>
    User? GetCurrentUser(HttpContext context);

    /// <summary>Creates a new account, signs the user in, and returns an error message on failure.</summary>
    (bool Success, string Error) Register(RegisterViewModel model, HttpContext context);

    /// <summary>Validates credentials and the requested account role, signs the user in, and returns an error message on failure.
    /// When rememberMe is true a persistent auth cookie is issued; otherwise the login
    /// is session-only (ends with the browser session).</summary>
    (bool Success, string Error) Login(string email, string password, string requestedRole, bool rememberMe, HttpContext context);

    /// <summary>Creates a role-bound 6-digit OTP password-reset request without revealing account existence.</summary>
    (bool Success, string Error, string? Otp, string? RecipientEmail, int? UserId, string? ResetRole) CreatePasswordResetOtp(string email, string requestedRole);

    /// <summary>Clears a password-reset OTP when email delivery fails.</summary>
    void CancelPasswordResetOtp(string email, string requestedRole);

    (bool Success, string Error, string? Token) VerifyPasswordResetOtp(int userId, string requestedRole, string otp);

    /// <summary>Validates and consumes a one-time password-reset token, then signs the user into the current session.</summary>
    (bool Success, string Error, string? Role) ResetPassword(string token, string requestedRole, string newPassword, HttpContext context);

    void Logout(HttpContext context);

    /// <summary>Updates basic profile info. Returns an error message on failure.</summary>
    string UpdateProfile(int userId, string fullName, string email, string phone, string currency);

    /// <summary>Changes the password after verifying the current one.</summary>
    bool ChangePassword(int userId, string currentPassword, string newPassword);

    /// <summary>Sets or clears (null) the user's profile picture data URI.</summary>
    void UpdateProfileImage(int userId, string? dataUri);

    User? GetUser(int userId);
}
