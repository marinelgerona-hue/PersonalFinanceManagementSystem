namespace PersonalFinanceManagementSystem.Services;

public interface IEmailService
{
    Task SendPasswordResetOtpAsync(string accountEmail, string otp, CancellationToken cancellationToken = default);
}
