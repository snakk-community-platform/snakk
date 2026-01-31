using FluentAssertions;
using Moq;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class FollowUseCaseTests
{
    private readonly Mock<IFollowRepository> _mockFollowRepository;
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepository;
    private readonly Mock<ISpaceRepository> _mockSpaceRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly FollowUseCase _useCase;

    public FollowUseCaseTests()
    {
        _mockFollowRepository = new Mock<IFollowRepository>();
        _mockDiscussionRepository = new Mock<IDiscussionRepository>();
        _mockSpaceRepository = new Mock<ISpaceRepository>();
        _mockUserRepository = new Mock<IUserRepository>();

        _useCase = new FollowUseCase(
            _mockFollowRepository.Object,
            _mockDiscussionRepository.Object,
            _mockSpaceRepository.Object,
            _mockUserRepository.Object);
    }

    #region ToggleFollowDiscussionAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Now following

        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Once);
        _mockFollowRepository.Verify(r => r.DeleteAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(); // No longer following

        _mockFollowRepository.Verify(r => r.DeleteAsync(existingFollow), Times.Once);
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Discussion not found");
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    #endregion

    #region ToggleFollowSpaceAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Now following

        _mockFollowRepository.Verify(r => r.AddAsync(It.Is<Follow>(f =>
            f.SpaceId == spaceId &&
            f.Level == FollowLevel.DiscussionsOnly)), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(); // No longer following

        _mockFollowRepository.Verify(r => r.DeleteAsync(existingFollow), Times.Once);
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Space not found");
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        _mockFollowRepository.Verify(r => r.AddAsync(It.Is<Follow>(f =>
            f.Level == FollowLevel.DiscussionsAndPosts)), Times.Once);
    }

    #endregion

    #region ToggleFollowUserAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Now following

        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(); // No longer following

        _mockFollowRepository.Verify(r => r.DeleteAsync(existingFollow), Times.Once);
    }

    [Fact]
    public async Task ToggleFollowUserAsync_FollowingSelf_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();

        // Act - Try to follow yourself
        var result = await _useCase.ToggleFollowUserAsync(userId, userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot follow yourself");
        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Never);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("User not found");
    }

    #endregion

    #region UpdateSpaceFollowLevelAsync Tests

    [Fact]
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(FollowLevel.DiscussionsAndPosts);
        existingFollow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
        _mockFollowRepository.Verify(r => r.UpdateAsync(existingFollow), Times.Once);
    }

    [Fact]
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
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Not following this space");
        _mockFollowRepository.Verify(r => r.UpdateAsync(It.IsAny<Follow>()), Times.Never);
    }

    #endregion

    #region Query Methods Tests

    [Fact]
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
        result.Should().BeTrue();
    }

    [Fact]
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
        result.Should().BeFalse();
    }

    [Fact]
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
        result.Should().BeTrue();
    }

    [Fact]
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
        result.Should().BeTrue();
    }

    [Fact]
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
        isFollowing.Should().BeTrue();
        level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    [Fact]
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
        isFollowing.Should().BeFalse();
        level.Should().BeNull();
    }

    [Fact]
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
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(followerIds);
    }

    [Fact]
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
        result.Should().Be(expectedCount);
    }

    [Fact]
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
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(spaceIds);
    }

    [Fact]
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
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(discussionIds);
    }

    [Fact]
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
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(followedUserIds);
    }

    #endregion

    #region Edge Cases

    [Fact]
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
        firstResult.IsSuccess.Should().BeTrue();
        firstResult.Value.Should().BeTrue(); // Now following

        // Arrange - Second call - now following
        var createdFollow = Follow.CreateForSpace(userId, spaceId);
        _mockFollowRepository.Setup(r => r.GetByUserAndSpaceAsync(userId, spaceId))
            .ReturnsAsync(createdFollow);

        // Act - Second toggle (unfollow)
        var secondResult = await _useCase.ToggleFollowSpaceAsync(userId, spaceId);

        // Assert second toggle
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().BeFalse(); // No longer following

        _mockFollowRepository.Verify(r => r.AddAsync(It.IsAny<Follow>()), Times.Once);
        _mockFollowRepository.Verify(r => r.DeleteAsync(It.IsAny<Follow>()), Times.Once);
    }

    [Fact]
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
        result1.IsSuccess.Should().BeTrue();
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);

        // Act & Assert - Toggle back to DiscussionsOnly
        var result2 = await _useCase.UpdateSpaceFollowLevelAsync(userId, spaceId, FollowLevel.DiscussionsOnly);
        result2.IsSuccess.Should().BeTrue();
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);

        _mockFollowRepository.Verify(r => r.UpdateAsync(follow), Times.Exactly(2));
    }

    #endregion
}
