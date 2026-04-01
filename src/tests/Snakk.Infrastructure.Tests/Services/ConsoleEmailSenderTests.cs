using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class ConsoleEmailSenderTests
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    private readonly ConsoleEmailSender _sender;

    public ConsoleEmailSenderTests()
    {
        _logger = Substitute.For<ILogger<ConsoleEmailSender>>();
        _sender = new ConsoleEmailSender(_logger);
    }

    [Test]
    public async Task SendEmailVerificationAsync_DoesNotThrow()
    {
        // Act
        var act = () => _sender.SendEmailVerificationAsync(
            "user@example.com",
            "Test User",
            "verification-token-123",
            "https://snakk.example.com");

        // Assert
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task SendPasswordResetAsync_DoesNotThrow()
    {
        // Act
        var act = () => _sender.SendPasswordResetAsync(
            "user@example.com",
            "Test User",
            "reset-token-456",
            "https://snakk.example.com");

        // Assert
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task SendWelcomeEmailAsync_DoesNotThrow()
    {
        // Act
        var act = () => _sender.SendWelcomeEmailAsync(
            "user@example.com",
            "Test User");

        // Assert
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task SendEmailAsync_DoesNotThrow()
    {
        // Act
        var act = () => _sender.SendEmailAsync(
            "user@example.com",
            "Test Subject",
            "This is the email body content.");

        // Assert
        await Assert.That(act).ThrowsNothing();
    }
}
