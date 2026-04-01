using NSubstitute;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class FollowUseCaseTests
{
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IDiscussionRepository _discussionRepository = Substitute.For<IDiscussionRepository>();
    private readonly ISpaceRepository _spaceRepository = Substitute.For<ISpaceRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICounterService _counterService = Substitute.For<ICounterService>();
    private FollowUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new FollowUseCase(
            _followRepository,
            _discussionRepository,
            _spaceRepository,
            _userRepository,
            _counterService);
    }

    #region ToggleFollowDiscussionAsync Tests

    [Test]
    public async Task ToggleFollowDiscussionAsync_WhenNotFollowing_CreatesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);
        _followRepository.GetByUserAndDiscussionAsync(userId, discussionId)
            .Returns((Follow?)null); // Not following

        // Act
        var result = await _useCase.ToggleFollowDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        await _followRepository.Received(1).AddAsync(Arg.Any<Follow>());
        await _followRepository.DidNotReceive().DeleteAsync(Arg.Any<Follow>());
    }

    [Test]
    public async Task ToggleFollowDiscussionAsync_WhenAlreadyFollowing_RemovesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var existingFollow = Follow.CreateForDiscussion(userId, discussionId);

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);
        _followRepository.GetByUserAndDiscussionAsync(userId, discussionId)
            .Returns(existingFollow); // Already following

        // Act
        var result = await _useCase.ToggleFollowDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        await _followRepository.Received(1).DeleteAsync(existingFollow);
        await _followRepository.DidNotReceive().AddAsync(Arg.Any<Follow>());
    }

    [Test]
    public async Task ToggleFollowDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns((Discussion?)null);

        // Act
        var result = await _useCase.ToggleFollowDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
        await _followRepository.DidNotReceive().AddAsync(Arg.Any<Follow>());
    }

    #endregion

    #region ToggleFollowSpaceAsync Tests

    [Test]
    public async Task ToggleFollowSpaceAsync_WhenNotFollowing_CreatesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Description");

        _spaceRepository.GetByPublicIdAsync(spaceId)
            .Returns(space);
        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns((Follow?)null); // Not following

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId, FollowLevel.DiscussionsOnly);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        await _followRepository.Received(1).AddAsync(Arg.Is<Follow>(f =>
            f.SpaceId == spaceId
            && f.Level == FollowLevel.DiscussionsOnly));
    }

    [Test]
    public async Task ToggleFollowSpaceAsync_WhenAlreadyFollowing_RemovesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Description");
        var existingFollow = Follow.CreateForSpace(userId, spaceId);

        _spaceRepository.GetByPublicIdAsync(spaceId)
            .Returns(space);
        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns(existingFollow); // Already following

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        await _followRepository.Received(1).DeleteAsync(existingFollow);
        await _followRepository.DidNotReceive().AddAsync(Arg.Any<Follow>());
    }

    [Test]
    public async Task ToggleFollowSpaceAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        _spaceRepository.GetByPublicIdAsync(spaceId)
            .Returns((Space?)null);

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Space not found");
    }

    [Test]
    public async Task ToggleFollowSpaceAsync_WithDiscussionsAndPostsLevel_CreatesFollowWithCorrectLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Description");

        _spaceRepository.GetByPublicIdAsync(spaceId)
            .Returns(space);
        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns((Follow?)null);

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await _followRepository.Received(1).AddAsync(Arg.Is<Follow>(f =>
            f.Level == FollowLevel.DiscussionsAndPosts));
    }

    #endregion

    #region ToggleFollowUserAsync Tests

    [Test]
    public async Task ToggleFollowUserAsync_WhenNotFollowing_CreatesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var targetUser = User.CreateWithEmail("TargetUser", "target@example.com", "hash", "token");

        _userRepository.GetByPublicIdAsync(followedUserId)
            .Returns(targetUser);
        _followRepository.GetByUserAndFollowedUserAsync(userId, followedUserId)
            .Returns((Follow?)null); // Not following

        // Act
        var result = await _useCase.ToggleFollowUserAsync(userId, followedUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        await _followRepository.Received(1).AddAsync(Arg.Any<Follow>());
    }

    [Test]
    public async Task ToggleFollowUserAsync_WhenAlreadyFollowing_RemovesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var targetUser = User.CreateWithEmail("TargetUser", "target@example.com", "hash", "token");
        var existingFollow = Follow.CreateForUser(userId, followedUserId);

        _userRepository.GetByPublicIdAsync(followedUserId)
            .Returns(targetUser);
        _followRepository.GetByUserAndFollowedUserAsync(userId, followedUserId)
            .Returns(existingFollow); // Already following

        // Act
        var result = await _useCase.ToggleFollowUserAsync(userId, followedUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        await _followRepository.Received(1).DeleteAsync(existingFollow);
    }

    [Test]
    public async Task ToggleFollowUserAsync_FollowingSelf_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();

        // Act - Try to follow yourself
        var result = await _useCase.ToggleFollowUserAsync(userId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Cannot follow yourself");
        await _followRepository.DidNotReceive().AddAsync(Arg.Any<Follow>());
    }

    [Test]
    public async Task ToggleFollowUserAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        _userRepository.GetByPublicIdAsync(followedUserId)
            .Returns((User?)null);

        // Act
        var result = await _useCase.ToggleFollowUserAsync(userId, followedUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region UpdateSpaceFollowLevelAsync Tests

    [Test]
    public async Task UpdateSpaceFollowLevelAsync_WhenFollowing_UpdatesLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var existingFollow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsOnly);

        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns(existingFollow);

        // Act
        var result = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(existingFollow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await _followRepository.Received(1).UpdateAsync(existingFollow);
    }

    [Test]
    public async Task UpdateSpaceFollowLevelAsync_WhenNotFollowing_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns((Follow?)null); // Not following

        // Act
        var result = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Not following this space");
        await _followRepository.DidNotReceive().UpdateAsync(Arg.Any<Follow>());
    }

    #endregion

    #region Query Methods Tests

    [Test]
    public async Task IsFollowingDiscussionAsync_WhenFollowing_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        _followRepository.IsFollowingDiscussionAsync(userId, discussionId)
            .Returns(true);

        // Act
        var result = await _useCase.IsFollowingDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsFollowingDiscussionAsync_WhenNotFollowing_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        _followRepository.IsFollowingDiscussionAsync(userId, discussionId)
            .Returns(false);

        // Act
        var result = await _useCase.IsFollowingDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsFollowingSpaceAsync_WhenFollowing_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        _followRepository.IsFollowingSpaceAsync(userId, spaceId)
            .Returns(true);

        // Act
        var result = await _useCase.IsFollowingSpaceAsync(userId, spaceId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsFollowingUserAsync_WhenFollowing_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        _followRepository.IsFollowingUserAsync(userId, followedUserId)
            .Returns(true);

        // Act
        var result = await _useCase.IsFollowingUserAsync(userId, followedUserId);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GetSpaceFollowStatusAsync_WhenFollowing_ReturnsStatusAndLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns(follow);

        // Act
        var (isFollowing, level) = await _useCase.GetSpaceFollowStatusAsync(userId, spaceId);

        // Assert
        await Assert.That(isFollowing).IsTrue();
        await Assert.That(level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task GetSpaceFollowStatusAsync_WhenNotFollowing_ReturnsNotFollowing()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns((Follow?)null);

        // Act
        var (isFollowing, level) = await _useCase.GetSpaceFollowStatusAsync(userId, spaceId);

        // Assert
        await Assert.That(isFollowing).IsFalse();
        await Assert.That(level).IsNull();
    }

    [Test]
    public async Task GetFollowersOfDiscussionAsync_ReturnsFollowerIds()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var followerIds = new List<UserId> { UserId.New(), UserId.New(), UserId.New() };

        _followRepository.GetFollowersOfDiscussionAsync(discussionId)
            .Returns(followerIds);

        // Act
        var result = await _useCase.GetFollowersOfDiscussionAsync(discussionId);

        // Assert
        await Assert.That(result).Count().IsEqualTo(3);
    }

    [Test]
    public async Task GetFollowerCountOfUserAsync_ReturnsCount()
    {
        // Arrange
        var userId = UserId.New();
        const int expectedCount = 42;

        _followRepository.GetFollowerCountOfUserAsync(userId)
            .Returns(expectedCount);

        // Act
        var result = await _useCase.GetFollowerCountOfUserAsync(userId);

        // Assert
        await Assert.That(result).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task GetFollowedSpacesAsync_ReturnsSpaceIds()
    {
        // Arrange
        var userId = UserId.New();
        var spaceIds = new List<SpaceId> { SpaceId.New(), SpaceId.New() };

        _followRepository.GetFollowedSpacesByUserAsync(userId)
            .Returns(spaceIds);

        // Act
        var result = await _useCase.GetFollowedSpacesAsync(userId);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetFollowedDiscussionsAsync_ReturnsDiscussionIds()
    {
        // Arrange
        var userId = UserId.New();
        var discussionIds = new List<DiscussionId> { DiscussionId.New(), DiscussionId.New(), DiscussionId.New() };

        _followRepository.GetFollowedDiscussionsByUserAsync(userId)
            .Returns(discussionIds);

        // Act
        var result = await _useCase.GetFollowedDiscussionsAsync(userId);

        // Assert
        await Assert.That(result).Count().IsEqualTo(3);
    }

    [Test]
    public async Task GetFollowedUsersAsync_ReturnsUserIds()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserIds = new List<UserId> { UserId.New(), UserId.New() };

        _followRepository.GetFollowedUsersByUserAsync(userId)
            .Returns(followedUserIds);

        // Act
        var result = await _useCase.GetFollowedUsersAsync(userId);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task ToggleFollowSpaceAsync_ToggleTwice_FollowsAndUnfollows()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Description");

        _spaceRepository.GetByPublicIdAsync(spaceId)
            .Returns(space);

        // First call - not following
        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns((Follow?)null);

        // Act - First toggle (follow)
        var firstResult = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert first toggle
        await Assert.That(firstResult.IsSuccess).IsTrue();
        await Assert.That(firstResult.Value).IsTrue();

        // Arrange - Second call - now following
        var createdFollow = Follow.CreateForSpace(userId, spaceId);
        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns(createdFollow);

        // Act - Second toggle (unfollow)
        var secondResult = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert second toggle
        await Assert.That(secondResult.IsSuccess).IsTrue();
        await Assert.That(secondResult.Value).IsFalse();

        await _followRepository.Received(1).AddAsync(Arg.Any<Follow>());
        await _followRepository.Received(1).DeleteAsync(Arg.Any<Follow>());
    }

    [Test]
    public async Task UpdateSpaceFollowLevelAsync_ToggleBetweenLevels_Works()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsOnly);

        _followRepository.GetByUserAndSpaceAsync(userId, spaceId)
            .Returns(follow);

        // Act & Assert - Toggle to DiscussionsAndPosts
        var result1 = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);
        await Assert.That(result1.IsSuccess).IsTrue();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);

        // Act & Assert - Toggle back to DiscussionsOnly
        var result2 = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsOnly);
        await Assert.That(result2.IsSuccess).IsTrue();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);

        await _followRepository.Received(2).UpdateAsync(follow);
    }

    #endregion
}
