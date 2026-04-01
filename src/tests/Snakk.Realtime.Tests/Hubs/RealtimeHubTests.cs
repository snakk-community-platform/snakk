using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Services;

namespace Snakk.Realtime.Tests.Hubs;

public class RealtimeHubTests : IDisposable
{
    private const string TestConnectionId = "test-connection-id";
    private const string TestUserId = "user-public-id-123";

    private readonly IAccessVerifier _accessVerifier;
    private readonly ILogger<RealtimeHub> _logger;
    private readonly IGroupManager _groups;
    private readonly HubCallerContext _context;
    private readonly IHubCallerClients _clients;
    private readonly ISingleClientProxy _callerClient;
    private readonly IClientProxy _groupClient;
    private readonly RealtimeHub _hub;

    public RealtimeHubTests()
    {
        _accessVerifier = Substitute.For<IAccessVerifier>();
        _logger = Substitute.For<ILogger<RealtimeHub>>();
        _groups = Substitute.For<IGroupManager>();
        _context = Substitute.For<HubCallerContext>();
        _clients = Substitute.For<IHubCallerClients>();
        _callerClient = Substitute.For<ISingleClientProxy>();
        _groupClient = Substitute.For<IClientProxy>();

        _context.ConnectionId.Returns(TestConnectionId);
        _context.UserIdentifier.Returns(TestUserId);
        _clients.Group(Arg.Any<string>()).Returns(_groupClient);
        _clients.OthersInGroup(Arg.Any<string>()).Returns(_groupClient);

        // Default: access granted
        _accessVerifier
            .VerifyDiscussionAccessAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        _accessVerifier
            .VerifySpaceAccessAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        _accessVerifier
            .VerifyHubAccessAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        _clients.Caller.Returns(_callerClient);

        _hub = new RealtimeHub(_accessVerifier, _logger)
        {
            Groups = _groups,
            Context = _context,
            Clients = _clients
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
        await _groups.Received(1).AddToGroupAsync(TestConnectionId, "global", default);
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
        await _groups.Received(1).AddToGroupAsync(TestConnectionId, $"discussion:{discussionId}", default);
    }

    [Test]
    public async Task SubscribeToDiscussion_WithDifferentId_UsesCorrectGroupName()
    {
        // Arrange
        var discussionId = "abc-def-ghi";

        // Act
        await _hub.SubscribeToDiscussion(discussionId);

        // Assert
        await _groups.Received(1).AddToGroupAsync(TestConnectionId, "discussion:abc-def-ghi", default);
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
        await _groups.Received(1).RemoveFromGroupAsync(TestConnectionId, $"discussion:{discussionId}", default);
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
        await _groups.Received(1).AddToGroupAsync(TestConnectionId, $"space:{spacePublicId}", default);
    }

    [Test]
    public async Task SubscribeToSpace_WhenAccessDenied_DoesNotAddToGroup()
    {
        // Arrange
        var spacePublicId = "restricted-space";
        _accessVerifier
            .VerifySpaceAccessAsync(TestUserId, spacePublicId)
            .Returns(false);

        // Act
        await _hub.SubscribeToSpace(spacePublicId);

        // Assert
        await _groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), default);
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
        await _groups.Received(1).RemoveFromGroupAsync(TestConnectionId, $"space:{spacePublicId}", default);
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
        await _groups.Received(1).AddToGroupAsync(TestConnectionId, $"hub:{hubPublicId}", default);
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
        await _groups.Received(1).RemoveFromGroupAsync(TestConnectionId, $"hub:{hubPublicId}", default);
    }

    #endregion

    #region OnConnectedAsync Tests

    [Test]
    public async Task OnConnectedAsync_AutoSubscribesToUserGroup()
    {
        // Act
        await _hub.OnConnectedAsync();

        // Assert — auto-subscribed to user group server-side
        await _groups.Received(1).AddToGroupAsync(TestConnectionId, $"user:{TestUserId}", default);
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
