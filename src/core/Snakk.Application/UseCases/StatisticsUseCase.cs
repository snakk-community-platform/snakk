using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.UseCases;

public class StatisticsUseCase(
    IPostRepository postRepo,
    IDiscussionRepository discussionRepo,
    IUserRepository userRepo,
    IStatsRepository statsRepo,
    IManageScopeDataService manageScopeData)
{
    /// <summary>
    /// Gets top contributors by post count for today
    /// </summary>
    public async Task<Result<PagedResult<TopContributorResult>>> GetTopContributorsTodayAsync(
        DateTime since,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5)
    {
        var topContributors = await postRepo.GetTopContributorsSinceAsync(
            since,
            hubId is not null ? HubId.From(hubId) : null,
            spaceId is not null ? SpaceId.From(spaceId) : null,
            communityId is not null ? CommunityId.From(communityId) : null,
            limit);

        // Batch load user details
        var userIds = topContributors
            .Select(c => c.UserId)
            .ToList();

        var users = await userRepo.GetAvatarSlimByPublicIdsAsync(userIds);
        var userDict = users.ToDictionary(u => u.PublicId);

        var results = topContributors
            .Select(c =>
            {
                userDict.TryGetValue(c.UserId.Value, out var u);
                return new TopContributorResult(
                    UserId: c.UserId.Value,
                    DisplayName: u?.DisplayName ?? "Deleted User",
                    AvatarFileName: u?.AvatarFileName,
                    AvatarThumbnailFileName: u?.AvatarThumbnailFileName,
                    AvatarMicroFileName: u?.AvatarMicroFileName,
                    AvatarRevision: u?.AvatarRevision ?? 0,
                    PostCountToday: c.PostCount,
                    Slug: u?.Slug);
            })
            .ToList();

        return Result<PagedResult<TopContributorResult>>.Success(
            new PagedResult<TopContributorResult>
            {
                Items = results,
                Offset = 0,
                PageSize = limit,
                HasMoreItems = false
            });
    }

    /// <summary>
    /// Gets top active discussions by post count for today
    /// </summary>
    public async Task<Result<PagedResult<TopDiscussionResult>>> GetTopActiveDiscussionsTodayAsync(
        DateTime since,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5,
        string? userId = null,
        bool viewerAllowsAdult = false)
    {
        var topDiscussions = await discussionRepo.GetTopActiveDiscussionsSinceAsync(
            since,
            hubId is not null ? HubId.From(hubId) : null,
            spaceId is not null ? SpaceId.From(spaceId) : null,
            communityId is not null ? CommunityId.From(communityId) : null,
            limit,
            userId,
            viewerAllowsAdult);

        var results = topDiscussions
            .Select(d => new TopDiscussionResult(
                DiscussionId: d.PublicId.Value,
                Title: d.Title,
                Slug: d.Slug,
                PostCountToday: d.PostCountToday,
                SpacePublicId: d.SpacePublicId,
                SpaceSlug: d.SpaceSlug,
                SpaceName: d.SpaceName,
                HubPublicId: d.HubPublicId,
                HubSlug: d.HubSlug,
                HubName: d.HubName,
                AuthorPublicId: d.AuthorPublicId,
                AuthorDisplayName: d.AuthorDisplayName,
                CommunitySlug: d.CommunitySlug,
                AuthorAvatarFileName: d.AuthorAvatarFileName))
            .ToList();

        return Result<PagedResult<TopDiscussionResult>>.Success(
            new PagedResult<TopDiscussionResult>
            {
                Items = results,
                Offset = 0,
                PageSize = limit,
                HasMoreItems = false
            });
    }

    /// <summary>
    /// Gets user activity history (discussions and posts) grouped by date
    /// </summary>
    public async Task<Result<UserActivityHistoryResult>> GetUserActivityHistoryAsync(
        string publicId,
        int days = 30)
    {
        // Validate parameters — 366 covers a full year heatmap.
        if (days <= 0 || days > 366)
            days = 30;

        var userId = UserId.From(publicId);
        var userExists = (await userRepo.GetAvatarSlimByPublicIdsAsync([userId])).Any();

        if (!userExists)
            return Result<UserActivityHistoryResult>.Failure("User not found");

        var startDate = DateTime.UtcNow.Date.AddDays(-days);

        // Get activity counts grouped by date
        var discussionActivity = await discussionRepo.GetActivityByDateAsync(userId, startDate);
        var postActivity = await postRepo.GetActivityByDateAsync(userId, startDate);

        // Create full date range and merge activity data
        var activityMap = new Dictionary<DateTime, (int Discussions, int Posts)>();

        for (var i = 0; i < days; i++)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            activityMap[date] = (0, 0);
        }

        foreach (var item in discussionActivity)
        {
            if (activityMap.ContainsKey(item.Date))
            {
                var current = activityMap[item.Date];
                activityMap[item.Date] = (item.Count, current.Posts);
            }
        }

        foreach (var item in postActivity)
        {
            if (activityMap.ContainsKey(item.Date))
            {
                var current = activityMap[item.Date];
                activityMap[item.Date] = (current.Discussions, item.Count);
            }
        }

        var dailyActivity = activityMap
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new DailyActivityData(
                Date: kvp.Key,
                Discussions: kvp.Value.Discussions,
                Posts: kvp.Value.Posts,
                Total: kvp.Value.Discussions + kvp.Value.Posts))
            .ToList();

        return Result<UserActivityHistoryResult>.Success(
            new UserActivityHistoryResult(Days: days, Data: dailyActivity));
    }

    /// <summary>
    /// Gets platform-wide statistics
    /// </summary>
    public async Task<PlatformStatsDto> GetPlatformStatsAsync() =>
        await statsRepo.GetPlatformStatsAsync();

    /// <summary>
    /// Gets statistics for a specific hub
    /// </summary>
    public async Task<Result<HubStatsDto>> GetHubStatsAsync(string publicId)
    {
        var stats = await statsRepo.GetHubStatsAsync(publicId);

        if (stats is null)
            return Result<HubStatsDto>.Failure("Hub not found");

        return Result<HubStatsDto>.Success(stats);
    }

    /// <summary>
    /// Gets statistics for a specific space
    /// </summary>
    public async Task<Result<SpaceStatsDto>> GetSpaceStatsAsync(string publicId)
    {
        var stats = await statsRepo.GetSpaceStatsAsync(publicId);

        if (stats is null)
            return Result<SpaceStatsDto>.Failure("Space not found");

        return Result<SpaceStatsDto>.Success(stats);
    }

    /// <summary>
    /// Gets statistics for a specific community
    /// </summary>
    public async Task<Result<CommunityStatsDto>> GetCommunityStatsAsync(string publicId)
    {
        var stats = await statsRepo.GetCommunityStatsAsync(publicId);

        if (stats is null)
            return Result<CommunityStatsDto>.Failure("Community not found");

        return Result<CommunityStatsDto>.Success(stats);
    }

    /// <summary>
    /// Gets statistics for a specific user
    /// </summary>
    public async Task<Result<UserStatsDto>> GetUserStatsAsync(string publicId)
    {
        var stats = await statsRepo.GetUserStatsAsync(publicId);

        if (stats is null)
            return Result<UserStatsDto>.Failure("User not found");

        var adminIds = await manageScopeData.GetGlobalAdminPublicIdsAsync();
        return Result<UserStatsDto>.Success(stats with { IsGlobalAdmin = adminIds.Contains(publicId) });
    }

    /// <summary>
    /// Gets statistics for a specific discussion
    /// </summary>
    public async Task<Result<DiscussionStatsDto>> GetDiscussionStatsAsync(string publicId)
    {
        var stats = await statsRepo.GetDiscussionStatsAsync(publicId);

        if (stats is null)
            return Result<DiscussionStatsDto>.Failure("Discussion not found");

        return Result<DiscussionStatsDto>.Success(stats);
    }

    /// <summary>
    /// Gets top active spaces by post count for today
    /// </summary>
    public async Task<List<TopActiveSpaceDto>> GetTopActiveSpacesTodayAsync(
        DateTime since,
        string? hubId = null,
        string? communityId = null,
        int limit = 5) =>
        await statsRepo.GetTopActiveSpacesSinceAsync(since, hubId, communityId, limit);

    /// <summary>
    /// Gets trending spaces by post count over a 7-day window
    /// </summary>
    public async Task<List<TopActiveSpaceDto>> GetTrendingSpacesAsync(
        DateTime since,
        string? hubId = null,
        string? communityId = null,
        int limit = 5) =>
        await statsRepo.GetTopActiveSpacesSinceAsync(since, hubId, communityId, limit);

    /// <summary>
    /// Gets top spaces by post count for a given time period
    /// </summary>
    public async Task<List<TopActiveSpaceDto>> GetTopSpacesByPeriodAsync(
        DateTime since,
        string? hubId = null,
        string? communityId = null,
        int limit = 5) =>
        await statsRepo.GetTopActiveSpacesSinceAsync(since, hubId, communityId, limit);

    /// <summary>
    /// Gets spaces ordered by most recent post
    /// </summary>
    public async Task<List<LatestActiveSpaceDto>> GetLatestActiveSpacesAsync(
        string? hubId = null,
        string? communityId = null,
        int limit = 5) =>
        await statsRepo.GetLatestActiveSpacesAsync(hubId, communityId, limit);

    /// <summary>
    /// Gets trending contributors by post count over a 7-day window
    /// </summary>
    public async Task<Result<PagedResult<TopContributorResult>>> GetTrendingContributorsAsync(
        DateTime since,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5) =>
        await GetTopContributorsTodayAsync(since, hubId, spaceId, communityId, limit);

    /// <summary>
    /// Gets top contributors by post count for a given time period
    /// </summary>
    public async Task<Result<PagedResult<TopContributorResult>>> GetTopContributorsByPeriodAsync(
        DateTime since,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5) =>
        await GetTopContributorsTodayAsync(since, hubId, spaceId, communityId, limit);

    /// <summary>
    /// Gets contributors ordered by most recent post
    /// </summary>
    public async Task<Result<PagedResult<LatestContributorResult>>> GetLatestContributorsAsync(
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5)
    {
        var latestContributors = await postRepo.GetLatestContributorsAsync(
            hubId is not null ? HubId.From(hubId) : null,
            spaceId is not null ? SpaceId.From(spaceId) : null,
            communityId is not null ? CommunityId.From(communityId) : null,
            limit);

        var userIds = latestContributors.Select(c => c.UserId).ToList();
        var users = await userRepo.GetAvatarSlimByPublicIdsAsync(userIds);
        var userDict = users.ToDictionary(u => u.PublicId);

        var results = latestContributors
            .Select(c =>
            {
                userDict.TryGetValue(c.UserId.Value, out var u);
                return new LatestContributorResult(
                    UserId: c.UserId.Value,
                    DisplayName: u?.DisplayName ?? "Deleted User",
                    AvatarFileName: u?.AvatarFileName,
                    AvatarThumbnailFileName: u?.AvatarThumbnailFileName,
                    AvatarMicroFileName: u?.AvatarMicroFileName,
                    AvatarRevision: u?.AvatarRevision ?? 0,
                    LastPostAt: c.LastPostAt,
                    Slug: u?.Slug);
            })
            .ToList();

        return Result<PagedResult<LatestContributorResult>>.Success(
            new PagedResult<LatestContributorResult>
            {
                Items = results,
                Offset = 0,
                PageSize = limit,
                HasMoreItems = false
            });
    }
}

// Result DTOs
public record TopContributorResult(
    string UserId,
    string DisplayName,
    string? AvatarFileName,
    string? AvatarThumbnailFileName,
    string? AvatarMicroFileName,
    int AvatarRevision,
    int PostCountToday,
    string? Slug = null);

public record TopDiscussionResult(
    string DiscussionId,
    string Title,
    string Slug,
    int PostCountToday,
    string SpacePublicId,
    string SpaceSlug,
    string SpaceName,
    string HubPublicId,
    string HubSlug,
    string HubName,
    string AuthorPublicId,
    string AuthorDisplayName,
    string CommunitySlug,
    string? AuthorAvatarFileName = null,
    string? AuthorAvatarThumbnailFileName = null);

public record LatestContributorResult(
    string UserId,
    string DisplayName,
    string? AvatarFileName,
    string? AvatarThumbnailFileName,
    string? AvatarMicroFileName,
    int AvatarRevision,
    DateTime LastPostAt,
    string? Slug = null);

public record UserActivityHistoryResult(
    int Days,
    IReadOnlyList<DailyActivityData> Data);

public record DailyActivityData(
    DateTime Date,
    int Discussions,
    int Posts,
    int Total);
