namespace Snakk.Application.Services;

/// <summary>
/// Application-layer abstraction for sending emails.
/// Implementations can use SendGrid, AWS SES, SMTP, or console logging for development.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Send an email verification message
    /// </summary>
    Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationToken, string baseUrl);

    /// <summary>
    /// Send a password reset email
    /// </summary>
    Task SendPasswordResetAsync(string toEmail, string displayName, string resetToken, string baseUrl);

    /// <summary>
    /// Send a welcome email after successful registration
    /// </summary>
    Task SendWelcomeEmailAsync(string toEmail, string displayName);
}
