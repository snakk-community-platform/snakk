using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Services;

namespace Snakk.Realtime.Tests.Hubs;

public class RealtimeHubTests : IDisposable
{
    private const string TestConnectionId = "test-connection-id";
    private const string TestUserId = "user-public-id-123";

    private readonly Mock<IAccessVerifier> _mockAccessVerifier;
    private readonly Mock<ILogger<RealtimeHub>> _mockLogger;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockCallerClient;
    private readonly Mock<IClientProxy> _mockGroupClient;
    private readonly RealtimeHub _hub;

    public RealtimeHubTests()
    {
        _mockAccessVerifier = new Mock<IAccessVerifier>();
        _mockLogger = new Mock<ILogger<RealtimeHub>>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockCallerClient = new Mock<ISingleClientProxy>();
        _mockGroupClient = new Mock<IClientProxy>();

        _mockContext.Setup(c => c.ConnectionId).Returns(TestConnectionId);
        _mockContext.Setup(c => c.UserIdentifier).Returns(TestUserId);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockGroupClient.Object);
        _mockClients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(_mockGroupClient.Object);

        // Default: access granted
        _mockAccessVerifier
            .Setup(v => v.VerifyDiscussionAccessAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockAccessVerifier
            .Setup(v => v.VerifySpaceAccessAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockAccessVerifier
            .Setup(v => v.VerifyHubAccessAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockClients.Setup(c => c.Caller).Returns(_mockCallerClient.Object);

        _hub = new RealtimeHub(_mockAccessVerifier.Object, _mockLogger.Object)
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
        var spacePublicId = "space-abc-123";

        // Act
        await _hub.SubscribeToSpace(spacePublicId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"space:{spacePublicId}", default),
            Times.Once);
    }

    [Test]
    public async Task SubscribeToSpace_WhenAccessDenied_DoesNotAddToGroup()
    {
        // Arrange
        var spacePublicId = "restricted-space";
        _mockAccessVerifier
            .Setup(v => v.VerifySpaceAccessAsync(TestUserId, spacePublicId))
            .ReturnsAsync(false);

        // Act
        await _hub.SubscribeToSpace(spacePublicId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    #endregion

    #region UnsubscribeFromSpace Tests

    [Test]
    public async Task UnsubscribeFromSpace_RemovesConnectionFromCorrectGroup()
    {
        // Arrange
        var spacePublicId = "space-abc-123";

        // Act
        await _hub.UnsubscribeFromSpace(spacePublicId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(TestConnectionId, $"space:{spacePublicId}", default),
            Times.Once);
    }

    #endregion

    #region SubscribeToHub Tests

    [Test]
    public async Task SubscribeToHub_AddsConnectionToCorrectGroup()
    {
        // Arrange
        var hubPublicId = "hub-public-id-456";

        // Act
        await _hub.SubscribeToHub(hubPublicId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"hub:{hubPublicId}", default),
            Times.Once);
    }

    #endregion

    #region UnsubscribeFromHub Tests

    [Test]
    public async Task UnsubscribeFromHub_RemovesConnectionFromCorrectGroup()
    {
        // Arrange
        var hubPublicId = "hub-public-id-456";

        // Act
        await _hub.UnsubscribeFromHub(hubPublicId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(TestConnectionId, $"hub:{hubPublicId}", default),
            Times.Once);
    }

    #endregion

    #region OnConnectedAsync Tests

    [Test]
    public async Task OnConnectedAsync_AutoSubscribesToUserGroup()
    {
        // Act
        await _hub.OnConnectedAsync();

        // Assert — auto-subscribed to user group server-side
        _mockGroups.Verify(
            g => g.AddToGroupAsync(TestConnectionId, $"user:{TestUserId}", default),
            Times.Once);
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
