namespace PersonalFinanceManagementSystem.Services;

public sealed class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    // Current User Secrets names.
    public string Email { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
    public string RecoveryEmail { get; set; } = string.Empty;

    // Backward-compatible aliases for the previous configuration shape.
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Finance Management";
}
