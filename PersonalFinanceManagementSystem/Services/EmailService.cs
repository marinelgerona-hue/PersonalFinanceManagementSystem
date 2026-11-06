using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace PersonalFinanceManagementSystem.Services;

public sealed class EmailService : IEmailService
{
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
        // Everything below comes from EmailSettings (bound to User Secrets).
        // No email address, password or SMTP credential is hardcoded here.

        var senderEmail = _settings.Email.Trim();
        var appPassword = string.Concat(
            _settings.AppPassword.Where(character => !char.IsWhiteSpace(character)));
        var smtpHost = NormalizeSmtpHost(
            string.IsNullOrWhiteSpace(_settings.SmtpServer)
                ? _settings.Host
                : _settings.SmtpServer);
        var port = _settings.Port;
        var recoveryEmail = _settings.RecoveryEmail.Trim();

        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException(
                "EmailSettings:Email is not configured in User Secrets.");
        if (string.IsNullOrWhiteSpace(appPassword))
            throw new InvalidOperationException(
                "EmailSettings:AppPassword is not configured in User Secrets.");
        if (string.IsNullOrWhiteSpace(smtpHost))
            throw new InvalidOperationException(
                "EmailSettings:SmtpServer is not configured in User Secrets.");
        if (port <= 0)
            throw new InvalidOperationException(
                "EmailSettings:Port is not configured in User Secrets.");
        if (string.IsNullOrWhiteSpace(recoveryEmail))
            throw new InvalidOperationException(
                "EmailSettings:RecoveryEmail is not configured in User Secrets.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, senderEmail));
        message.To.Add(MailboxAddress.Parse(recoveryEmail));
        message.Subject = "Your Finance Management verification code";
        message.Body = new TextPart("plain")
        {
            Text = $"Your password reset verification code for {accountEmail} is: {otp}\n\nThis code expires in 5 minutes. If you did not request this, you can safely ignore this email."
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(smtpHost, port, SecureSocketOptions.StartTls, cancellationToken);
        await smtp.AuthenticateAsync(senderEmail, appPassword, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }

    private static string NormalizeSmtpHost(string value)
    {
        var host = value.Trim();
        if (host.Length == 0) return string.Empty;

        if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        var separator = host.IndexOf(':');
        return separator >= 0 ? host[..separator].Trim() : host;
    }
}
