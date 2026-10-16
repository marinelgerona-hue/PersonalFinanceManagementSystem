using System.Security.Cryptography;
using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// Authentication service backed by the MySQL database.
/// </summary>
public class AuthService : IAuthService
{
    private const string SessionKey =
        "FinanceManagement.UserId";

    // "Remember me": a secure, opaque token cookie that re-establishes the
    // session after a browser restart. Only a SHA-256 hash of the token is
    // stored server-side, never the token itself.
    private const string RememberCookieName =
        "FinanceManagement.RememberMe";

    private static readonly TimeSpan RememberDuration =
        TimeSpan.FromDays(30);

    private readonly ApplicationDbContext _db;

    public AuthService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------
    // GET CURRENTLY LOGGED-IN USER
    // ------------------------------------------------------------

    public User? GetCurrentUser(HttpContext context)
    {
        var raw =
            context.Session.GetString(SessionKey);

        if (int.TryParse(raw, out var userId))
        {
            return GetUser(userId);
        }

        // No session (expired browser session or restart): a "remember me"
        // token cookie silently restores the session, when present and
        // still valid. Unchecked logins behave exactly as before.
        var token =
            context.Request.Cookies[RememberCookieName];

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var remembered = _db.Users.FirstOrDefault(u =>
            u.RememberTokenHash == TokenHash(token) &&
            u.RememberTokenExpires > DateTime.Now);

        if (remembered is null)
        {
            return null;
        }

        context.Session.SetString(
            SessionKey,
            remembered.Id.ToString());

        return remembered;
    }

    // ------------------------------------------------------------
    // REGISTER
    // ------------------------------------------------------------

    public (bool Success, string Error) Register(
        RegisterViewModel model,
        HttpContext context)
    {
        // Normalize email
        var email =
            model.Email.Trim().ToLowerInvariant();

        // Check if email already exists
        var existingUser =
            _db.Users.FirstOrDefault(u =>
                u.Email.ToLower() == email);

        if (existingUser is not null)
        {
            return (
                false,
                "An account with this email already exists."
            );
        }

        // Create new user
        var user = new User
        {
            FullName =
                model.FullName.Trim(),

            Email =
                email,

            Phone =
                model.Phone?.Trim()
                ?? string.Empty,

            Currency =
                model.Currency,

            CreatedAt =
                DateTime.Now
        };

        // --------------------------------------------------------
        // HASH PASSWORD
        // --------------------------------------------------------

        user.PasswordSalt =
            SecurityHelper.NewSalt();

        user.PasswordHash =
            SecurityHelper.Hash(
                model.Password,
                user.PasswordSalt);

        // --------------------------------------------------------
        // SAVE USER TO MYSQL
        // --------------------------------------------------------

        _db.Users.Add(user);

        _db.SaveChanges();

        // IMPORTANT:
        // Do NOT create a session here.
        //
        // The user must log in after creating the account.

        return (
            true,
            string.Empty
        );
    }

    // ------------------------------------------------------------
    // LOGIN
    // ------------------------------------------------------------

    public (bool Success, string Error) Login(
        string email,
        string password,
        bool rememberMe,
        HttpContext context)
    {
        // Normalize email
        var normalizedEmail =
            email.Trim().ToLowerInvariant();

        // Find user in MySQL
        var user =
            _db.Users.FirstOrDefault(u =>
                u.Email.ToLower() ==
                normalizedEmail);

        // User doesn't exist
        if (user is null)
        {
            return (
                false,
                "Invalid email or password."
            );
        }

        // Verify password
        var validPassword =
            SecurityHelper.Verify(
                password,
                user.PasswordSalt,
                user.PasswordHash);

        // Password is incorrect
        if (!validPassword)
        {
            return (
                false,
                "Invalid email or password."
            );
        }

        // --------------------------------------------------------
        // LOGIN SUCCESSFUL
        // --------------------------------------------------------

        context.Session.SetString(
            SessionKey,
            user.Id.ToString());

        if (rememberMe)
        {
            IssueRememberCookie(user, context);
        }
        else
        {
            // Session-only login: remove any remembered token so a stale
            // cookie cannot outlive the user's choice.
            ClearRememberToken(user, context);
        }

        return (
            true,
            string.Empty
        );
    }

