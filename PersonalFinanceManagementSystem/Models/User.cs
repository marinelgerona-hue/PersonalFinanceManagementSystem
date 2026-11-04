namespace PersonalFinanceManagementSystem.Models;

/// <summary>Role names stored on <see cref="User.Role"/>.</summary>
public static class UserRoles
{
    public const string User = "User";
    public const string Admin = "Admin";
}

/// <summary>Application user account.</summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    /// <summary>Currency code used to format money for this user (USD, EUR, ...).</summary>
    public string Currency { get; set; } = "USD";
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>Access role: "User" (regular account, default) or "Admin" (can reach the Admin Console).</summary>
    public string Role { get; set; } = UserRoles.User;

    /// <summary>Whether the account can sign in. Deactivated accounts keep their records
    /// but cannot log in — an admin can reactivate them at any time.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Profile picture as a data URI (uploaded from Settings); null shows the initials avatar.</summary>
    public string? ProfileImage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>SHA-256 hash of the current "remember me" cookie token
    /// (Login page). The cookie itself never stores the hash — only the
    /// matching random token. Null = no remembered login.</summary>
    public string? RememberTokenHash { get; set; }

    /// <summary>When the remembered login expires; the cookie stops working after this.</summary>
    public DateTime? RememberTokenExpires { get; set; }

    /// <summary>SHA-256 hash of the current password-reset token. Null means no reset is pending.</summary>
    public string? PasswordResetTokenHash { get; set; }

    /// <summary>Expiration time for the password-reset token.</summary>
    public DateTime? PasswordResetTokenExpires { get; set; }

    /// <summary>Role selected when the reset request was made.</summary>
    public string? PasswordResetRole { get; set; }

    /// <summary>SHA-256 hash of the current six-digit password-reset OTP.</summary>
    public string? PasswordResetOtpHash { get; set; }

    /// <summary>When the current OTP expires.</summary>
    public DateTime? PasswordResetOtpExpires { get; set; }

    /// <summary>Number of incorrect OTP submissions for the current challenge.</summary>
    public int PasswordResetOtpAttempts { get; set; }

    /// <summary>When the current OTP was requested, used for request throttling.</summary>
    public DateTime? PasswordResetOtpRequestedAt { get; set; }

    /// <summary>Whether the OTP has been verified for this reset challenge.</summary>
    public bool PasswordResetOtpVerified { get; set; }
}
