using Moq;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class FollowUseCaseTests
{
    private readonly Mock<IFollowRepository> _mockFollowRepository = new();
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepository = new();
    private readonly Mock<ISpaceRepository> _mockSpaceRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private FollowUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new FollowUseCase(
            _mockFollowRepository.Object,
            _mockDiscussionRepository.Object,
            _mockSpaceRepository.Object,
            _mockUserRepository.Object);
    }

    #region ToggleFollowDiscussionAsync Tests

    [Test]
    public async Task ToggleFollowDiscussionAsync_WhenNotFollowing_CreatesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockFollowRepository.Setup(r => r.GetByUserAndDiscussionAsync(userId, discussionId))
            .ReturnsAsync((Follow?)null); // Not following

        // Act
        var result = await _useCase.ToggleFollowDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Once);
        _mockFollowRepository.Verify(r => r.DeleteAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Test]
    public async Task ToggleFollowDiscussionAsync_WhenAlreadyFollowing_RemovesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var existingFollow = Follow.CreateForDiscussion(userId, discussionId);

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync(discussion);
        _mockFollowRepository.Setup(r => r.GetByUserAndDiscussionAsync(userId, discussionId))
            .ReturnsAsync(existingFollow); // Already following

        // Act
        var result = await _useCase.ToggleFollowDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        _mockFollowRepository.Verify(r => r.DeleteAsync(existingFollow), Times.Once);
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Test]
    public async Task ToggleFollowDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        _mockDiscussionRepository.Setup(r => r.GetByPublicIdAsync(discussionId))
            .ReturnsAsync((Discussion?)null);

        // Act
        var result = await _useCase.ToggleFollowDiscussionAsync(userId, discussionId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
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

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);
        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync((Follow?)null); // Not following

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId, FollowLevel.DiscussionsOnly);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockFollowRepository.Verify(r => r.AddAsync(It.Is<Follow>(f =>
            f.SpaceId == spaceId &&
            f.Level == FollowLevel.DiscussionsOnly)), Times.Once);
    }

    [Test]
    public async Task ToggleFollowSpaceAsync_WhenAlreadyFollowing_RemovesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space", "Description");
        var existingFollow = Follow.CreateForSpace(userId, spaceId);

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);
        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync(existingFollow); // Already following

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        _mockFollowRepository.Verify(r => r.DeleteAsync(existingFollow), Times.Once);
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Test]
    public async Task ToggleFollowSpaceAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync((Space?)null);

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

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);
        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync((Follow?)null);

        // Act
        var result = await _useCase.ToggleFollowSpaceAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockFollowRepository.Verify(r => r.AddAsync(It.Is<Follow>(f =>
            f.Level == FollowLevel.DiscussionsAndPosts)), Times.Once);
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

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(followedUserId))
            .ReturnsAsync(targetUser);
        _mockFollowRepository.Setup(r => r.GetByUserAndFollowedUserAsync(userId, followedUserId))
            .ReturnsAsync((Follow?)null); // Not following

        // Act
        var result = await _useCase.ToggleFollowUserAsync(userId, followedUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTrue();

        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Once);
    }

    [Test]
    public async Task ToggleFollowUserAsync_WhenAlreadyFollowing_RemovesFollow()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var targetUser = User.CreateWithEmail("TargetUser", "target@example.com", "hash", "token");
        var existingFollow = Follow.CreateForUser(userId, followedUserId);

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(followedUserId))
            .ReturnsAsync(targetUser);
        _mockFollowRepository.Setup(r => r.GetByUserAndFollowedUserAsync(userId, followedUserId))
            .ReturnsAsync(existingFollow); // Already following

        // Act
        var result = await _useCase.ToggleFollowUserAsync(userId, followedUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsFalse();

        _mockFollowRepository.Verify(r => r.DeleteAsync(existingFollow), Times.Once);
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
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Test]
    public async Task ToggleFollowUserAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(followedUserId))
            .ReturnsAsync((User?)null);

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

        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync(existingFollow);

        // Act
        var result = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(existingFollow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        _mockFollowRepository.Verify(r => r.UpdateAsync(existingFollow), Times.Once);
    }

    [Test]
    public async Task UpdateSpaceFollowLevelAsync_WhenNotFollowing_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync((Follow?)null); // Not following

        // Act
        var result = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Not following this space");
        _mockFollowRepository.Verify(r => r.UpdateAsync(It.IsAny<Follow>()), Times.Never);
    }

    #endregion

    #region Query Methods Tests

    [Test]
    public async Task IsFollowingDiscussionAsync_WhenFollowing_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        _mockFollowRepository.Setup(r => r.IsFollowingDiscussionAsync(userId, discussionId))
            .ReturnsAsync(true);

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

        _mockFollowRepository.Setup(r => r.IsFollowingDiscussionAsync(userId, discussionId))
            .ReturnsAsync(false);

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

        _mockFollowRepository.Setup(r => r.IsFollowingSpaceAsync(userId, spaceId))
            .ReturnsAsync(true);

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

        _mockFollowRepository.Setup(r => r.IsFollowingUserAsync(userId, followedUserId))
            .ReturnsAsync(true);

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

        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync(follow);

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

        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync((Follow?)null);

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

        _mockFollowRepository.Setup(r => r.GetFollowersOfDiscussionAsync(discussionId))
            .ReturnsAsync(followerIds);

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

        _mockFollowRepository.Setup(r => r.GetFollowerCountOfUserAsync(userId))
            .ReturnsAsync(expectedCount);

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

        _mockFollowRepository.Setup(r => r.GetFollowedSpacesByUserAsync(userId))
            .ReturnsAsync(spaceIds);

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

        _mockFollowRepository.Setup(r => r.GetFollowedDiscussionsByUserAsync(userId))
            .ReturnsAsync(discussionIds);

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

        _mockFollowRepository.Setup(r => r.GetFollowedUsersByUserAsync(userId))
            .ReturnsAsync(followedUserIds);

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

        _mockSpaceRepository.Setup(r => r.GetByPublicIdAsync(spaceId))
            .ReturnsAsync(space);

        // First call - not following
        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync((Follow?)null);

        // Act - First toggle (follow)
        var firstResult = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert first toggle
        await Assert.That(firstResult.IsSuccess).IsTrue();
        await Assert.That(firstResult.Value).IsTrue();

        // Arrange - Second call - now following
        var createdFollow = Follow.CreateForSpace(userId, spaceId);
        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync(createdFollow);

        // Act - Second toggle (unfollow)
        var secondResult = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert second toggle
        await Assert.That(secondResult.IsSuccess).IsTrue();
        await Assert.That(secondResult.Value).IsFalse();

        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Once);
        _mockFollowRepository.Verify(r => r.DeleteAsync(It.IsAny<Follow>()), Times.Once);
    }

    [Test]
    public async Task UpdateSpaceFollowLevelAsync_ToggleBetweenLevels_Works()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsOnly);

        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync(follow);

        // Act & Assert - Toggle to DiscussionsAndPosts
        var result1 = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsAndPosts);
        await Assert.That(result1.IsSuccess).IsTrue();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);

        // Act & Assert - Toggle back to DiscussionsOnly
        var result2 = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsOnly);
        await Assert.That(result2.IsSuccess).IsTrue();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);

        _mockFollowRepository.Verify(r => r.UpdateAsync(follow), Times.Exactly(2));
    }

    #endregion
}
