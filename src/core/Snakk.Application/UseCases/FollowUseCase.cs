namespace Snakk.Application.UseCases;

using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public class FollowUseCase(
    IFollowRepository followRepository,
    IDiscussionRepository discussionRepository,
    ISpaceRepository spaceRepository,
    IUserRepository userRepository)
{
    private readonly IFollowRepository _followRepository = followRepository;
    private readonly IDiscussionRepository _discussionRepository = discussionRepository;
    private readonly ISpaceRepository _spaceRepository = spaceRepository;
    private readonly IUserRepository _userRepository = userRepository;

    /// <summary>
    /// Toggle follow state for a discussion.
    /// </summary>
    /// <returns>True if now following, false if unfollowed</returns>
    public async Task<Result<bool>> ToggleFollowDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        var discussion = await _discussionRepository.GetByPublicIdAsync(discussionId);
        if (discussion == null)
            return Result<bool>.Failure("Discussion not found");

        var existingFollow = await _followRepository.GetByUserAndDiscussionAsync(userId, discussionId);

        if (existingFollow != null)
        {
            // Unfollow
            existingFollow.MarkForRemoval();
            await _followRepository.DeleteAsync(existingFollow);
            return Result<bool>.Success(false);
        }

        // Follow
        var follow = Follow.CreateForDiscussion(userId, discussionId);
        await _followRepository.AddAsync(follow);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Toggle follow state for a space.
    /// </summary>
    /// <returns>True if now following, false if unfollowed</returns>
    public async Task<Result<bool>> ToggleFollowSpaceAsync(UserId userId, SpaceId spaceId, FollowLevel level = FollowLevel.DiscussionsOnly)
    {
        var space = await _spaceRepository.GetByPublicIdAsync(spaceId);
        if (space == null)
            return Result<bool>.Failure("Space not found");

        var existingFollow = await _followRepository.GetByUserAndSpaceAsync(userId, spaceId);

        if (existingFollow != null)
        {
            // Unfollow
            existingFollow.MarkForRemoval();
            await _followRepository.DeleteAsync(existingFollow);
            return Result<bool>.Success(false);
        }

        // Follow
        var follow = Follow.CreateForSpace(userId, spaceId, level);
        await _followRepository.AddAsync(follow);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Update the notification level for a space follow.
    /// </summary>
    public async Task<Result<FollowLevel>> UpdateSpaceFollowLevelAsync(UserId userId, SpaceId spaceId, FollowLevel level)
    {
        var existingFollow = await _followRepository.GetByUserAndSpaceAsync(userId, spaceId);

        if (existingFollow == null)
            return Result<FollowLevel>.Failure("Not following this space");

        existingFollow.UpdateLevel(level);
        await _followRepository.UpdateAsync(existingFollow);
        return Result<FollowLevel>.Success(level);
    }

    public async Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        return await _followRepository.IsFollowingDiscussionAsync(userId, discussionId);
    }

    public async Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId)
    {
        return await _followRepository.IsFollowingSpaceAsync(userId, spaceId);
    }

    /// <summary>
    /// Gets the follow status and level for a space.
    /// </summary>
    public async Task<(bool IsFollowing, FollowLevel? Level)> GetSpaceFollowStatusAsync(UserId userId, SpaceId spaceId)
    {
        var follow = await _followRepository.GetByUserAndSpaceAsync(userId, spaceId);
        if (follow == null)
            return (false, null);
        return (true, follow.Level);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId)
    {
        return await _followRepository.GetFollowersOfDiscussionAsync(discussionId);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId)
    {
        return await _followRepository.GetFollowersOfSpaceAsync(spaceId);
    }

    /// <summary>
    /// Toggle follow state for a user.
    /// </summary>
    /// <returns>True if now following, false if unfollowed</returns>
    public async Task<Result<bool>> ToggleFollowUserAsync(UserId userId, UserId followedUserId)
    {
        // Can't follow yourself
        if (userId.Value == followedUserId.Value)
            return Result<bool>.Failure("Cannot follow yourself");

        var targetUser = await _userRepository.GetByPublicIdAsync(followedUserId);
        if (targetUser == null)
            return Result<bool>.Failure("User not found");

        var existingFollow = await _followRepository.GetByUserAndFollowedUserAsync(userId, followedUserId);

        if (existingFollow != null)
        {
            // Unfollow
            existingFollow.MarkForRemoval();
            await _followRepository.DeleteAsync(existingFollow);
            return Result<bool>.Success(false);
        }

        // Follow
        var follow = Follow.CreateForUser(userId, followedUserId);
        await _followRepository.AddAsync(follow);
        return Result<bool>.Success(true);
    }

    public async Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId)
    {
        return await _followRepository.IsFollowingUserAsync(userId, followedUserId);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId)
    {
        return await _followRepository.GetFollowersOfUserAsync(userId);
    }

    public async Task<int> GetFollowerCountOfUserAsync(UserId userId)
    {
        return await _followRepository.GetFollowerCountOfUserAsync(userId);
    }

    /// <summary>
    /// Get all spaces followed by a user (for caching).
    /// </summary>
    public async Task<IEnumerable<SpaceId>> GetFollowedSpacesAsync(UserId userId)
    {
        return await _followRepository.GetFollowedSpacesByUserAsync(userId);
    }

    /// <summary>
    /// Get all discussions followed by a user (for caching).
    /// </summary>
    public async Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsAsync(UserId userId)
    {
        return await _followRepository.GetFollowedDiscussionsByUserAsync(userId);
    }

    /// <summary>
    /// Get all users followed by a user (for caching).
    /// </summary>
    public async Task<IEnumerable<UserId>> GetFollowedUsersAsync(UserId userId)
    {
        return await _followRepository.GetFollowedUsersByUserAsync(userId);
    }
}
