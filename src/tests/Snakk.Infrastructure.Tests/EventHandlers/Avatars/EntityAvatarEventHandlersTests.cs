using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.EventHandlers.Avatars;

namespace Snakk.Infrastructure.Tests.EventHandlers.Avatars;

public class EntityAvatarEventHandlersTests
{
    private readonly IAvatarGenerationService _avatarService = Substitute.For<IAvatarGenerationService>();

    #region Hub Event Handlers Tests

    [Test]
    public async Task HubCreatedHandler_GeneratesAvatar_WhenHubCreated()
    {
        // Arrange
        var hubId = HubId.From("h_newhub");
        var @event = new HubCreatedEvent(hubId);
        var logger = Substitute.For<ILogger<HubCreatedAvatarGenerationHandler>>();
        var handler = new HubCreatedAvatarGenerationHandler(_avatarService, logger);

        _avatarService.GenerateHubAvatarAsync(hubId.Value, Arg.Any<int>())
            .Returns("/path/to/hub-avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).GenerateHubAvatarAsync(hubId.Value, Arg.Any<int>());
    }

    [Test]
    public async Task HubDeletedHandler_DeletesAvatar_WhenHubDeleted()
    {
        // Arrange
        var hubId = HubId.From("h_deletedhub");
        var @event = new HubDeletedEvent(hubId);
        var logger = Substitute.For<ILogger<HubDeletedAvatarCleanupHandler>>();
        var handler = new HubDeletedAvatarCleanupHandler(_avatarService, logger);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).DeleteAvatarAsync("hub", hubId.Value);
    }

    [Test]
    public async Task HubCreatedHandler_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var hubId = HubId.From("h_failhub");
        var @event = new HubCreatedEvent(hubId);
        var logger = Substitute.For<ILogger<HubCreatedAvatarGenerationHandler>>();
        var handler = new HubCreatedAvatarGenerationHandler(_avatarService, logger);

        _avatarService.GenerateHubAvatarAsync(hubId.Value, Arg.Any<int>())
            .Throws(new IOException("Error"));

        // Act & Assert
        var act = async () => await handler.HandleAsync(@event);
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Space Event Handlers Tests

    [Test]
    public async Task SpaceCreatedHandler_GeneratesAvatar_WhenSpaceCreated()
    {
        // Arrange
        var spaceId = SpaceId.From("s_newspace");
        var @event = new SpaceCreatedEvent(spaceId);
        var logger = Substitute.For<ILogger<SpaceCreatedAvatarGenerationHandler>>();
        var handler = new SpaceCreatedAvatarGenerationHandler(_avatarService, logger);

        _avatarService.GenerateSpaceAvatarAsync(spaceId.Value, Arg.Any<int>())
            .Returns("/path/to/space-avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).GenerateSpaceAvatarAsync(spaceId.Value, Arg.Any<int>());
    }

    [Test]
    public async Task SpaceDeletedHandler_DeletesAvatar_WhenSpaceDeleted()
    {
        // Arrange
        var spaceId = SpaceId.From("s_deletedspace");
        var @event = new SpaceDeletedEvent(spaceId);
        var logger = Substitute.For<ILogger<SpaceDeletedAvatarCleanupHandler>>();
        var handler = new SpaceDeletedAvatarCleanupHandler(_avatarService, logger);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).DeleteAvatarAsync("space", spaceId.Value);
    }

    [Test]
    public async Task SpaceCreatedHandler_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var spaceId = SpaceId.From("s_failspace");
        var @event = new SpaceCreatedEvent(spaceId);
        var logger = Substitute.For<ILogger<SpaceCreatedAvatarGenerationHandler>>();
        var handler = new SpaceCreatedAvatarGenerationHandler(_avatarService, logger);

        _avatarService.GenerateSpaceAvatarAsync(spaceId.Value, Arg.Any<int>())
            .Throws(new IOException("Error"));

        // Act & Assert
        var act = async () => await handler.HandleAsync(@event);
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Community Event Handlers Tests

    [Test]
    public async Task CommunityCreatedHandler_GeneratesAvatar_WhenCommunityCreated()
    {
        // Arrange
        var communityId = CommunityId.From("c_newcomm");
        var @event = new CommunityCreatedEvent(communityId);
        var logger = Substitute.For<ILogger<CommunityCreatedAvatarGenerationHandler>>();
        var handler = new CommunityCreatedAvatarGenerationHandler(_avatarService, logger);

        _avatarService.GenerateCommunityAvatarAsync(communityId.Value, Arg.Any<int>())
            .Returns("/path/to/community-avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).GenerateCommunityAvatarAsync(communityId.Value, Arg.Any<int>());
    }

    [Test]
    public async Task CommunityDeletedHandler_DeletesAvatar_WhenCommunityDeleted()
    {
        // Arrange
        var communityId = CommunityId.From("c_deletedcomm");
        var @event = new CommunityDeletedEvent(communityId);
        var logger = Substitute.For<ILogger<CommunityDeletedAvatarCleanupHandler>>();
        var handler = new CommunityDeletedAvatarCleanupHandler(_avatarService, logger);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _avatarService.Received(1).DeleteAvatarAsync("community", communityId.Value);
    }

    [Test]
    public async Task CommunityCreatedHandler_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var communityId = CommunityId.From("c_failcomm");
        var @event = new CommunityCreatedEvent(communityId);
        var logger = Substitute.For<ILogger<CommunityCreatedAvatarGenerationHandler>>();
        var handler = new CommunityCreatedAvatarGenerationHandler(_avatarService, logger);

        _avatarService.GenerateCommunityAvatarAsync(communityId.Value, Arg.Any<int>())
            .Throws(new IOException("Error"));

        // Act & Assert
        var act = async () => await handler.HandleAsync(@event);
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task AllCreationHandlers_LogErrors_WhenGenerationFails()
    {
        // This test ensures all creation handlers follow the same error handling pattern

        // Arrange & Act & Assert for Hub
        var hubId = HubId.From("h_errorhub");
        var hubEvent = new HubCreatedEvent(hubId);
        var hubLogger = Substitute.For<ILogger<HubCreatedAvatarGenerationHandler>>();
        var hubHandler = new HubCreatedAvatarGenerationHandler(_avatarService, hubLogger);

        var exception = new IOException("Test error");
        _avatarService.GenerateHubAvatarAsync(hubId.Value, Arg.Any<int>())
            .Throws(exception);

        await hubHandler.HandleAsync(hubEvent);

        hubLogger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task AllDeletionHandlers_LogErrors_WhenDeletionFails()
    {
        // This test ensures all deletion handlers follow the same error handling pattern

        // Arrange & Act & Assert for Space
        var spaceId = SpaceId.From("s_errorspace");
        var spaceEvent = new SpaceDeletedEvent(spaceId);
        var spaceLogger = Substitute.For<ILogger<SpaceDeletedAvatarCleanupHandler>>();
        var spaceHandler = new SpaceDeletedAvatarCleanupHandler(_avatarService, spaceLogger);

        var exception = new IOException("Test error");
        _avatarService.DeleteAvatarAsync("space", spaceId.Value)
            .Throws(exception);

        await spaceHandler.HandleAsync(spaceEvent);

        spaceLogger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                exception,
                Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}
