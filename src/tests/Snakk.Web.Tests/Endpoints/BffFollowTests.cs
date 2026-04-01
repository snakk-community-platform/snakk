using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using Snakk.Protos.Follow;
using Snakk.Web.Services;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests for BFF follow/unfollow endpoints:
///   GET  /bff/spaces/{spaceId}/follow-status
///   POST /bff/spaces/{spaceId}/follow
///   PUT  /bff/spaces/{spaceId}/follow-level
///   GET  /bff/discussions/{discussionId}/follow-status
///   POST /bff/discussions/{discussionId}/follow
///   GET  /bff/users/{userId}/follow-status
///   POST /bff/users/{userId}/follow
///   GET  /bff/follows/spaces
///   GET  /bff/follows/discussions
///   GET  /bff/follows/users
/// </summary>
public class BffFollowTests
{
    // ==================== Space Follow ====================

    [Test]
    public async Task GetSpaceFollowStatus_ReturnsFollowingStatus()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetSpaceFollowStatusAsync(Arg.Any<string>())
            .Returns(new SpaceFollowStatusResponse { IsFollowing = true, Level = "AllActivity" });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-001/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsTrue();
        await Assert.That(body.GetProperty("level").GetString()).IsEqualTo("AllActivity");
    }

    [Test]
    public async Task GetSpaceFollowStatus_WhenNotFollowing_ReturnsFalse()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetSpaceFollowStatusAsync(Arg.Any<string>())
            .Returns(new SpaceFollowStatusResponse { IsFollowing = false });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/spaces/space-002/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task ToggleSpaceFollow_ReturnsUpdatedFollowState()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .ToggleSpaceFollowResultAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(GrpcResult<SpaceFollowToggleResponse>.Ok(
                new SpaceFollowToggleResponse { IsFollowing = true, Level = "DiscussionsOnly" }));

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/spaces/space-001/follow", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsTrue();
        await Assert.That(body.GetProperty("level").GetString()).IsEqualTo("DiscussionsOnly");
    }

    [Test]
    public async Task ToggleSpaceFollow_WhenApiFails_ReturnsServiceUnavailable()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .ToggleSpaceFollowResultAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(GrpcResult<SpaceFollowToggleResponse>.ServerError());

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/spaces/space-001/follow", null);

        // Assert — MapGrpcError maps ServerError to 503
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task SetSpaceFollowLevel_UpdatesLevel()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .SetSpaceFollowLevelAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new FollowLevelResponse { Level = "AllActivity" });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PutAsync("/bff/spaces/space-001/follow-level?level=AllActivity", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("level").GetString()).IsEqualTo("AllActivity");
    }

    // ==================== Discussion Follow ====================

    [Test]
    public async Task GetDiscussionFollowStatus_ReturnsFollowingStatus()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetDiscussionFollowStatusAsync(Arg.Any<string>())
            .Returns(new FollowToggleResponse { IsFollowing = true });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/discussions/disc-001/follow-status");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ToggleDiscussionFollow_ReturnsUpdatedFollowState()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .ToggleDiscussionFollowResultAsync(Arg.Any<string>())
            .Returns(GrpcResult<FollowToggleResponse>.Ok(
                new FollowToggleResponse { IsFollowing = false }));

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/discussions/disc-001/follow", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsFalse();
    }

    // ==================== User Follow ====================

    [Test]
    public async Task GetUserFollowStatus_ReturnsFollowingStatus()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetUserFollowStatusAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new FollowToggleResponse { IsFollowing = true });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/users/user-002/follow-status?currentUserId=user-001");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ToggleUserFollow_ReturnsUpdatedFollowState()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .ToggleUserFollowResultAsync(Arg.Any<string>())
            .Returns(GrpcResult<FollowToggleResponse>.Ok(
                new FollowToggleResponse { IsFollowing = true }));

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/users/user-002/follow", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(body.GetProperty("isFollowing").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ToggleUserFollow_WhenApiFails_ReturnsServiceUnavailable()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .ToggleUserFollowResultAsync(Arg.Any<string>())
            .Returns(GrpcResult<FollowToggleResponse>.ServerError());

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.PostAsync("/bff/users/user-002/follow", null);

        // Assert — MapGrpcError maps ServerError to 503
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    // ==================== Follow Lists ====================

    [Test]
    public async Task GetFollowedSpaces_ReturnsListOfPublicIds()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetFollowedSpacesAsync()
            .Returns(new List<string> { "space-001", "space-002", "space-003" });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/follows/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(3);
    }

    [Test]
    public async Task GetFollowedDiscussions_ReturnsListOfPublicIds()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetFollowedDiscussionsAsync()
            .Returns(new List<string> { "disc-001", "disc-002" });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/follows/discussions");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task GetFollowedUsers_ReturnsListOfPublicIds()
    {
        // Arrange
        await using var app = new TestWebApp();
        app.MockApiClient
            .GetFollowedUsersAsync()
            .Returns(new List<string> { "user-001" });

        var client = TestJwtHelper.CreateAuthenticatedClient(app);

        // Act
        var response = await client.GetAsync("/bff/follows/users");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task GetFollowedSpaces_WhenNotAuthenticated_ReturnsEmptyList()
    {
        // Arrange
        await using var app = new TestWebApp();
        // Mock returns empty list (simulating unauthenticated user getting no results)
        app.MockApiClient
            .GetFollowedSpacesAsync()
            .Returns(new List<string>());

        var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/bff/follows/spaces");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        await Assert.That(items.GetArrayLength()).IsEqualTo(0);
    }
}
