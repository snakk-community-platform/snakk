using Moq;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class UserProfileUseCaseTests
{
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private UserProfileUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new UserProfileUseCase(
            _mockUserRepository.Object);
    }

    #region GetUserProfileAsync Tests

    [Test]
    public async Task GetUserProfileAsync_WithExistingUser_ReturnsProfile()
    {
        // Arrange
        var user = User.Rehydrate(
            UserId.New(), "TestUser", "test@example.com", "hash", true, null,
            null, null, null, "avatar.png", 1, true,
            DateTime.UtcNow.AddDays(-30),
            lastSeenAt: DateTime.UtcNow,
            discussionCount: 15,
            replyCount: 120,
            followerCount: 8);
        var publicId = user.PublicId.Value;

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.GetUserProfileAsync(publicId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(publicId);
        await Assert.That(result.DisplayName).IsEqualTo("TestUser");
        await Assert.That(result.AvatarFileName).IsEqualTo("avatar.png");
        await Assert.That(result.DiscussionCount).IsEqualTo(15);
        await Assert.That(result.ReplyCount).IsEqualTo(120);
        await Assert.That(result.FollowerCount).IsEqualTo(8);
        await Assert.That(result.JoinedAt).IsEqualTo(user.CreatedAt);
        await Assert.That(result.LastSeenAt).IsEqualTo(user.LastSeenAt);
    }

    [Test]
    public async Task GetUserProfileAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = UserId.New();

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.GetUserProfileAsync(userId.Value);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetUserProfileAsync_WithNoAvatar_ReturnsNullAvatarFileName()
    {
        // Arrange
        var user = User.CreateWithEmail("NoAvatarUser", "no-avatar@test.com", "hash", "token");
        var publicId = user.PublicId.Value;

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.GetUserProfileAsync(publicId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.AvatarFileName).IsNull();
    }

    [Test]
    public async Task GetUserProfileAsync_WithZeroCounts_ReturnsZeroCounts()
    {
        // Arrange
        var user = User.CreateWithEmail("NewUser", "new@test.com", "hash", "token");
        var publicId = user.PublicId.Value;

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.GetUserProfileAsync(publicId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DiscussionCount).IsEqualTo(0);
        await Assert.That(result.ReplyCount).IsEqualTo(0);
        await Assert.That(result.FollowerCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetUserProfileAsync_WithNullLastSeenAt_ReturnsNullLastSeenAt()
    {
        // Arrange
        var user = User.Rehydrate(
            UserId.New(), "TestUser", "test@example.com", "hash", true, null,
            null, null, null, null, 0, true,
            DateTime.UtcNow.AddDays(-30),
            lastSeenAt: null);
        var publicId = user.PublicId.Value;

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.GetUserProfileAsync(publicId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.LastSeenAt).IsNull();
    }

    #endregion
}
