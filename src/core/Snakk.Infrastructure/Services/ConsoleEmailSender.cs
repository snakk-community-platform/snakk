namespace Snakk.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using Snakk.Application.Services;

/// <summary>
/// Development email sender that logs to console instead of sending real emails.
/// Replace with real implementation (SendGrid, AWS SES, SMTP) in production.
/// </summary>
public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationToken, string baseUrl)
    {
        var verificationUrl = $"{baseUrl}/auth/verify-email?token={verificationToken}";

        _logger.LogInformation("""

            ================================================
            EMAIL VERIFICATION
            ================================================
            To: {ToEmail}
            Subject: Verify your Snakk account

            Hi {DisplayName},

            Please verify your email address by clicking the link below:
            {VerificationUrl}

            If you didn't create an account, you can safely ignore this email.

            ================================================
            """, toEmail, displayName, verificationUrl);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetToken, string baseUrl)
    {
        var resetUrl = $"{baseUrl}/auth/reset-password?token={resetToken}";

        _logger.LogInformation("""

            ================================================
            PASSWORD RESET
            ================================================
            To: {ToEmail}
            Subject: Reset your Snakk password

            Hi {DisplayName},

            Click the link below to reset your password:
            {ResetUrl}

            If you didn't request a password reset, you can safely ignore this email.

            ================================================
            """, toEmail, displayName, resetUrl);

        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string displayName)
    {
        _logger.LogInformation("""

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
}