    // ------------------------------------------------------------
    // LOGOUT
    // ------------------------------------------------------------

    public void Logout(HttpContext context)
    {
        // Explicit logout also ends any "remember me" login.
        var current =
            GetCurrentUser(context);

        if (current is not null)
        {
            ClearRememberToken(current, context);
        }

        context.Session.Remove(SessionKey);

        context.Response.Cookies.Delete(RememberCookieName);
    }

    // ------------------------------------------------------------
    // "REMEMBER ME" HELPERS
    // ------------------------------------------------------------

    private static string TokenHash(string token)
        => Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token)));

    /// <summary>Generates a fresh random token, stores only its hash and
    /// expiry on the user row, and issues it as a secure auth cookie. The
    /// cookie never carries user data — just the opaque token.</summary>
    private void IssueRememberCookie(User user, HttpContext context)
    {
        var token =
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        user.RememberTokenHash =
            TokenHash(token);

        user.RememberTokenExpires =
            DateTime.Now.Add(RememberDuration);

        _db.SaveChanges();

        context.Response.Cookies.Append(RememberCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = RememberDuration,
            Path = "/"
        });
    }

    /// <summary>Invalidates the remembered login: wipes the stored token
    /// (with expiry) server-side and removes the cookie client-side.</summary>
    private void ClearRememberToken(User user, HttpContext context)
    {
        if (user.RememberTokenHash is null)
        {
            return;
        }

        user.RememberTokenHash = null;
        user.RememberTokenExpires = null;

        _db.SaveChanges();

        context.Response.Cookies.Delete(
            RememberCookieName,
            new CookieOptions { Path = "/" });
    }

    // ------------------------------------------------------------
    // UPDATE PROFILE
    // ------------------------------------------------------------

    public string UpdateProfile(
        int userId,
        string fullName,
        string email,
        string phone,
        string currency)
    {
        var normalizedEmail =
            email.Trim().ToLowerInvariant();

        // Find user
        var user =
            _db.Users.FirstOrDefault(u =>
                u.Id == userId);

        if (user is null)
        {
            return "Account not found.";
        }

        // Check whether another account
        // already uses this email
        var emailExists =
            _db.Users.Any(u =>
                u.Id != userId &&
                u.Email.ToLower() ==
                normalizedEmail);

        if (emailExists)
        {
            return
                "Another account already uses this email.";
        }

        // Update profile
        user.FullName =
            fullName.Trim();

        user.Email =
            normalizedEmail;

        user.Phone =
            phone?.Trim()
            ?? string.Empty;

        user.Currency =
            currency;

        // Save changes to MySQL
        _db.SaveChanges();

        return string.Empty;
    }

    // ------------------------------------------------------------
    // CHANGE PASSWORD
    // ------------------------------------------------------------

    public bool ChangePassword(
        int userId,
        string currentPassword,
        string newPassword)
    {
        // Find user in MySQL
        var user =
            _db.Users.FirstOrDefault(u =>
                u.Id == userId);

        if (user is null)
        {
            return false;
        }

        // Check current password
        var validPassword =
            SecurityHelper.Verify(
                currentPassword,
                user.PasswordSalt,
                user.PasswordHash);

        if (!validPassword)
        {
            return false;
        }

        // Create new salt
        user.PasswordSalt =
            SecurityHelper.NewSalt();

        // Create new password hash
        user.PasswordHash =
            SecurityHelper.Hash(
                newPassword,
                user.PasswordSalt);

        // Save new password to MySQL
        _db.SaveChanges();

        return true;
    }

    // ------------------------------------------------------------
    // UPDATE PROFILE IMAGE
    // ------------------------------------------------------------

    public void UpdateProfileImage(
        int userId,
        string? dataUri)
    {
        var user =
            _db.Users.FirstOrDefault(u =>
                u.Id == userId);

        if (user is null)
        {
            return;
        }

        user.ProfileImage =
            dataUri;

        // Save to MySQL
        _db.SaveChanges();
    }

    // ------------------------------------------------------------
    // GET USER
    // ------------------------------------------------------------

    public User? GetUser(int userId)
    {
        return _db.Users.FirstOrDefault(u =>
            u.Id == userId);
    }
}