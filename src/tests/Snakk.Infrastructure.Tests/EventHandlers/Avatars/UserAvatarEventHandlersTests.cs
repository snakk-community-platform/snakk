using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.EventHandlers.Avatars;

namespace Snakk.Infrastructure.Tests.EventHandlers.Avatars;

public class UserAvatarEventHandlersTests
{
    private readonly IAvatarGenerationService _avatarService = Substitute.For<IAvatarGenerationService>();
    private readonly ILogger<UserCreatedAvatarGenerationHandler> _creationLogger = Substitute.For<ILogger<UserCreatedAvatarGenerationHandler>>();
    private readonly ILogger<UserDeletedAvatarCleanupHandler> _deletionLogger = Substitute.For<ILogger<UserDeletedAvatarCleanupHandler>>();

    #region UserCreatedAvatarGenerationHandler Tests

    [Test]
    public async Task HandleAsync_GeneratesAvatar_WhenUserCreated()
    {
        // Arrange
        var userId = UserId.From("u_newuser");
        var @event = new UserCreatedEvent(userId);
        var handler = new UserCreatedAvatarGenerationHandler(_avatarService, _creationLogger);

        _avatarService.GenerateUserAvatarAsync(userId.Value, Arg.Any<int>())
            .Returns("/path/to/avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).GenerateUserAvatarAsync(userId.Value, Arg.Any<int>());
    }

    [Test]
    public async Task HandleAsync_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var userId = UserId.From("u_failuser");
        var @event = new UserCreatedEvent(userId);
        var handler = new UserCreatedAvatarGenerationHandler(_avatarService, _creationLogger);

        _avatarService.GenerateUserAvatarAsync(userId.Value, Arg.Any<int>())
            .Throws(new IOException("Disk full"));

        // Act & Assert
        var act = async () => await handler.HandleAsync(@event);
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task HandleAsync_LogsError_WhenGenerationFails()
    {
        // Arrange
        var userId = UserId.From("u_logerror");
        var @event = new UserCreatedEvent(userId);
        var handler = new UserCreatedAvatarGenerationHandler(_avatarService, _creationLogger);

        var exception = new IOException("Disk full");
        _avatarService.GenerateUserAvatarAsync(userId.Value, Arg.Any<int>())
            .Throws(exception);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _creationLogger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region UserDeletedAvatarCleanupHandler Tests

    [Test]
    public async Task HandleAsync_DeletesAvatar_WhenUserDeleted()
    {
        // Arrange
        var userId = UserId.From("u_deleteduser");
        var @event = new UserDeletedEvent(userId);
        var handler = new UserDeletedAvatarCleanupHandler(_avatarService, _deletionLogger);

        _avatarService.DeleteAvatarAsync("user", userId.Value)
            .Returns(Task.CompletedTask);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).DeleteAvatarAsync("user", userId.Value);
    }

    [Test]
    public async Task HandleAsync_DoesNotThrow_WhenDeletionFails()
    {
        // Arrange
        var userId = UserId.From("u_faildelete");
        var @event = new UserDeletedEvent(userId);
        var handler = new UserDeletedAvatarCleanupHandler(_avatarService, _deletionLogger);

        _avatarService.DeleteAvatarAsync("user", userId.Value)
            .Throws(new IOException("File locked"));

        // Act & Assert
        var act = async () => await handler.HandleAsync(@event);
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task HandleAsync_LogsError_WhenDeletionFails()
    {
        // Arrange
        var userId = UserId.From("u_logerrordelete");
        var @event = new UserDeletedEvent(userId);
        var handler = new UserDeletedAvatarCleanupHandler(_avatarService, _deletionLogger);

        var exception = new IOException("File locked");
        _avatarService.DeleteAvatarAsync("user", userId.Value)
            .Throws(exception);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _deletionLogger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}
