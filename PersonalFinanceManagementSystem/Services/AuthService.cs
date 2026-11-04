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

    private static readonly TimeSpan PasswordResetDuration =
        TimeSpan.FromMinutes(15);

    private static readonly TimeSpan PasswordResetOtpDuration =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PasswordResetOtpRequestCooldown =
        TimeSpan.FromMinutes(1);

    private const int PasswordResetOtpMaxAttempts = 5;

    private readonly ApplicationDbContext _db;
    private readonly IActivityLogger _activity;

    public AuthService(ApplicationDbContext db, IActivityLogger activity)
    {
        _db = db;
        _activity = activity;
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

        if (remembered is null || !remembered.IsActive)
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
                model.FullPhone,

            Currency =
                model.Currency,

            // Public registration can only create regular user accounts.
            Role = UserRoles.User,

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

        _activity.Log(context, user.Id, user.FullName, user.Role, "Registration",
            $"New account registered ({user.Email}).");

        // Establish the same session used by a normal user sign-in so the new
        // account can go directly to the Finance Dashboard.
        context.Session.SetString(
            SessionKey,
            user.Id.ToString());

        return (
            true,
            string.Empty
        );
    }

    public (bool Success, string Error, string? Otp, string? RecipientEmail, int? UserId, string? ResetRole) CreatePasswordResetOtp(
        string email,
        string requestedRole)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = _db.Users.FirstOrDefault(u =>
            u.Email == normalizedEmail &&
            u.IsActive);

        if (user is null)
        {
            return (false, string.Empty, null, null, null, null);
        }

        var role = string.Equals(user.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? UserRoles.Admin
            : UserRoles.User;

        var now = DateTime.Now;
        if (user.PasswordResetOtpRequestedAt is DateTime requestedAt &&
            requestedAt.Add(PasswordResetOtpRequestCooldown) > now)
        {
            return (false, "Please wait before requesting another verification code.", null, null, user.Id, role);
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        user.PasswordResetOtpHash = TokenHash(otp);
        user.PasswordResetOtpExpires = now.Add(PasswordResetOtpDuration);
        user.PasswordResetOtpAttempts = 0;
        user.PasswordResetOtpRequestedAt = now;
        user.PasswordResetOtpVerified = false;
        user.PasswordResetRole = role;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpires = null;
        _db.SaveChanges();

        return (true, string.Empty, otp, user.Email, user.Id, role);
    }

    public void CancelPasswordResetOtp(string email, string requestedRole)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var role = string.Equals(
            requestedRole.Trim(), UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? UserRoles.Admin
            : UserRoles.User;
        var user = _db.Users.FirstOrDefault(u =>
            u.Email == normalizedEmail &&
            u.PasswordResetRole == role);

        if (user is null)
        {
            return;
        }

        ClearPasswordResetOtp(user);
        user.PasswordResetOtpRequestedAt = null;
        user.PasswordResetRole = null;
        _db.SaveChanges();
    }

    public (bool Success, string Error, string? Token) VerifyPasswordResetOtp(
        int userId,
        string requestedRole,
        string otp)
    {
        var isAdministratorRequest = string.Equals(
            requestedRole.Trim(), UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var role = isAdministratorRequest ? UserRoles.Admin : UserRoles.User;
        var user = _db.Users.FirstOrDefault(u =>
            u.Id == userId &&
            u.PasswordResetRole == role &&
            u.Role == role &&
            u.IsActive);

        if (user is null || user.PasswordResetOtpHash is null)
        {
            return (false, "The verification code is invalid or has expired.", null);
        }

        if (user.PasswordResetOtpExpires <= DateTime.Now)
        {
            ClearPasswordResetOtp(user);
            _db.SaveChanges();
            return (false, "The verification code is invalid or has expired.", null);
        }

        if (user.PasswordResetOtpAttempts >= PasswordResetOtpMaxAttempts)
        {
            return (false, "Too many incorrect attempts. Request a new verification code.", null);
        }

        if (!CryptographicEquals(user.PasswordResetOtpHash, TokenHash(otp.Trim())))
        {
            user.PasswordResetOtpAttempts++;
            _db.SaveChanges();
            return (false, user.PasswordResetOtpAttempts >= PasswordResetOtpMaxAttempts
                ? "Too many incorrect attempts. Request a new verification code."
                : "The verification code is invalid.", null);
        }

        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetTokenHash = TokenHash(resetToken);
        user.PasswordResetTokenExpires = DateTime.Now.Add(PasswordResetDuration);
        user.PasswordResetOtpVerified = true;
        ClearPasswordResetOtp(user, preserveVerified: true);
        _db.SaveChanges();

        return (true, string.Empty, resetToken);
    }

    private static bool CryptographicEquals(string expectedHash, string actualHash)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHash),
            Convert.FromHexString(actualHash));

    private static void ClearPasswordResetOtp(User user, bool preserveVerified = false)
    {
        user.PasswordResetOtpHash = null;
        user.PasswordResetOtpExpires = null;
        user.PasswordResetOtpAttempts = 0;
        if (!preserveVerified)
        {
            user.PasswordResetOtpVerified = false;
        }
    }

    public (bool Success, string Error, string? Role) ResetPassword(
        string token,
        string requestedRole,
        string newPassword,
        HttpContext context)
    {
        var tokenHash = TokenHash(token);
        var isAdministratorRequest =
            string.Equals(requestedRole.Trim(), UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var role = isAdministratorRequest ? UserRoles.Admin : UserRoles.User;
        var user = _db.Users.FirstOrDefault(u =>
            u.PasswordResetTokenHash == tokenHash &&
            u.PasswordResetTokenExpires > DateTime.Now &&
            u.PasswordResetOtpVerified &&
            u.PasswordResetRole == role &&
            u.Role.ToLower() == role.ToLower() &&
            u.IsActive);

        if (user is null)
        {
            return (false, "This password reset session is invalid or has expired.", null);
        }

        user.PasswordSalt = SecurityHelper.NewSalt();
        user.PasswordHash = SecurityHelper.Hash(newPassword, user.PasswordSalt);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpires = null;
        user.PasswordResetRole = null;
        user.PasswordResetOtpVerified = false;
        user.PasswordResetOtpRequestedAt = null;
        user.RememberTokenHash = null;
        user.RememberTokenExpires = null;
        _db.SaveChanges();

        context.Session.SetString(
            SessionKey,
            user.Id.ToString());

        return (true, "", isAdministratorRequest ? UserRoles.Admin : UserRoles.User);
    }

    // ------------------------------------------------------------
    // LOGIN
    // ------------------------------------------------------------

    public (bool Success, string Error) Login(
        string email,
        string password,
        string requestedRole,
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

        if (!string.Equals(user.Role, requestedRole, StringComparison.OrdinalIgnoreCase))
        {
            return (
                false,
                "This account is not authorized for the selected sign-in role."
            );
        }

        // --------------------------------------------------------
        // ACCOUNT ACTIVITY CHECK
        // --------------------------------------------------------

        // A deactivated account keeps its records but cannot sign in
        // until an admin re-activates it.
        if (!user.IsActive)
        {
            _activity.Log(context, user.Id, user.FullName, user.Role, "SigninBlocked",
                "Sign-in blocked: account is deactivated.");

            return (
                false,
                "This account has been deactivated. Contact an administrator for help."
            );
        }

        // --------------------------------------------------------
        // ACCOUNT ACTIVITY CHECK
        // --------------------------------------------------------

        // A deactivated account keeps its records but cannot sign in
        // until an admin re-activates it.
        if (!user.IsActive)
        {
            return (
                false,
                "This account has been deactivated. Contact an administrator for help."
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

        _activity.Log(context, user.Id, user.FullName, user.Role,
            user.Role == UserRoles.Admin ? "AdminSignin" : "Signin",
            $"{(rememberMe ? "Signed in (remember me)" : "Signed in")}.");

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

            _activity.Log(context, current.Id, current.FullName, current.Role,
                current.Role == UserRoles.Admin ? "AdminSignout" : "Signout",
                "Signed out.");
        }

        // Drop everything held in the session (user id, any per-page state) so
        // no part of the authenticated session survives the sign-out.
        context.Session.Clear();

        context.Response.Cookies.Delete(
            RememberCookieName,
            new CookieOptions { Path = "/" });
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