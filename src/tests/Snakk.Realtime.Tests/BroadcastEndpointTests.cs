using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Models;
using System.Text.Json;

namespace Snakk.Realtime.Tests;

public class BroadcastEndpointTests
{
    private readonly IHubContext<RealtimeHub> _hubContext;
    private readonly IHubClients _clients;
    private readonly IClientProxy _clientProxy;

    public BroadcastEndpointTests()
    {
        _hubContext = Substitute.For<IHubContext<RealtimeHub>>();
        _clients = Substitute.For<IHubClients>();
        _clientProxy = Substitute.For<IClientProxy>();

        _clients.Group(Arg.Any<string>()).Returns(_clientProxy);

        _hubContext.Clients.Returns(_clients);
    }

    /// <summary>
    /// Extracts the Value property from an Ok&lt;T&gt; result via reflection,
    /// then serializes it to JSON for assertion.
    /// </summary>
    private static JsonElement GetOkResultValue(IResult result)
    {
        var resultType = result.GetType();
        var valueProperty = resultType.GetProperty("Value")
            ?? throw new InvalidOperationException($"Result type {resultType.Name} does not have a Value property");

        var value = valueProperty.GetValue(result)
            ?? throw new InvalidOperationException("Ok result value was null");

        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json).RootElement;
    }

    #region BroadcastEvent Tests

    [Test]
    public async Task BroadcastEvent_SendsReceiveUpdateToTargetGroup()
    {
        // Arrange
        var request = new BroadcastRequest
        {
            EventType = "new-post",
            TargetGroup = "discussion:disc-123",
            TargetId = "post-456",
            HtmlContent = "<div>New post content</div>",
            SwapStrategy = "beforeend",
            PostId = "post-456",
            Counts = new Dictionary<string, int> { ["replyCount"] = 5 }
        };

        // Act
        await BroadcastEndpoints.BroadcastEvent(request, _hubContext);

        // Assert
        _clients.Received(1).Group("discussion:disc-123");
        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveUpdate",
            Arg.Is<object?[]>(args => args.Length == 1),
            default);
    }

    [Test]
    public async Task BroadcastEvent_ReturnsOkWithSuccessAndTargetGroup()
    {
        // Arrange
        var request = new BroadcastRequest
        {
            EventType = "new-post",
            TargetGroup = "discussion:disc-123",
            TargetId = "post-456",
            HtmlContent = "<div>Content</div>",
            SwapStrategy = "beforeend"
        };

        // Act
        var result = await BroadcastEndpoints.BroadcastEvent(request, _hubContext);

        // Assert - verify it's an Ok result with success and targetGroup
        var value = GetOkResultValue(result);
        var success = value.GetProperty("success").GetBoolean();
        var targetGroup = value.GetProperty("targetGroup").GetString();
        await Assert.That(success).IsTrue();
        await Assert.That(targetGroup).IsEqualTo("discussion:disc-123");
    }

    [Test]
    public async Task BroadcastEvent_WithNullOptionalFields_SendsSuccessfully()
    {
        // Arrange
        var request = new BroadcastRequest
        {
            EventType = "update",
            TargetGroup = "global",
            TargetId = "target-1",
            HtmlContent = "<p>Updated</p>",
            SwapStrategy = "innerHTML",
            PostId = null,
            Counts = null
        };

        // Act
        var result = await BroadcastEndpoints.BroadcastEvent(request, _hubContext);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveUpdate",
            Arg.Any<object?[]>(),
            default);

        var value = GetOkResultValue(result);
        var success = value.GetProperty("success").GetBoolean();
        await Assert.That(success).IsTrue();
    }

    [Test]
    public async Task BroadcastEvent_UsesCorrectTargetGroup()
    {
        // Arrange
        var request = new BroadcastRequest
        {
            EventType = "reaction",
            TargetGroup = "space:tech:programming",
            TargetId = "post-789",
            HtmlContent = "",
            SwapStrategy = "none"
        };

        // Act
        await BroadcastEndpoints.BroadcastEvent(request, _hubContext);

        // Assert
        _clients.Received(1).Group("space:tech:programming");
    }

    #endregion

    #region BroadcastActivity Tests

    [Test]
    public async Task BroadcastActivity_SendsReceiveActivityToTargetGroup()
    {
        // Arrange
        var request = new ActivityBroadcastRequest
        {
            ActivityType = "user-registered",
            TargetGroup = "global",
            Data = new { userId = "user-123", displayName = "TestUser" }
        };

        // Act
        await BroadcastEndpoints.BroadcastActivity(request, _hubContext);

        // Assert
        _clients.Received(1).Group("global");
        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveActivity",
            Arg.Is<object?[]>(args => args.Length == 1),
            default);
    }

    [Test]
    public async Task BroadcastActivity_ReturnsOkWithSuccessAndTargetGroup()
    {
        // Arrange
        var request = new ActivityBroadcastRequest
        {
            ActivityType = "post-created",
            TargetGroup = "global",
            Data = new { postId = "post-123" }
        };

        // Act
        var result = await BroadcastEndpoints.BroadcastActivity(request, _hubContext);

        // Assert
        var value = GetOkResultValue(result);
        var success = value.GetProperty("success").GetBoolean();
        var targetGroup = value.GetProperty("targetGroup").GetString();
        await Assert.That(success).IsTrue();
        await Assert.That(targetGroup).IsEqualTo("global");
    }

    [Test]
    public async Task BroadcastActivity_UsesCorrectTargetGroup()
    {
        // Arrange
        var request = new ActivityBroadcastRequest
        {
            ActivityType = "moderation-action",
            TargetGroup = "user:admin-456",
            Data = new { action = "ban", targetUserId = "user-789" }
        };

        // Act
        await BroadcastEndpoints.BroadcastActivity(request, _hubContext);

        // Assert
        _clients.Received(1).Group("user:admin-456");
    }

    [Test]
    public async Task BroadcastActivity_WithComplexData_SendsSuccessfully()
    {
        // Arrange
        var request = new ActivityBroadcastRequest
        {
            ActivityType = "discussion-created",
            TargetGroup = "hub:technology",
            Data = new
            {
                discussionId = "disc-999",
                title = "New Discussion",
                authorName = "TestAuthor",
                spaceName = "General",
                timestamp = DateTime.UtcNow
            }
        };

        // Act
        var result = await BroadcastEndpoints.BroadcastActivity(request, _hubContext);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveActivity",
            Arg.Any<object?[]>(),
            default);

        var value = GetOkResultValue(result);
        var success = value.GetProperty("success").GetBoolean();
        await Assert.That(success).IsTrue();
    }

    #endregion
}
