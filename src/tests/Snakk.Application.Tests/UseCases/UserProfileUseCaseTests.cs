using NSubstitute;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;

namespace Snakk.Application.Tests.UseCases;

public class UserProfileUseCaseTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IReactionRepository _reactionRepository = Substitute.For<IReactionRepository>();
    private readonly IAchievementRepository _achievementRepository = Substitute.For<IAchievementRepository>();
    private readonly IDiscussionRepository _discussionRepository = Substitute.For<IDiscussionRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IUserAchievementRepository _userAchievementRepository = Substitute.For<IUserAchievementRepository>();
    private readonly IUserAchievementProgressRepository _userAchievementProgressRepository = Substitute.For<IUserAchievementProgressRepository>();
    private UserProfileUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _followRepository.GetFollowingCountByUserAsync(Arg.Any<UserId>()).Returns(0);
        _reactionRepository.GetTotalReactionsReceivedByUserAsync(Arg.Any<UserId>()).Returns(0);
        _discussionRepository.GetTopDiscussionsByUserAsync(Arg.Any<UserId>(), Arg.Any<int>()).Returns([]);
        _postRepository.GetTopSpacesForUserAsync(Arg.Any<UserId>(), Arg.Any<int>()).Returns([]);
        _userAchievementRepository.GetDisplayedByUserIdAsync(Arg.Any<UserId>()).Returns([]);

        var achievementService = new AchievementService(
            _achievementRepository,
            _userAchievementRepository,
            _userAchievementProgressRepository);

        _useCase = new UserProfileUseCase(
            _userRepository,
            _followRepository,
            _reactionRepository,
            _achievementRepository,
            _discussionRepository,
            _postRepository,
            achievementService);
    }

    #region GetUserProfileAsync Tests

    [Test]
    public async Task GetUserProfileAsync_WithExistingUser_ReturnsProfile()
    {
        var user = User.Rehydrate(
            UserId.New(), "TestUser", "test@example.com", "hash", true, null,
            null, "avatar.png", null, null, 1, true,
            DateTime.UtcNow.AddDays(-30),
            lastSeenAt: DateTime.UtcNow,
            discussionCount: 15,
            replyCount: 120,
            followerCount: 8);
        var publicId = user.PublicId.Value;
        _userRepository.GetProfileSlimByPublicIdAsync(user.PublicId).Returns(
            new UserProfileSlim(publicId, "TestUser", "avatar.png", null, user.CreatedAt, user.LastSeenAt, 15, 8, 120, null));

        var result = await _useCase.GetUserProfileAsync(publicId);

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
        var userId = UserId.New();
        _userRepository.GetProfileSlimByPublicIdAsync(userId).Returns((UserProfileSlim?)null);

        var result = await _useCase.GetUserProfileAsync(userId.Value);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetUserProfileAsync_WithNoAvatar_ReturnsNullAvatarFileName()
    {
        var user = User.CreateWithEmail("NoAvatarUser", "no-avatar@test.com", "hash", "token");
        var publicId = user.PublicId.Value;
        _userRepository.GetProfileSlimByPublicIdAsync(user.PublicId).Returns(
            new UserProfileSlim(publicId, "NoAvatarUser", null, null, user.CreatedAt, null, 0, 0, 0, null));

        var result = await _useCase.GetUserProfileAsync(publicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.AvatarFileName).IsNull();
    }

    [Test]
    public async Task GetUserProfileAsync_WithZeroCounts_ReturnsZeroCounts()
    {
        var user = User.CreateWithEmail("NewUser", "new@test.com", "hash", "token");
        var publicId = user.PublicId.Value;
        _userRepository.GetProfileSlimByPublicIdAsync(user.PublicId).Returns(
            new UserProfileSlim(publicId, "NewUser", null, null, user.CreatedAt, null, 0, 0, 0, null));

        var result = await _useCase.GetUserProfileAsync(publicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.DiscussionCount).IsEqualTo(0);
        await Assert.That(result.ReplyCount).IsEqualTo(0);
        await Assert.That(result.FollowerCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetUserProfileAsync_WithNullLastSeenAt_ReturnsNullLastSeenAt()
    {
        var user = User.Rehydrate(
            UserId.New(), "TestUser", "test@example.com", "hash", true, null,
            null, null, null, null, 0, true,
            DateTime.UtcNow.AddDays(-30),
            lastSeenAt: null);
        var publicId = user.PublicId.Value;
        _userRepository.GetProfileSlimByPublicIdAsync(user.PublicId).Returns(
            new UserProfileSlim(publicId, "TestUser", null, null, user.CreatedAt, null, 0, 0, 0, null));

        var result = await _useCase.GetUserProfileAsync(publicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.LastSeenAt).IsNull();
    }

    #endregion
}
