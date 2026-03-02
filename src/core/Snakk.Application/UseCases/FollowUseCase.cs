namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;
using Snakk.Application.Services;

public class FollowUseCase(
    IFollowRepository followRepository,
    IDiscussionRepository discussionRepository,
    ISpaceRepository spaceRepository,
    IUserRepository userRepository,
    ICounterService counterService)
{
    /// <summary>
    /// Toggle follow state for a discussion.
    /// </summary>
    /// <returns>True if now following, false if unfollowed</returns>
    public async Task<Result<bool>> ToggleFollowDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        var discussion = await discussionRepository.GetByPublicIdAsync(discussionId);

        if (discussion is null)
            return Result<bool>.Failure("Discussion not found");

        var existingFollow = await followRepository.GetByUserAndDiscussionAsync(userId, discussionId);

        if (existingFollow is not null)
        {
            // Unfollow
            existingFollow.MarkForRemoval();
            await followRepository.DeleteAsync(existingFollow);
            await counterService.DecrementDiscussionFollowerCountAsync(discussionId);

            return Result<bool>.Success(false);
        }

        // Follow
        var follow = Follow.CreateForDiscussion(userId, discussionId);
        await followRepository.AddAsync(follow);
        await counterService.IncrementDiscussionFollowerCountAsync(discussionId);

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Toggle follow state for a space.
    /// </summary>
    /// <returns>True if now following, false if unfollowed</returns>
    public async Task<Result<bool>> ToggleFollowSpaceAsync(UserId userId, SpaceId spaceId, FollowLevel level = FollowLevel.DiscussionsOnly)
    {
        var space = await spaceRepository.GetByPublicIdAsync(spaceId);

        if (space is null)
            return Result<bool>.Failure("Space not found");

        var existingFollow = await followRepository.GetByUserAndSpaceAsync(userId, spaceId);

        if (existingFollow is not null)
        {
            // Unfollow
            existingFollow.MarkForRemoval();
            await followRepository.DeleteAsync(existingFollow);

            return Result<bool>.Success(false);
        }

        // Follow
        var follow = Follow.CreateForSpace(userId, spaceId, level);
        await followRepository.AddAsync(follow);

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Update the notification level for a space follow.
    /// </summary>
    public async Task<Result<FollowLevel>> UpdateSpaceFollowLevelAsync(UserId userId, SpaceId spaceId, FollowLevel level)
    {
        var existingFollow = await followRepository.GetByUserAndSpaceAsync(userId, spaceId);

        if (existingFollow is null)
            return Result<FollowLevel>.Failure("Not following this space");

        existingFollow.UpdateLevel(level);
        await followRepository.UpdateAsync(existingFollow);

        return Result<FollowLevel>.Success(level);
    }

    public async Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId) =>
        await followRepository.IsFollowingDiscussionAsync(userId, discussionId);

    public async Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId) =>
        await followRepository.IsFollowingSpaceAsync(userId, spaceId);

    /// <summary>
    /// Gets the follow status and level for a space.
    /// </summary>
    public async Task<(bool IsFollowing, FollowLevel? Level)> GetSpaceFollowStatusAsync(UserId userId, SpaceId spaceId)
    {
        var follow = await followRepository.GetByUserAndSpaceAsync(userId, spaceId);

        if (follow is null)
            return (false, null);

        return (true, follow.Level);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId) =>
        await followRepository.GetFollowersOfDiscussionAsync(discussionId);

    public async Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId) =>
        await followRepository.GetFollowersOfSpaceAsync(spaceId);

    /// <summary>
    /// Toggle follow state for a user.
    /// </summary>
    /// <returns>True if now following, false if unfollowed</returns>
    public async Task<Result<bool>> ToggleFollowUserAsync(UserId userId, UserId followedUserId)
    {
        // Can't follow yourself
        if (userId.Value == followedUserId.Value)
            return Result<bool>.Failure("Cannot follow yourself");

        var targetUser = await userRepository.GetByPublicIdAsync(followedUserId);

        if (targetUser is null)
            return Result<bool>.Failure("User not found");

        var existingFollow = await followRepository.GetByUserAndFollowedUserAsync(userId, followedUserId);

        if (existingFollow is not null)
        {
            // Unfollow
            existingFollow.MarkForRemoval();
            await followRepository.DeleteAsync(existingFollow);
            await counterService.DecrementUserFollowerCountAsync(followedUserId);

            return Result<bool>.Success(false);
        }

        // Follow
        var follow = Follow.CreateForUser(userId, followedUserId);
        await followRepository.AddAsync(follow);
        await counterService.IncrementUserFollowerCountAsync(followedUserId);

        return Result<bool>.Success(true);
    }

    public async Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId) =>
        await followRepository.IsFollowingUserAsync(userId, followedUserId);

    public async Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId) =>
        await followRepository.GetFollowersOfUserAsync(userId);

    public async Task<int> GetFollowerCountOfUserAsync(UserId userId) =>
        await followRepository.GetFollowerCountOfUserAsync(userId);

    /// <summary>
    /// Get all spaces followed by a user (for caching).
    /// </summary>
    public async Task<IEnumerable<SpaceId>> GetFollowedSpacesAsync(UserId userId) =>
        await followRepository.GetFollowedSpacesByUserAsync(userId);

    /// <summary>
    /// Get all discussions followed by a user (for caching).
    /// </summary>
    public async Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsAsync(UserId userId) =>
        await followRepository.GetFollowedDiscussionsByUserAsync(userId);

    /// <summary>
    /// Get all users followed by a user (for caching).
    /// </summary>
    public async Task<IEnumerable<UserId>> GetFollowedUsersAsync(UserId userId) =>
        await followRepository.GetFollowedUsersByUserAsync(userId);
}
