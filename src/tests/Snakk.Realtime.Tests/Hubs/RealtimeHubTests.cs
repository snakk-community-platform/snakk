using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Realtime.Hubs;

namespace Snakk.Realtime.Tests.Hubs;

public class RealtimeHubTests : IDisposable
{
    private const string TestConnectionId = "test-connection-id";

    private readonly Mock<ILogger<RealtimeHub>> _mockLogger;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly RealtimeHub _hub;

    public RealtimeHubTests()
    {
        _mockLogger = new Mock<ILogger<RealtimeHub>>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients>();

        _mockContext.Setup(c => c.ConnectionId).Returns(TestConnectionId);

        _hub = new RealtimeHub(_mockLogger.Object)
        {
            Groups = _mockGroups.Object,
            Context = _mockContext.Object,
            Clients = _mockClients.Object
        };
    }

    public void Dispose()
    {
        _hub.Dispose();
    }

    #region SubscribeToGlobal Tests

    [Test]
    public async Task SubscribeToGlobal_AddsConnectionToGlobalGroup()
    {
        // Act
        await _hub.SubscribeToGlobal();

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, "global", default),
            Times.Once);
    }

    #endregion

    #region SubscribeToDiscussion Tests

    [Test]
    public async Task SubscribeToDiscussion_AddsConnectionToCorrectGroup()
    {
        // Arrange
        var discussionId = "disc-123";

        // Act
        await _hub.SubscribeToDiscussion(discussionId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"discussion:{discussionId}", default),
            Times.Once);
    }

    [Test]
    public async Task SubscribeToDiscussion_WithDifferentId_UsesCorrectGroupName()
    {
        // Arrange
        var discussionId = "abc-def-ghi";

        // Act
        await _hub.SubscribeToDiscussion(discussionId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, "discussion:abc-def-ghi", default),
            Times.Once);
    }

    #endregion

    #region UnsubscribeFromDiscussion Tests

    [Test]
    public async Task UnsubscribeFromDiscussion_RemovesConnectionFromCorrectGroup()
    {
        // Arrange
        var discussionId = "disc-123";

        // Act
        await _hub.UnsubscribeFromDiscussion(discussionId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(TestConnectionId, $"discussion:{discussionId}", default),
            Times.Once);
    }

    #endregion

    #region SubscribeToSpace Tests

    [Test]
    public async Task SubscribeToSpace_AddsConnectionToCorrectGroup()
    {
        // Arrange
        var hubSlug = "tech";
        var spaceSlug = "programming";

        // Act
        await _hub.SubscribeToSpace(hubSlug, spaceSlug);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, "space:tech:programming", default),
            Times.Once);
    }

    #endregion

    #region UnsubscribeFromSpace Tests

    [Test]
    public async Task UnsubscribeFromSpace_RemovesConnectionFromCorrectGroup()
    {
        // Arrange
        var hubSlug = "tech";
        var spaceSlug = "programming";

        // Act
        await _hub.UnsubscribeFromSpace(hubSlug, spaceSlug);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(TestConnectionId, "space:tech:programming", default),
            Times.Once);
    }

    #endregion

    #region SubscribeToHub Tests

    [Test]
    public async Task SubscribeToHub_AddsConnectionToCorrectGroup()
    {
        // Arrange
        var hubSlug = "technology";

        // Act
        await _hub.SubscribeToHub(hubSlug);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, "hub:technology", default),
            Times.Once);
    }

    #endregion

    #region UnsubscribeFromHub Tests

    [Test]
    public async Task UnsubscribeFromHub_RemovesConnectionFromCorrectGroup()
    {
        // Arrange
        var hubSlug = "technology";

        // Act
        await _hub.UnsubscribeFromHub(hubSlug);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(TestConnectionId, "hub:technology", default),
            Times.Once);
    }

    #endregion

    #region SubscribeToUserNotifications Tests

    [Test]
    public async Task SubscribeToUserNotifications_AddsConnectionToCorrectGroup()
    {
        // Arrange
        var userId = "user-456";

        // Act
        await _hub.SubscribeToUserNotifications(userId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, "user:user-456", default),
            Times.Once);
    }

    #endregion

    #region OnConnectedAsync Tests

    [Test]
    public async Task OnConnectedAsync_CompletesSuccessfully()
    {
        // Act & Assert - should not throw
        await _hub.OnConnectedAsync();
    }

    #endregion

    #region OnDisconnectedAsync Tests

    [Test]
    public async Task OnDisconnectedAsync_WithNoException_CompletesSuccessfully()
    {
        // Act & Assert - should not throw
        await _hub.OnDisconnectedAsync(null);
    }

    [Test]
    public async Task OnDisconnectedAsync_WithException_CompletesSuccessfully()
    {
        // Arrange
        var exception = new InvalidOperationException("Test connection error");

        // Act & Assert - should not throw
        await _hub.OnDisconnectedAsync(exception);
    }

    #endregion
}
