using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Models;
using System.Text.Json;

namespace Snakk.Realtime.Tests;

public class BroadcastEndpointTests
{
    private readonly Mock<IHubContext<RealtimeHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;

    public BroadcastEndpointTests()
    {
        _mockHubContext = new Mock<IHubContext<RealtimeHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(_mockClientProxy.Object);

        _mockHubContext
            .Setup(h => h.Clients)
            .Returns(_mockClients.Object);
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
        await BroadcastEndpoints.BroadcastEvent(request, _mockHubContext.Object);

        // Assert
        _mockClients.Verify(c => c.Group("discussion:disc-123"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "ReceiveUpdate",
                It.Is<object?[]>(args => args.Length == 1),
                default),
            Times.Once);
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
        var result = await BroadcastEndpoints.BroadcastEvent(request, _mockHubContext.Object);

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
        var result = await BroadcastEndpoints.BroadcastEvent(request, _mockHubContext.Object);

        // Assert
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "ReceiveUpdate",
                It.IsAny<object?[]>(),
                default),
            Times.Once);

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
        await BroadcastEndpoints.BroadcastEvent(request, _mockHubContext.Object);

        // Assert
        _mockClients.Verify(c => c.Group("space:tech:programming"), Times.Once);
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
        await BroadcastEndpoints.BroadcastActivity(request, _mockHubContext.Object);

        // Assert
        _mockClients.Verify(c => c.Group("global"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "ReceiveActivity",
                It.Is<object?[]>(args => args.Length == 1),
                default),
            Times.Once);
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
        var result = await BroadcastEndpoints.BroadcastActivity(request, _mockHubContext.Object);

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
        await BroadcastEndpoints.BroadcastActivity(request, _mockHubContext.Object);

        // Assert
        _mockClients.Verify(c => c.Group("user:admin-456"), Times.Once);
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
        var result = await BroadcastEndpoints.BroadcastActivity(request, _mockHubContext.Object);

        // Assert
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "ReceiveActivity",
                It.IsAny<object?[]>(),
                default),
            Times.Once);

        var value = GetOkResultValue(result);
        var success = value.GetProperty("success").GetBoolean();
        await Assert.That(success).IsTrue();
    }

    #endregion
}
