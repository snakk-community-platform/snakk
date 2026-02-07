using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.EventHandlers.Avatars;

namespace Snakk.Infrastructure.Tests.EventHandlers.Avatars;

public class UserAvatarEventHandlersTests
{
    private readonly Mock<IAvatarGenerationService> _mockAvatarService;
    private readonly Mock<ILogger<UserCreatedAvatarGenerationHandler>> _mockCreationLogger;
    private readonly Mock<ILogger<UserDeletedAvatarCleanupHandler>> _mockDeletionLogger;

    public UserAvatarEventHandlersTests()
    {
        _mockAvatarService = new Mock<IAvatarGenerationService>();
        _mockCreationLogger = new Mock<ILogger<UserCreatedAvatarGenerationHandler>>();
        _mockDeletionLogger = new Mock<ILogger<UserDeletedAvatarCleanupHandler>>();
    }

    #region UserCreatedAvatarGenerationHandler Tests

    [Fact]
    public async Task HandleAsync_GeneratesAvatar_WhenUserCreated()
    {
        // Arrange
        var userId = UserId.From("u_newuser");
        var @event = new UserCreatedEvent(userId);
        var handler = new UserCreatedAvatarGenerationHandler(_mockAvatarService.Object, _mockCreationLogger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateUserAvatarAsync(userId.Value, It.IsAny<int>()))
            .ReturnsAsync("/path/to/avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.GenerateUserAvatarAsync(userId.Value, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var userId = UserId.From("u_failuser");
        var @event = new UserCreatedEvent(userId);
        var handler = new UserCreatedAvatarGenerationHandler(_mockAvatarService.Object, _mockCreationLogger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateUserAvatarAsync(userId.Value, It.IsAny<int>()))
            .ThrowsAsync(new IOException("Disk full"));

        // Act
        var act = async () => await handler.HandleAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_LogsError_WhenGenerationFails()
    {
        // Arrange
        var userId = UserId.From("u_logerror");
        var @event = new UserCreatedEvent(userId);
        var handler = new UserCreatedAvatarGenerationHandler(_mockAvatarService.Object, _mockCreationLogger.Object);

        var exception = new IOException("Disk full");
        _mockAvatarService
            .Setup(x => x.GenerateUserAvatarAsync(userId.Value, It.IsAny<int>()))
            .ThrowsAsync(exception);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockCreationLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UserDeletedAvatarCleanupHandler Tests

    [Fact]
    public async Task HandleAsync_DeletesAvatar_WhenUserDeleted()
    {
        // Arrange
        var userId = UserId.From("u_deleteduser");
        var @event = new UserDeletedEvent(userId);
        var handler = new UserDeletedAvatarCleanupHandler(_mockAvatarService.Object, _mockDeletionLogger.Object);

        _mockAvatarService
            .Setup(x => x.DeleteAvatarAsync("user", userId.Value))
            .Returns(Task.CompletedTask);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.DeleteAvatarAsync("user", userId.Value), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DoesNotThrow_WhenDeletionFails()
    {
        // Arrange
        var userId = UserId.From("u_faildelete");
        var @event = new UserDeletedEvent(userId);
        var handler = new UserDeletedAvatarCleanupHandler(_mockAvatarService.Object, _mockDeletionLogger.Object);

        _mockAvatarService
            .Setup(x => x.DeleteAvatarAsync("user", userId.Value))
            .ThrowsAsync(new IOException("File locked"));

        // Act
        var act = async () => await handler.HandleAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_LogsError_WhenDeletionFails()
    {
        // Arrange
        var userId = UserId.From("u_logerrordelete");
        var @event = new UserDeletedEvent(userId);
        var handler = new UserDeletedAvatarCleanupHandler(_mockAvatarService.Object, _mockDeletionLogger.Object);

        var exception = new IOException("File locked");
        _mockAvatarService
            .Setup(x => x.DeleteAvatarAsync("user", userId.Value))
            .ThrowsAsync(exception);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockDeletionLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
