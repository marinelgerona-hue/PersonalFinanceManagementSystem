namespace PersonalFinanceManagementSystem.Models;

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
    /// <summary>Profile picture as a data URI (uploaded from Settings); null shows the initials avatar.</summary>
    public string? ProfileImage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>SHA-256 hash of the current "remember me" cookie token
    /// (Login page). The cookie itself never stores the hash — only the
    /// matching random token. Null = no remembered login.</summary>
    public string? RememberTokenHash { get; set; }

    /// <summary>When the remembered login expires; the cookie stops working after this.</summary>
    public DateTime? RememberTokenExpires { get; set; }
}
