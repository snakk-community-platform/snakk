using System.Net;
using System.Text;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests that verify authenticated users can pass the auth check
/// on all auth-protected BFF endpoints. Each test sends a request WITH a valid
/// auth cookie and asserts the response is NOT 401 Unauthorized.
/// The actual response may be 200, 400, 404, or 503 depending on mock setup —
/// the point is proving the auth middleware accepts the token.
/// </summary>
[NotInParallel]
public class BffAuthenticatedAccessTests
{
    private TestWebApp _app = null!;
    private HttpClient _client = null!;

    [Before(Test)]
    public async Task SetUp()
    {
        _app = new TestWebApp();
        _client = TestJwtHelper.CreateAuthenticatedClient(_app);
    }

    [After(Test)]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    // ==================== Notifications ====================

    [Test]
    public async Task GetNotifications_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/notifications");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetNotificationsUnreadCount_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/notifications/unread-count");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MarkNotificationRead_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/notifications/test-id/read", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MarkAllNotificationsRead_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/notifications/read-all", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Space Follows ====================

    [Test]
    public async Task GetSpaceFollowStatus_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/follow-status");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task FollowSpace_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/spaces/test-id/follow", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdateSpaceFollowLevel_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/bff/spaces/test-id/follow-level", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Discussion Follows ====================

    [Test]
    public async Task GetDiscussionFollowStatus_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/follow-status");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task FollowDiscussion_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/follow", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task MarkDiscussionRead_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/mark-read", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Post Reactions ====================

    [Test]
    public async Task GetMyReaction_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/posts/test-id/reactions/me");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AddReaction_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/posts/test-id/reactions", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Markup ====================

    [Test]
    public async Task MarkupPreview_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"content\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/markup/preview", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Moderation ====================

    [Test]
    public async Task CreateReport_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent(
            "{\"targetType\":\"Post\",\"targetId\":\"test-id\",\"reason\":\"test\"}",
            Encoding.UTF8,
            "application/json");
        var response = await _client.PostAsync("/bff/moderation/reports", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Follows Lists ====================

    [Test]
    public async Task GetFollowedSpaces_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/follows/spaces");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFollowedDiscussions_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/follows/discussions");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFollowedUsers_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/follows/users");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Read States ====================

    [Test]
    public async Task BatchUpdateReadStates_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"updates\":[]}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/read-states/batch", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Auth ====================

    [Test]
    public async Task Logout_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/auth/logout", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Profile / Preferences ====================

    [Test]
    public async Task UpdateProfile_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"displayName\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/bff/me/profile", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task UpdatePreferences_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/bff/me/preferences", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetDisplayNameHistory_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/me/display-name-history");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== User Follows ====================

    [Test]
    public async Task GetUserFollowStatus_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/users/test-id/follow-status");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task FollowUser_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/users/test-id/follow", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Space Info ====================

    [Test]
    public async Task GetAllowedTypes_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/allowed-types?hubSlug=test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetSpaceInfo_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/info");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Post Editing ====================

    [Test]
    public async Task EditPost_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"content\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/posts/test-id/edit", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeletePost_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.DeleteAsync("/bff/posts/test-id");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetPostHistory_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/posts/test-id/history");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Discussion Type-Specific ====================

    [Test]
    public async Task SolveQuestion_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"postId\":\"test-id\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/discussions/test-id/question/solve", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SetDebatePosition_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"position\":\"for\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/discussions/test-id/debate/position", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AddJournalEntry_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"content\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/discussions/test-id/journal/entry", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task VoteOnPoll_WithAuthCookie_DoesNotReturn401()
    {
        var content = new StringContent("{\"optionId\":1}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/discussions/test-id/poll/vote", content);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task RemovePollVote_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.DeleteAsync("/bff/discussions/test-id/poll/vote?optionId=1");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    // ==================== Feed Token ====================

    [Test]
    public async Task CreateFeedToken_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.PostAsync("/bff/me/feed-token", null);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteFeedToken_WithAuthCookie_DoesNotReturn401()
    {
        var response = await _client.DeleteAsync("/bff/me/feed-token");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
