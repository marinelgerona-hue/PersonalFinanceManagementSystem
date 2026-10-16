using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

public interface IAuthService
{
    /// <summary>Currently logged-in user for this browser session, or null.</summary>
    User? GetCurrentUser(HttpContext context);

    /// <summary>Creates a new account, signs the user in, and returns an error message on failure.</summary>
    (bool Success, string Error) Register(RegisterViewModel model, HttpContext context);

    /// <summary>Validates credentials, signs the user in, and returns an error message on failure.
    /// When rememberMe is true a persistent auth cookie is issued; otherwise the login
    /// is session-only (ends with the browser session).</summary>
    (bool Success, string Error) Login(string email, string password, bool rememberMe, HttpContext context);

    void Logout(HttpContext context);

    /// <summary>Updates basic profile info. Returns an error message on failure.</summary>
    string UpdateProfile(int userId, string fullName, string email, string phone, string currency);

    /// <summary>Changes the password after verifying the current one.</summary>
    bool ChangePassword(int userId, string currentPassword, string newPassword);

    /// <summary>Sets or clears (null) the user's profile picture data URI.</summary>
    void UpdateProfileImage(int userId, string? dataUri);

    User? GetUser(int userId);
}
