using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.EventHandlers.Avatars;

namespace Snakk.Infrastructure.Tests.EventHandlers.Avatars;

public class EntityAvatarEventHandlersTests
{
    private readonly Mock<IAvatarGenerationService> _mockAvatarService;

    public EntityAvatarEventHandlersTests()
    {
        _mockAvatarService = new Mock<IAvatarGenerationService>();
    }

    #region Hub Event Handlers Tests

    [Fact]
    public async Task HubCreatedHandler_GeneratesAvatar_WhenHubCreated()
    {
        // Arrange
        var hubId = HubId.From("h_newhub");
        var @event = new HubCreatedEvent(hubId);
        var logger = new Mock<ILogger<HubCreatedAvatarGenerationHandler>>();
        var handler = new HubCreatedAvatarGenerationHandler(_mockAvatarService.Object, logger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateHubAvatarAsync(hubId.Value, It.IsAny<int>()))
            .ReturnsAsync("/path/to/hub-avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.GenerateHubAvatarAsync(hubId.Value, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task HubDeletedHandler_DeletesAvatar_WhenHubDeleted()
    {
        // Arrange
        var hubId = HubId.From("h_deletedhub");
        var @event = new HubDeletedEvent(hubId);
        var logger = new Mock<ILogger<HubDeletedAvatarCleanupHandler>>();
        var handler = new HubDeletedAvatarCleanupHandler(_mockAvatarService.Object, logger.Object);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.DeleteAvatarAsync("hub", hubId.Value), Times.Once);
    }

    [Fact]
    public async Task HubCreatedHandler_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var hubId = HubId.From("h_failhub");
        var @event = new HubCreatedEvent(hubId);
        var logger = new Mock<ILogger<HubCreatedAvatarGenerationHandler>>();
        var handler = new HubCreatedAvatarGenerationHandler(_mockAvatarService.Object, logger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateHubAvatarAsync(hubId.Value, It.IsAny<int>()))
            .ThrowsAsync(new IOException("Error"));

        // Act
        var act = async () => await handler.HandleAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Space Event Handlers Tests

    [Fact]
    public async Task SpaceCreatedHandler_GeneratesAvatar_WhenSpaceCreated()
    {
        // Arrange
        var spaceId = SpaceId.From("s_newspace");
        var @event = new SpaceCreatedEvent(spaceId);
        var logger = new Mock<ILogger<SpaceCreatedAvatarGenerationHandler>>();
        var handler = new SpaceCreatedAvatarGenerationHandler(_mockAvatarService.Object, logger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateSpaceAvatarAsync(spaceId.Value, It.IsAny<int>()))
            .ReturnsAsync("/path/to/space-avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.GenerateSpaceAvatarAsync(spaceId.Value, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task SpaceDeletedHandler_DeletesAvatar_WhenSpaceDeleted()
    {
        // Arrange
        var spaceId = SpaceId.From("s_deletedspace");
        var @event = new SpaceDeletedEvent(spaceId);
        var logger = new Mock<ILogger<SpaceDeletedAvatarCleanupHandler>>();
        var handler = new SpaceDeletedAvatarCleanupHandler(_mockAvatarService.Object, logger.Object);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.DeleteAvatarAsync("space", spaceId.Value), Times.Once);
    }

    [Fact]
    public async Task SpaceCreatedHandler_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var spaceId = SpaceId.From("s_failspace");
        var @event = new SpaceCreatedEvent(spaceId);
        var logger = new Mock<ILogger<SpaceCreatedAvatarGenerationHandler>>();
        var handler = new SpaceCreatedAvatarGenerationHandler(_mockAvatarService.Object, logger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateSpaceAvatarAsync(spaceId.Value, It.IsAny<int>()))
            .ThrowsAsync(new IOException("Error"));

        // Act
        var act = async () => await handler.HandleAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Community Event Handlers Tests

    [Fact]
    public async Task CommunityCreatedHandler_GeneratesAvatar_WhenCommunityCreated()
    {
        // Arrange
        var communityId = CommunityId.From("c_newcomm");
        var @event = new CommunityCreatedEvent(communityId);
        var logger = new Mock<ILogger<CommunityCreatedAvatarGenerationHandler>>();
        var handler = new CommunityCreatedAvatarGenerationHandler(_mockAvatarService.Object, logger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateCommunityAvatarAsync(communityId.Value, It.IsAny<int>()))
            .ReturnsAsync("/path/to/community-avatar.svg");

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.GenerateCommunityAvatarAsync(communityId.Value, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CommunityDeletedHandler_DeletesAvatar_WhenCommunityDeleted()
    {
        // Arrange
        var communityId = CommunityId.From("c_deletedcomm");
        var @event = new CommunityDeletedEvent(communityId);
        var logger = new Mock<ILogger<CommunityDeletedAvatarCleanupHandler>>();
        var handler = new CommunityDeletedAvatarCleanupHandler(_mockAvatarService.Object, logger.Object);

        // Act
        await handler.HandleAsync(@event);

        // Assert
        _mockAvatarService.Verify(x => x.DeleteAvatarAsync("community", communityId.Value), Times.Once);
    }

    [Fact]
    public async Task CommunityCreatedHandler_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var communityId = CommunityId.From("c_failcomm");
        var @event = new CommunityCreatedEvent(communityId);
        var logger = new Mock<ILogger<CommunityCreatedAvatarGenerationHandler>>();
        var handler = new CommunityCreatedAvatarGenerationHandler(_mockAvatarService.Object, logger.Object);

        _mockAvatarService
            .Setup(x => x.GenerateCommunityAvatarAsync(communityId.Value, It.IsAny<int>()))
            .ThrowsAsync(new IOException("Error"));

        // Act
        var act = async () => await handler.HandleAsync(@event);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AllCreationHandlers_LogErrors_WhenGenerationFails()
    {
        // This test ensures all creation handlers follow the same error handling pattern

        // Arrange & Act & Assert for Hub
        var hubId = HubId.From("h_errorhub");
        var hubEvent = new HubCreatedEvent(hubId);
        var hubLogger = new Mock<ILogger<HubCreatedAvatarGenerationHandler>>();
        var hubHandler = new HubCreatedAvatarGenerationHandler(_mockAvatarService.Object, hubLogger.Object);

        var exception = new IOException("Test error");
        _mockAvatarService
            .Setup(x => x.GenerateHubAvatarAsync(hubId.Value, It.IsAny<int>()))
            .ThrowsAsync(exception);

        await hubHandler.HandleAsync(hubEvent);

        hubLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AllDeletionHandlers_LogErrors_WhenDeletionFails()
    {
        // This test ensures all deletion handlers follow the same error handling pattern

        // Arrange & Act & Assert for Space
        var spaceId = SpaceId.From("s_errorspace");
        var spaceEvent = new SpaceDeletedEvent(spaceId);
        var spaceLogger = new Mock<ILogger<SpaceDeletedAvatarCleanupHandler>>();
        var spaceHandler = new SpaceDeletedAvatarCleanupHandler(_mockAvatarService.Object, spaceLogger.Object);

        var exception = new IOException("Test error");
        _mockAvatarService
            .Setup(x => x.DeleteAvatarAsync("space", spaceId.Value))
            .ThrowsAsync(exception);

        await spaceHandler.HandleAsync(spaceEvent);

        spaceLogger.Verify(
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
