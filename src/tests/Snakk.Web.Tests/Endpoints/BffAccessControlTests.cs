using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Snakk.Web.Tests.Helpers;

namespace Snakk.Web.Tests.Endpoints;

/// <summary>
/// Integration tests verifying access control on all BFF endpoints.
/// Group 1: Auth-protected endpoints must return 401 for unauthenticated requests.
/// Group 2: Public endpoints must NOT return 401 for unauthenticated requests.
/// </summary>
[NotInParallel]
public class BffAccessControlTests : IAsyncDisposable
{
    private readonly TestWebApp _app = new();
    private readonly HttpClient _client;

    public BffAccessControlTests()
    {
        _client = _app.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    // =====================================================================
    // Group 1: Auth-protected endpoints — unauthenticated requests → 401
    // =====================================================================

    [Test]
    public async Task Unauthenticated_GetNotifications_Returns401()
    {
        var response = await _client.GetAsync("/bff/notifications?offset=0&pageSize=10");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetNotificationsUnreadCount_Returns401()
    {
        var response = await _client.GetAsync("/bff/notifications/unread-count");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostNotificationRead_Returns401()
    {
        var response = await _client.PostAsync("/bff/notifications/test-id/read", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostNotificationsReadAll_Returns401()
    {
        var response = await _client.PostAsync("/bff/notifications/read-all", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetSpaceFollowStatus_Returns401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/follow-status");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostSpaceFollow_Returns401()
    {
        var response = await _client.PostAsync("/bff/spaces/test-id/follow", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PutSpaceFollowLevel_Returns401()
    {
        var response = await _client.PutAsync("/bff/spaces/test-id/follow-level?level=normal", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetDiscussionFollowStatus_Returns401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/follow-status");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostDiscussionFollow_Returns401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/follow", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostDiscussionMarkRead_Returns401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/mark-read?postId=x", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetPostReactionsMe_Returns401()
    {
        var response = await _client.GetAsync("/bff/posts/test-id/reactions/me");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostPostReaction_Returns401()
    {
        var content = new StringContent("{\"Type\":1}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/posts/test-id/reactions", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostMarkupPreview_Returns401()
    {
        var content = new StringContent("{\"markup\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/markup/preview", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostModerationReport_Returns401()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/moderation/reports", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetFollowsSpaces_Returns401()
    {
        var response = await _client.GetAsync("/bff/follows/spaces");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetFollowsDiscussions_Returns401()
    {
        var response = await _client.GetAsync("/bff/follows/discussions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetFollowsUsers_Returns401()
    {
        var response = await _client.GetAsync("/bff/follows/users");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostReadStatesBatch_Returns401()
    {
        var content = new StringContent("{\"discussionIds\":[]}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/read-states/batch", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostAuthLogout_Returns401()
    {
        var response = await _client.PostAsync("/bff/auth/logout", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PutMeProfile_Returns401()
    {
        var content = new StringContent("{\"displayName\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/bff/me/profile", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PutMePreferences_Returns401()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/bff/me/preferences", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetMeDisplayNameHistory_Returns401()
    {
        var response = await _client.GetAsync("/bff/me/display-name-history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetUserFollowStatus_Returns401()
    {
        var response = await _client.GetAsync("/bff/users/test-id/follow-status");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostUserFollow_Returns401()
    {
        var response = await _client.PostAsync("/bff/users/test-id/follow", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetSpaceAllowedTypes_Returns401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/allowed-types?hubSlug=test");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetSpaceInfo_Returns401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/info");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostPostEdit_Returns401()
    {
        var content = new StringContent("{\"Content\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/bff/posts/test-id/edit", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_DeletePost_Returns401()
    {
        var response = await _client.DeleteAsync("/bff/posts/test-id");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetPostHistory_Returns401()
    {
        var response = await _client.GetAsync("/bff/posts/test-id/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostMediaUpload_Returns401()
    {
        var fileBytes = new ByteArrayContent([0x89, 0x50]);
        fileBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var content = new MultipartFormDataContent();
        content.Add(fileBytes, "file", "test.png");
        var response = await _client.PostAsync("/bff/media/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_DeleteMediaDraft_Returns401()
    {
        var response = await _client.DeleteAsync("/bff/media/draft?url=test");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostAvatarUpload_Returns401()
    {
        var fileBytes = new ByteArrayContent([0x89, 0x50]);
        fileBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var content = new MultipartFormDataContent();
        content.Add(fileBytes, "avatar", "avatar.png");
        var response = await _client.PostAsync("/bff/avatars/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_DeleteAvatar_Returns401()
    {
        var response = await _client.DeleteAsync("/bff/avatars");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostQuestionSolve_Returns401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/question/solve?postPublicId=x", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostDebatePosition_Returns401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/debate/position?postPublicId=x&positionId=1", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostJournalEntry_Returns401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/journal/entry?postPublicId=x", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostPollVote_Returns401()
    {
        var response = await _client.PostAsync("/bff/discussions/test-id/poll/vote?optionId=1", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_DeletePollVote_Returns401()
    {
        var response = await _client.DeleteAsync("/bff/discussions/test-id/poll/vote?optionId=1");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_GetMeDevices_Returns401()
    {
        var response = await _client.GetAsync("/bff/me/devices");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_DeleteMeDevice_Returns401()
    {
        var response = await _client.DeleteAsync("/bff/me/devices/test-id");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_PostMeFeedToken_Returns401()
    {
        var response = await _client.PostAsync("/bff/me/feed-token", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Unauthenticated_DeleteMeFeedToken_Returns401()
    {
        var response = await _client.DeleteAsync("/bff/me/feed-token");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // =====================================================================
    // Group 2: Public endpoints — unauthenticated requests → NOT 401
    // =====================================================================

    [Test]
    public async Task Public_GetDiscussionsRecent_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/recent");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetSpaceDiscussions_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/discussions");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetPostReactions_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/posts/test-id/reactions");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetUserStats_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/users/test-id/stats");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetUserActivityHistory_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/users/test-id/activity-history");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_SearchDiscussions_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/search/discussions?q=test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_SearchPosts_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/search/posts?q=test");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_SearchSpaces_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/search/spaces");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionPosts_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/posts?offset=0&pageSize=10");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionPreview_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/preview");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetHubStats_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/hubs/test-id/stats");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetSpaceStats_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/spaces/test-id/stats");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetCommunityStats_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/communities/test-id/stats");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetUserStatsPopup_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/users/test-id/stats-popup");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionStats_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/stats");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetModerationReportReasons_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/moderation/reports/reasons");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionQuestion_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/question");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionDebate_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/debate");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionLink_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/link");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_GetDiscussionPoll_DoesNotReturn401()
    {
        var response = await _client.GetAsync("/bff/discussions/test-id/poll");
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }
}
