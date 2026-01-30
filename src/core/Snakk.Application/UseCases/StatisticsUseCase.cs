using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.UseCases;

public class StatisticsUseCase
{
    private readonly IPostRepository _postRepo;
    private readonly IDiscussionRepository _discussionRepo;
    private readonly IUserRepository _userRepo;

    public StatisticsUseCase(
        IPostRepository postRepo,
        IDiscussionRepository discussionRepo,
        IUserRepository userRepo)
    {
        _postRepo = postRepo;
        _discussionRepo = discussionRepo;
        _userRepo = userRepo;
    }

    /// <summary>
    /// Gets top contributors by post count for today
    /// </summary>
    public async Task<Result<PagedResult<TopContributorResult>>> GetTopContributorsTodayAsync(
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5)
    {
        var today = DateTime.UtcNow.Date;

        var topContributors = await _postRepo.GetTopContributorsSinceAsync(
            today,
            hubId != null ? HubId.From(hubId) : null,
            spaceId != null ? SpaceId.From(spaceId) : null,
            communityId != null ? CommunityId.From(communityId) : null,
            limit);

        // Batch load user details
        var userIds = topContributors.Select(c => c.UserId).ToList();
        var users = await _userRepo.GetByPublicIdsAsync(userIds);
        var userDict = users.ToDictionary(u => u.PublicId.Value);

        var results = topContributors.Select(c => new TopContributorResult(
            UserId: c.UserId.Value,
            DisplayName: userDict.TryGetValue(c.UserId.Value, out var user) ? user.DisplayName : "Deleted User",
            AvatarFileName: userDict.TryGetValue(c.UserId.Value, out var u) ? u.AvatarFileName : null,
            PostCountToday: c.PostCount
        )).ToList();

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
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        int limit = 5)
    {
        var today = DateTime.UtcNow.Date;

        var topDiscussions = await _discussionRepo.GetTopActiveDiscussionsSinceAsync(
            today,
            hubId != null ? HubId.From(hubId) : null,
            spaceId != null ? SpaceId.From(spaceId) : null,
            communityId != null ? CommunityId.From(communityId) : null,
            limit);

        var results = topDiscussions.Select(d => new TopDiscussionResult(
            DiscussionId: d.PublicId.Value,
            Title: d.Title,
            Slug: d.Slug,
            PostCountToday: d.PostCountToday,
            SpaceName: d.SpaceName,
            HubName: d.HubName
        )).ToList();

        return Result<PagedResult<TopDiscussionResult>>.Success(
            new PagedResult<TopDiscussionResult>
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
    int PostCountToday);

public record TopDiscussionResult(
    string DiscussionId,
    string Title,
    string Slug,
    int PostCountToday,
    string SpaceName,
    string HubName);
