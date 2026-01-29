namespace Snakk.Web.Services;

using System.Net.Http.Json;
using Snakk.Web.Models;

public class SnakkApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    // Community operations
    public async Task<PagedResult<CommunityDto>?> GetCommunitiesAsync(int offset = 0, int pageSize = 20)
    {
        return await _httpClient.GetFromJsonAsync<PagedResult<CommunityDto>>($"/communities?offset={offset}&pageSize={pageSize}");
    }

    public async Task<CommunityDetailDto?> GetCommunityBySlugAsync(string slug)
    {
        return await _httpClient.GetFromJsonAsync<CommunityDetailDto>($"/communities/by-slug/{slug}");
    }

    public async Task<CommunityDetailDto?> GetCommunityByDomainAsync(string domain)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CommunityDetailDto>($"/communities/by-domain/{Uri.EscapeDataString(domain)}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<PagedResult<HubDto>?> GetHubsByCommunityAsync(string communityId, int offset = 0, int pageSize = 20)
    {
        return await _httpClient.GetFromJsonAsync<PagedResult<HubDto>>($"/communities/{communityId}/hubs?offset={offset}&pageSize={pageSize}");
    }

    // Hub operations
    public async Task<PagedResult<HubDto>?> GetHubsAsync(int offset = 0, int pageSize = 20)
    {
        return await _httpClient.GetFromJsonAsync<PagedResult<HubDto>>($"/hubs?offset={offset}&pageSize={pageSize}");
    }

    public async Task<HubDetailDto?> GetHubBySlugAsync(string slug)
    {
        return await _httpClient.GetFromJsonAsync<HubDetailDto>($"/hubs/by-slug/{slug}");
    }

    // Space operations
    public async Task<PagedResult<SpaceDto>?> GetSpacesByHubAsync(string hubId, int offset = 0, int pageSize = 20)
    {
        return await _httpClient.GetFromJsonAsync<PagedResult<SpaceDto>>($"/hubs/{hubId}/spaces?offset={offset}&pageSize={pageSize}");
    }

    public async Task<SpaceDetailDto?> GetSpaceBySlugAsync(string slug)
    {
        return await _httpClient.GetFromJsonAsync<SpaceDetailDto>($"/spaces/by-slug/{slug}");
    }

    public async Task<PagedResult<DiscussionDto>?> GetDiscussionsBySpaceAsync(string spaceId, int offset = 0, int pageSize = 20)
    {
        return await _httpClient.GetFromJsonAsync<PagedResult<DiscussionDto>>($"/spaces/{spaceId}/discussions?offset={offset}&pageSize={pageSize}");
    }

    // Discussion operations
    public async Task<DiscussionDto?> GetDiscussionAsync(string publicId)
    {
        return await _httpClient.GetFromJsonAsync<DiscussionDto>($"/discussions/{publicId}");
    }

    public async Task<string?> CreateDiscussionAsync(CreateDiscussionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/discussions", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DiscussionDto>();
        return result?.PublicId;
    }

    // Post operations
    public async Task<PagedResult<PostDto>?> GetDiscussionPostsAsync(string discussionId, int offset = 0, int pageSize = 20)
    {
        return await _httpClient.GetFromJsonAsync<PagedResult<PostDto>>($"/discussions/{discussionId}/posts?offset={offset}&pageSize={pageSize}");
    }

    public async Task<string?> CreatePostAsync(CreatePostRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/posts", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PostDto>();
        return result?.PublicId;
    }

    // Read state operations
    public async Task<ReadStateDto?> GetReadStateAsync(string userId, string discussionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/read-states/user/{userId}/discussion/{discussionId}");
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<ReadStateDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<int> GetPostNumberAsync(string discussionId, string postId)
    {
        var response = await _httpClient.GetAsync($"/discussions/{discussionId}/posts/{postId}/number");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PostNumberResult>();
        return result?.PostNumber ?? 1;
    }

    // Recent discussions (for front page)
    public async Task<PagedResult<RecentDiscussionDto>?> GetRecentDiscussionsAsync(int offset = 0, int pageSize = 50, string? communityId = null, string? cursor = null)
    {
        var url = $"/discussions/recent?offset={offset}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(communityId))
            url += $"&communityId={communityId}";
        if (!string.IsNullOrEmpty(cursor))
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        return await _httpClient.GetFromJsonAsync<PagedResult<RecentDiscussionDto>>(url);
    }

    // Top active today (for sidebar)
    public async Task<TopActiveDiscussionsResult?> GetTopActiveDiscussionsTodayAsync(string? hubId = null, string? spaceId = null, string? communityId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId))
                queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(spaceId))
                queryParams.Add($"spaceId={spaceId}");
            else if (!string.IsNullOrEmpty(hubId))
                queryParams.Add($"hubId={hubId}");

            var url = queryParams.Count > 0
                ? $"/discussions/top-active-today?{string.Join("&", queryParams)}"
                : "/discussions/top-active-today";
            return await _httpClient.GetFromJsonAsync<TopActiveDiscussionsResult>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<TopActiveSpacesResult?> GetTopActiveSpacesTodayAsync(string? hubId = null, string? communityId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId))
                queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(hubId))
                queryParams.Add($"hubId={hubId}");

            var url = queryParams.Count > 0
                ? $"/spaces/top-active-today?{string.Join("&", queryParams)}"
                : "/spaces/top-active-today";
            return await _httpClient.GetFromJsonAsync<TopActiveSpacesResult>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<TopContributorsResult?> GetTopContributorsTodayAsync(string? hubId = null, string? spaceId = null, string? communityId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId))
                queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(spaceId))
                queryParams.Add($"spaceId={spaceId}");
            else if (!string.IsNullOrEmpty(hubId))
                queryParams.Add($"hubId={hubId}");

            var url = queryParams.Count > 0
                ? $"/api/users/top-contributors-today?{string.Join("&", queryParams)}"
                : "/api/users/top-contributors-today";
            return await _httpClient.GetFromJsonAsync<TopContributorsResult>(url);
        }
        catch
        {
            return null;
        }
    }

    // Stats operations
    public async Task<PlatformStatsDto?> GetPlatformStatsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PlatformStatsDto>("/api/platform/stats");
        }
        catch
        {
            return null;
        }
    }

    public async Task<HubStatsDto?> GetHubStatsAsync(string hubId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HubStatsDto>($"/api/hubs/{hubId}/stats");
        }
        catch
        {
            return null;
        }
    }

    public async Task<SpaceStatsDto?> GetSpaceStatsAsync(string spaceId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SpaceStatsDto>($"/api/spaces/{spaceId}/stats");
        }
        catch
        {
            return null;
        }
    }

    public async Task<CommunityStatsDto?> GetCommunityStatsAsync(string communityId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CommunityStatsDto>($"/api/communities/{communityId}/stats");
        }
        catch
        {
            return null;
        }
    }

    // Search operations
    public async Task<PagedResult<DiscussionSearchResultDto>?> SearchDiscussionsAsync(
        string? query = null,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"offset={offset}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrEmpty(query))
                queryParams.Add($"q={Uri.EscapeDataString(query)}");
            if (!string.IsNullOrEmpty(authorPublicId))
                queryParams.Add($"authorPublicId={authorPublicId}");
            if (!string.IsNullOrEmpty(spacePublicId))
                queryParams.Add($"spacePublicId={spacePublicId}");
            if (!string.IsNullOrEmpty(hubPublicId))
                queryParams.Add($"hubPublicId={hubPublicId}");

            var url = $"/api/search/discussions?{string.Join("&", queryParams)}";
            return await _httpClient.GetFromJsonAsync<PagedResult<DiscussionSearchResultDto>>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<PagedResult<PostSearchResultDto>?> SearchPostsAsync(
        string? query = null,
        string? authorPublicId = null,
        string? discussionPublicId = null,
        string? spacePublicId = null,
        int offset = 0,
        int pageSize = 20)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"offset={offset}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrEmpty(query))
                queryParams.Add($"q={Uri.EscapeDataString(query)}");
            if (!string.IsNullOrEmpty(authorPublicId))
                queryParams.Add($"authorPublicId={authorPublicId}");
            if (!string.IsNullOrEmpty(discussionPublicId))
                queryParams.Add($"discussionPublicId={discussionPublicId}");
            if (!string.IsNullOrEmpty(spacePublicId))
                queryParams.Add($"spacePublicId={spacePublicId}");

            var url = $"/api/search/posts?{string.Join("&", queryParams)}";
            return await _httpClient.GetFromJsonAsync<PagedResult<PostSearchResultDto>>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(string publicId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserProfileDto>($"/api/users/{publicId}/profile");
        }
        catch
        {
            return null;
        }
    }

    // Auth operations
    public async Task<AuthStatusDto?> GetAuthStatusAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AuthStatusDto>("/auth/status");
        }
        catch
        {
            return new AuthStatusDto(false, null, null, false);
        }
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CurrentUserDto>("/auth/me");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateProfileAsync(string displayName)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/auth/update-profile", new { displayName });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdatePreferencesAsync(bool preferEndlessScroll)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/auth/preferences", new { preferEndlessScroll });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Health check
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==================== Moderation ====================

    // Permission checks
    public async Task<bool> CanModerateAsync(string? communityId = null, string? hubId = null, string? spaceId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId)) queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(hubId)) queryParams.Add($"hubId={hubId}");
            if (!string.IsNullOrEmpty(spaceId)) queryParams.Add($"spaceId={spaceId}");

            var url = queryParams.Count > 0
                ? $"/api/moderation/can-moderate?{string.Join("&", queryParams)}"
                : "/api/moderation/can-moderate";

            var result = await _httpClient.GetFromJsonAsync<CanModerateResult>(url);
            return result?.CanModerate ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CanAdministerAsync(string? communityId = null, string? hubId = null, string? spaceId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId)) queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(hubId)) queryParams.Add($"hubId={hubId}");
            if (!string.IsNullOrEmpty(spaceId)) queryParams.Add($"spaceId={spaceId}");

            var url = queryParams.Count > 0
                ? $"/api/moderation/can-administer?{string.Join("&", queryParams)}"
                : "/api/moderation/can-administer";

            var result = await _httpClient.GetFromJsonAsync<CanAdministerResult>(url);
            return result?.CanAdminister ?? false;
        }
        catch
        {
            return false;
        }
    }

    // Role management
    public async Task<IEnumerable<UserRoleDto>?> GetMyRolesAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UserRolesResult>("/api/moderation/roles/me");
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<UserRoleDto>?> GetUserRolesAsync(string userId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UserRolesResult>($"/api/moderation/roles/user/{userId}");
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<UserRoleDto>?> GetRolesForCommunityAsync(string communityId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UserRolesResult>($"/api/moderation/roles/community/{communityId}");
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<UserRoleDto>?> GetRolesForHubAsync(string hubId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UserRolesResult>($"/api/moderation/roles/hub/{hubId}");
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<UserRoleDto>?> GetRolesForSpaceAsync(string spaceId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UserRolesResult>($"/api/moderation/roles/space/{spaceId}");
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserRoleDto?> AssignRoleAsync(AssignRoleRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/moderation/roles", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserRoleDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RevokeRoleAsync(string roleId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/moderation/roles/{roleId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Ban management
    public async Task<IEnumerable<UserBanDto>?> GetUserBansAsync(string userId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UserBansResult>($"/api/moderation/bans/user/{userId}");
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    public async Task<BanCheckResult?> CheckUserBanAsync(string userId, string? communityId = null, string? hubId = null, string? spaceId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId)) queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(hubId)) queryParams.Add($"hubId={hubId}");
            if (!string.IsNullOrEmpty(spaceId)) queryParams.Add($"spaceId={spaceId}");

            var url = queryParams.Count > 0
                ? $"/api/moderation/bans/check/{userId}?{string.Join("&", queryParams)}"
                : $"/api/moderation/bans/check/{userId}";

            return await _httpClient.GetFromJsonAsync<BanCheckResult>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserBanDto?> BanUserAsync(BanUserRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/moderation/bans", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserBanDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UnbanUserAsync(string banId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/moderation/bans/{banId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Report management
    public async Task<int> GetPendingReportCountAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<PendingReportCountResult>("/api/moderation/reports/pending-count");
            return result?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<PagedResult<ReportListDto>?> GetReportsAsync(string? status = null, int offset = 0, int pageSize = 20)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"offset={offset}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={status}");

            var url = $"/api/moderation/reports?{string.Join("&", queryParams)}";
            return await _httpClient.GetFromJsonAsync<PagedResult<ReportListDto>>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ReportDetailDto?> GetReportDetailAsync(string reportId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ReportDetailDto>($"/api/moderation/reports/{reportId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ReportDto?> CreateReportAsync(CreateReportRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/moderation/reports", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReportDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ResolveReportAsync(string reportId, ResolveReportRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/moderation/reports/{reportId}/resolve", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ReportCommentDto?> AddReportCommentAsync(string reportId, AddReportCommentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/moderation/reports/{reportId}/comments", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReportCommentDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<ReportReasonDto>?> GetReportReasonsAsync(string? communityId = null, string? hubId = null, string? spaceId = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(communityId)) queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(hubId)) queryParams.Add($"hubId={hubId}");
            if (!string.IsNullOrEmpty(spaceId)) queryParams.Add($"spaceId={spaceId}");

            var url = queryParams.Count > 0
                ? $"/api/moderation/report-reasons?{string.Join("&", queryParams)}"
                : "/api/moderation/report-reasons";

            var result = await _httpClient.GetFromJsonAsync<ReportReasonsResult>(url);
            return result?.Items;
        }
        catch
        {
            return null;
        }
    }

    // Moderation log
    public async Task<PagedResult<ModerationLogDto>?> GetModerationLogsAsync(
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        int offset = 0,
        int pageSize = 20)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"offset={offset}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrEmpty(communityId)) queryParams.Add($"communityId={communityId}");
            if (!string.IsNullOrEmpty(hubId)) queryParams.Add($"hubId={hubId}");
            if (!string.IsNullOrEmpty(spaceId)) queryParams.Add($"spaceId={spaceId}");

            var url = $"/api/moderation/logs?{string.Join("&", queryParams)}";
            return await _httpClient.GetFromJsonAsync<PagedResult<ModerationLogDto>>(url);
        }
        catch
        {
            return null;
        }
    }

    // Content moderation
    public async Task<bool> DeletePostAsync(string postId, string? reason = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/moderation/content/posts/{postId}/delete", new ModerateContentRequest(reason));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteDiscussionAsync(string discussionId, string? reason = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/moderation/content/discussions/{discussionId}/delete", new ModerateContentRequest(reason));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LockDiscussionAsync(string discussionId, string? reason = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/moderation/content/discussions/{discussionId}/lock", new ModerateContentRequest(reason));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnlockDiscussionAsync(string discussionId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/moderation/content/discussions/{discussionId}/unlock", new { });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Notifications
    public async Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int offset = 0, int pageSize = 10)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PagedResult<NotificationDto>>(
                $"/api/notifications?offset={offset}&pageSize={pageSize}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<UnreadCountDto?> GetUnreadNotificationCountAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UnreadCountDto>(
                $"/api/notifications/unread-count");
        }
        catch
        {
            return new UnreadCountDto(0);
        }
    }

    public async Task MarkNotificationAsReadAsync(string notificationId)
    {
        try
        {
            await _httpClient.PostAsync(
                $"/api/notifications/{notificationId}/read",
                null);
        }
        catch { }
    }

    public async Task MarkAllNotificationsAsReadAsync()
    {
        try
        {
            await _httpClient.PostAsync(
                $"/api/notifications/read-all",
                null);
        }
        catch { }
    }

    // Space follow
    public async Task<FollowStatusDto?> GetSpaceFollowStatusAsync(string spaceId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FollowStatusDto>(
                $"/api/spaces/{spaceId}/follow-status");
        }
        catch
        {
            return new FollowStatusDto(false, null);
        }
    }

    public async Task<FollowResultDto?> ToggleSpaceFollowAsync(string spaceId, string? level)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/spaces/{spaceId}/follow?level={level ?? "DiscussionsOnly"}",
                null);
            return await response.Content.ReadFromJsonAsync<FollowResultDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<FollowResultDto?> SetSpaceFollowLevelAsync(string spaceId, string level)
    {
        try
        {
            var response = await _httpClient.PutAsync(
                $"/api/spaces/{spaceId}/follow-level?level={level}",
                null);
            return await response.Content.ReadFromJsonAsync<FollowResultDto>();
        }
        catch
        {
            return null;
        }
    }

    // Discussion follow
    public async Task<FollowStatusDto?> GetDiscussionFollowStatusAsync(string discussionId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FollowStatusDto>(
                $"/api/discussions/{discussionId}/follow-status");
        }
        catch
        {
            return new FollowStatusDto(false, null);
        }
    }

    public async Task<FollowResultDto?> ToggleDiscussionFollowAsync(string discussionId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/discussions/{discussionId}/follow",
                null);
            return await response.Content.ReadFromJsonAsync<FollowResultDto>();
        }
        catch
        {
            return null;
        }
    }

    // Get all followed entities (for caching)
    public async Task<List<string>> GetFollowedSpacesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FollowedEntitiesResult>(
                $"/api/follows/spaces");
            return response?.PublicIds ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<List<string>> GetFollowedDiscussionsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FollowedEntitiesResult>(
                $"/api/follows/discussions");
            return response?.PublicIds ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<List<string>> GetFollowedUsersAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FollowedEntitiesResult>(
                $"/api/follows/users");
            return response?.PublicIds ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    // Batch update read states
    public async Task BatchUpdateReadStatesAsync(List<ReadStateUpdateDto> updates)
    {
        try
        {
            await _httpClient.PostAsJsonAsync(
                $"/api/read-states/batch",
                new { updates });
        }
        catch { }
    }

    public async Task MarkDiscussionAsReadAsync(string discussionId, string userId, string postId)
    {
        try
        {
            await _httpClient.PostAsync(
                $"/api/discussions/{discussionId}/mark-read?userId={userId}&postId={postId}",
                null);
        }
        catch { }
    }

    // Post reactions
    public async Task<Dictionary<string, int>?> GetPostReactionsAsync(string postId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Dictionary<string, int>>(
                $"/api/posts/{postId}/reactions");
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    public async Task<MyReactionDto?> GetMyPostReactionAsync(string postId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<MyReactionDto>(
                $"/api/posts/{postId}/reactions/me");
        }
        catch
        {
            return new MyReactionDto(null);
        }
    }

    public async Task TogglePostReactionAsync(string postId, string emoji)
    {
        try
        {
            await _httpClient.PostAsJsonAsync(
                $"/api/posts/{postId}/reactions",
                new { emoji });
        }
        catch { }
    }

    // Markup
    public async Task<string?> PreviewMarkupAsync(string content)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/markup/preview",
                new { content });
            var result = await response.Content.ReadFromJsonAsync<MarkupPreviewResult>();
            return result?.Html;
        }
        catch
        {
            return null;
        }
    }

    // Endless scroll
    public async Task<PagedResult<DiscussionDto>?> GetSpaceDiscussionsAsync(string spaceId, int offset, int pageSize)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PagedResult<DiscussionDto>>(
                $"/api/spaces/{spaceId}/discussions?offset={offset}&pageSize={pageSize}");
        }
        catch
        {
            return null;
        }
    }

    // Wrapper methods for backward compatibility
    public async Task<TopActiveDiscussionsResult?> GetTopActiveDiscussionsAsync(string? communityId = null)
    {
        return await GetTopActiveDiscussionsTodayAsync(communityId: communityId);
    }

    public async Task<TopActiveSpacesResult?> GetTopActiveSpacesAsync(string? communityId = null)
    {
        return await GetTopActiveSpacesTodayAsync(communityId: communityId);
    }

    public async Task<TopContributorsResult?> GetTopContributorsAsync(string? communityId = null)
    {
        return await GetTopContributorsTodayAsync(communityId: communityId);
    }
}

