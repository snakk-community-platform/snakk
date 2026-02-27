using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class ConsoleEmailSenderTests
{
    private readonly Mock<ILogger<ConsoleEmailSender>> _mockLogger;
    private readonly ConsoleEmailSender _sender;

    public ConsoleEmailSenderTests()
    {
        _mockLogger = new Mock<ILogger<ConsoleEmailSender>>();
        _sender = new ConsoleEmailSender(_mockLogger.Object);
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
