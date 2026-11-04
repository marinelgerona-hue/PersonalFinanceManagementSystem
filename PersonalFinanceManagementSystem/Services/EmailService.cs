using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace PersonalFinanceManagementSystem.Services;

public sealed class EmailService : IEmailService
{
    private const string RequiredSenderEmail = "marinelquimson@gmail.com";
    private const string RequiredRecoveryEmail = "marinelquimson@gmail.com";
    private const string RequiredSmtpHost = "smtp.gmail.com";

    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendPasswordResetOtpAsync(
        string accountEmail,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var senderEmail = _settings.Email.Trim();
        var appPassword = string.Concat(
            _settings.AppPassword.Where(character => !char.IsWhiteSpace(character)));
        var smtpHost = string.IsNullOrWhiteSpace(_settings.SmtpServer)
            ? _settings.Host.Trim()
            : _settings.SmtpServer.Trim();
        var recoveryEmail = _settings.RecoveryEmail.Trim();

        if (!string.Equals(senderEmail, RequiredSenderEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"EmailSettings:Email must be configured as {RequiredSenderEmail} in User Secrets.");
        if (string.IsNullOrWhiteSpace(appPassword))
            throw new InvalidOperationException("EmailSettings:AppPassword is not configured in User Secrets.");
        if (appPassword.Length != 16)
            throw new InvalidOperationException("EmailSettings:AppPassword must be a valid 16-character Gmail app password.");
        if (!string.Equals(recoveryEmail, RequiredRecoveryEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"EmailSettings:RecoveryEmail must be configured as {RequiredRecoveryEmail}.");
        if (!string.Equals(smtpHost, RequiredSmtpHost, StringComparison.OrdinalIgnoreCase) || _settings.Port != 587)
            throw new InvalidOperationException("Gmail SMTP must use smtp.gmail.com on port 587.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, senderEmail));
        message.To.Add(MailboxAddress.Parse(recoveryEmail));
        message.Subject = "Your Finance Management verification code";
        message.Body = new TextPart("plain")
        {
            Text = $"Your password reset verification code is: {otp}\n\nThis code expires in 5 minutes. If you did not request this, you can safely ignore this email."
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(smtpHost, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        await smtp.AuthenticateAsync(senderEmail, appPassword, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}