// Auth DTOs
public record AuthStatusDto(
    bool IsAuthenticated,
    string? PublicId,
    string? DisplayName,
    bool EmailVerified);

public record CurrentUserDto(
    string PublicId,
    string DisplayName,
    string? Email,
    bool EmailVerified,
    string? OAuthProvider,
    bool PreferEndlessScroll = true);

// Top active today DTOs
public record TopActiveDiscussionsResult(TopActiveDiscussionDto[] Items);
public record TopActiveDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    int PostCountToday,
    TopActiveEntityRef Space,
    TopActiveEntityRef Hub,
    TopActiveAuthorRef Author);

public record TopActiveAuthorRef(
    string PublicId,
    string DisplayName);

public record TopActiveSpacesResult(TopActiveSpaceDto[] Items);
public record TopActiveSpaceDto(
    string PublicId,
    string Name,
    string Slug,
    int PostCountToday,
    TopActiveEntityRef Hub);

public record TopActiveEntityRef(
    string PublicId,
    string Slug,
    string Name);

// Top contributors DTOs
public record TopContributorsResult(TopContributorDto[] Items);
public record TopContributorDto(
    string PublicId,
    string DisplayName,
    int PostCountToday);

// Stats DTOs
public record PlatformStatsDto(
    int HubCount,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);

public record HubStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);

public record SpaceStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int DiscussionCount,
    int ReplyCount,
    int FollowerCount);

public record CommunityStatsDto(
    string PublicId,
    string Name,
    string? Description,
    int HubCount,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);

// Read state DTOs
public record ReadStateDto(
    string UserId,
    string DiscussionId,
    string? LastReadPostId,
    DateTime LastReadAt);

public record PostNumberResult(int PostNumber);

// Notification DTOs
public record NotificationDto(
    string PublicId,
    string Type,
    string Title,
    string? Body,
    string? SourcePostId,
    string? SourceDiscussionId,
    string? ActorUserId,
    bool IsRead,
    DateTime CreatedAt);

public record UnreadCountDto(int Count);

// Follow DTOs
public record FollowStatusDto(bool IsFollowing, string? Level);
public record FollowResultDto(bool IsFollowing, string? Level);
public record FollowedEntitiesResult(List<string> PublicIds);

// Read state update DTO
public record ReadStateUpdateDto(string DiscussionId, string PostId, long Timestamp);

// Reaction DTOs
public record MyReactionDto(string? Emoji);

// Markup DTOs
public record MarkupPreviewResult(string Html);
