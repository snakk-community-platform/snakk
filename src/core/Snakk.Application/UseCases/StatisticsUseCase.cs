using Snakk.Application.Repositories;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.UseCases;

public class StatisticsUseCase(
    IPostRepository postRepo,
    IDiscussionRepository discussionRepo,
    IUserRepository userRepo,
    IStatsRepository statsRepo)
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

        var users = await userRepo.GetByPublicIdsAsync(userIds);
        var userDict = users.ToDictionary(u => u.PublicId.Value);

        var results = topContributors
            .Select(c => new TopContributorResult(
                UserId: c.UserId.Value,
                DisplayName: userDict.TryGetValue(c.UserId.Value, out var user) ? user.DisplayName : "Deleted User",
                AvatarFileName: userDict.TryGetValue(c.UserId.Value, out var u) ? u.AvatarFileName : null,
                PostCountToday: c.PostCount))
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
        string? userId = null)
    {
        var topDiscussions = await discussionRepo.GetTopActiveDiscussionsSinceAsync(
            since,
            hubId is not null ? HubId.From(hubId) : null,
            spaceId is not null ? SpaceId.From(spaceId) : null,
            communityId is not null ? CommunityId.From(communityId) : null,
            limit,
            userId);

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
        // Validate parameters
        if (days <= 0 || days > 90)
            days = 30;

        var userId = UserId.From(publicId);
        var user = await userRepo.GetByPublicIdAsync(userId);

        if (user is null)
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

        return Result<UserStatsDto>.Success(stats);
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
}

// Result DTOs
public record TopContributorResult(
    string UserId,
    string DisplayName,
    string? AvatarFileName,
    int PostCountToday);

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
    string? AuthorAvatarFileName = null);

public record UserActivityHistoryResult(
    int Days,
    IReadOnlyList<DailyActivityData> Data);

public record DailyActivityData(
    DateTime Date,
    int Discussions,
    int Posts,
    int Total);
