namespace Snakk.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using Snakk.Application.Services;

/// <summary>
/// Development email sender that logs to console instead of sending real emails.
/// Replace with real implementation (SendGrid, AWS SES, SMTP) in production.
/// </summary>
public class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(
        string toEmail,
        string displayName,
        string verificationToken,
        string baseUrl,
        CancellationToken ct = default)
    {
        // Token is intentionally omitted from logs — use a real email provider in non-Development.
        logger.LogInformation(
            "DEV EMAIL: Verification email would be sent to {ToEmail} (token omitted from logs)",
            toEmail);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string displayName,
        string resetToken,
        string baseUrl,
        CancellationToken ct = default)
    {
        // Token is intentionally omitted from logs — use a real email provider in non-Development.
        logger.LogInformation(
            "DEV EMAIL: Password reset email would be sent to {ToEmail} (token omitted from logs)",
            toEmail);

        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string displayName, CancellationToken ct = default)
    {
        logger.LogInformation("""

            ================================================
            WELCOME EMAIL
            ================================================
            To: {ToEmail}
            Subject: Welcome to Snakk!

            Hi {DisplayName},

            Welcome to Snakk! Your account has been created successfully.

            You can now start participating in discussions.

            ================================================
            """, toEmail, displayName);

        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("""

            ================================================
            GENERIC EMAIL
            ================================================
            To: {ToEmail}
            Subject: {Subject}

            {Body}

            ================================================
            """, toEmail, subject, body);

        return Task.CompletedTask;
    }
}
