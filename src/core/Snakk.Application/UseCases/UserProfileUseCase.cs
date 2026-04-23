namespace Snakk.Application.UseCases;

using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

public record UserProfileDto(
    string PublicId,
    string DisplayName,
    string? AvatarFileName,
    DateTime JoinedAt,
    DateTime? LastSeenAt,
    int DiscussionCount,
    int FollowerCount,
    int FollowingCount,
    int ReplyCount,
    int ReactionsReceived,
    string? Bio,
    IReadOnlyList<UserAchievementSummary> Achievements,
    IReadOnlyList<TopDiscussionByUser> TopDiscussions,
    IReadOnlyList<TopSpaceForUser> TopSpaces,
    string? AvatarThumbnailFileName = null);

public record UserAchievementSummary(
    string Slug,
    string Name,
    string Description,
    string? IconUrl,
    string Category,
    string Tier,
    int Points,
    DateTime EarnedAt);

public class UserProfileUseCase(
    IUserRepository userRepository,
    IFollowRepository followRepository,
    IReactionRepository reactionRepository,
    IAchievementRepository achievementRepository,
    IDiscussionRepository discussionRepository,
    IPostRepository postRepository,
    AchievementService achievementService) : UseCaseBase
{
    public async Task<UserProfileDto?> GetUserProfileAsync(string publicId)
    {
        var userId = UserId.From(publicId);
        var user = await userRepository.GetByPublicIdAsync(userId);

        if (user is null)
            return null;

        var followingCount = await followRepository.GetFollowingCountByUserAsync(userId);
        var reactionsReceived = await reactionRepository.GetTotalReactionsReceivedByUserAsync(userId);
        var topDiscussions = await discussionRepository.GetTopDiscussionsByUserAsync(userId, 5);
        var topSpaces = await postRepository.GetTopSpacesForUserAsync(userId, 5);

        var userAchievements = (await achievementService.GetDisplayedUserAchievementsAsync(userId)).ToList();
        var achievementsById = userAchievements.Count > 0
            ? (await achievementRepository.GetByIdsAsync(userAchievements.Select(ua => ua.AchievementId)))
                .ToDictionary(a => a.PublicId)
            : new Dictionary<AchievementId, Domain.Entities.Achievement>();

        var achievements = userAchievements
            .Where(ua => achievementsById.ContainsKey(ua.AchievementId))
            .OrderBy(ua => ua.DisplayOrder)
            .Select(ua =>
            {
                var a = achievementsById[ua.AchievementId];
                return new UserAchievementSummary(
                    a.Slug,
                    a.Name,
                    a.Description,
                    a.IconUrl,
                    a.Category.ToString(),
                    a.Tier.ToString(),
                    a.Points,
                    ua.EarnedAt);
            })
            .ToList();

        return new UserProfileDto(
            user.PublicId.Value,
            user.DisplayName ?? "",
            user.AvatarFileName,
            user.CreatedAt,
            user.LastSeenAt,
            user.DiscussionCount,
            user.FollowerCount,
            followingCount,
            user.ReplyCount,
            reactionsReceived,
            user.Bio,
            achievements,
            topDiscussions,
            topSpaces,
            user.AvatarThumbnailFileName);
    }
}
