using Moq;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class StatisticsUseCaseTests
{
    private readonly Mock<IPostRepository> _mockPostRepo = new();
    private readonly Mock<IDiscussionRepository> _mockDiscussionRepo = new();
    private readonly Mock<IUserRepository> _mockUserRepo = new();
    private readonly Mock<IStatsRepository> _mockStatsRepo = new();
    private StatisticsUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new StatisticsUseCase(
            _mockPostRepo.Object,
            _mockDiscussionRepo.Object,
            _mockUserRepo.Object,
            _mockStatsRepo.Object);
    }

    #region GetTopContributorsTodayAsync Tests

    [Test]
    public async Task GetTopContributorsTodayAsync_WithContributors_ReturnsEnrichedResults()
    {
        // Arrange
        var userId1 = UserId.New();
        var userId2 = UserId.New();

        var topContributors = new List<(UserId UserId, int PostCount)>
        {
            (userId1, 10),
            (userId2, 5)
        };

        var user1 = User.Rehydrate(userId1, "TopUser1", "user1@test.com", null, true, null, null, null, null, "avatar1.png", 0, true, true, DateTime.UtcNow);
        var user2 = User.Rehydrate(userId2, "TopUser2", "user2@test.com", null, true, null, null, null, null, null, 0, true, true, DateTime.UtcNow);

        _mockPostRepo.Setup(r => r.GetTopContributorsSinceAsync(
                It.IsAny<DateTime>(), null, null, null, 5))
            .ReturnsAsync(topContributors);
        _mockUserRepo.Setup(r => r.GetByPublicIdsAsync(It.IsAny<IEnumerable<UserId>>()))
            .ReturnsAsync([user1, user2]);

        // Act
        var result = await _useCase.GetTopContributorsTodayAsync();

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Items).Count().IsEqualTo(2);

        var items = result.Value.Items.ToList();
        await Assert.That(items[0].DisplayName).IsEqualTo("TopUser1");
        await Assert.That(items[0].PostCountToday).IsEqualTo(10);
        await Assert.That(items[0].AvatarFileName).IsEqualTo("avatar1.png");
        await Assert.That(items[1].DisplayName).IsEqualTo("TopUser2");
        await Assert.That(items[1].PostCountToday).IsEqualTo(5);
        await Assert.That(items[1].AvatarFileName).IsNull();
    }

    [Test]
    public async Task GetTopContributorsTodayAsync_WithDeletedUser_ShowsDeletedUserName()
    {
        // Arrange
        var userId = UserId.New();
        var topContributors = new List<(UserId UserId, int PostCount)>
        {
            (userId, 3)
        };

        _mockPostRepo.Setup(r => r.GetTopContributorsSinceAsync(
                It.IsAny<DateTime>(), null, null, null, 5))
            .ReturnsAsync(topContributors);
        _mockUserRepo.Setup(r => r.GetByPublicIdsAsync(It.IsAny<IEnumerable<UserId>>()))
            .ReturnsAsync([]); // No users found (deleted)

        // Act
        var result = await _useCase.GetTopContributorsTodayAsync();

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        var items = result.Value!.Items.ToList();
        await Assert.That(items[0].DisplayName).IsEqualTo("Deleted User");
    }

    [Test]
    public async Task GetTopContributorsTodayAsync_WithHubFilter_PassesHubId()
    {
        // Arrange
        const string hubId = "hub-123";

        _mockPostRepo.Setup(r => r.GetTopContributorsSinceAsync(
                It.IsAny<DateTime>(), HubId.From(hubId), null, null, 5))
            .ReturnsAsync([]);
        _mockUserRepo.Setup(r => r.GetByPublicIdsAsync(It.IsAny<IEnumerable<UserId>>()))
            .ReturnsAsync([]);

        // Act
        await _useCase.GetTopContributorsTodayAsync(hubId: hubId);

        // Assert
        _mockPostRepo.Verify(r => r.GetTopContributorsSinceAsync(
            It.IsAny<DateTime>(), It.Is<HubId>(h => h.Value == hubId), null, null, 5), Times.Once);
    }

    [Test]
    public async Task GetTopContributorsTodayAsync_WithCustomLimit_PassesLimit()
    {
        // Arrange
        _mockPostRepo.Setup(r => r.GetTopContributorsSinceAsync(
                It.IsAny<DateTime>(), null, null, null, 10))
            .ReturnsAsync([]);
        _mockUserRepo.Setup(r => r.GetByPublicIdsAsync(It.IsAny<IEnumerable<UserId>>()))
            .ReturnsAsync([]);

        // Act
        await _useCase.GetTopContributorsTodayAsync(limit: 10);

        // Assert
        _mockPostRepo.Verify(r => r.GetTopContributorsSinceAsync(
            It.IsAny<DateTime>(), null, null, null, 10), Times.Once);
    }

    #endregion

    #region GetTopActiveDiscussionsTodayAsync Tests

    [Test]
    public async Task GetTopActiveDiscussionsTodayAsync_ReturnsResults()
    {
        // Arrange
        var topDiscussions = new List<TopActiveDiscussion>
        {
            new(DiscussionId.New(), "Hot Discussion", "hot-discussion", 15,
                "space-1", "general", "General",
                "hub-1", "main", "Main Hub",
                "user-1", "Author1"),
            new(DiscussionId.New(), "Another Active", "another-active", 8,
                "space-2", "off-topic", "Off Topic",
                "hub-1", "main", "Main Hub",
                "user-2", "Author2")
        };

        _mockDiscussionRepo.Setup(r => r.GetTopActiveDiscussionsSinceAsync(
                It.IsAny<DateTime>(), null, null, null, 5))
            .ReturnsAsync(topDiscussions);

        // Act
        var result = await _useCase.GetTopActiveDiscussionsTodayAsync();

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Items).Count().IsEqualTo(2);

        var items = result.Value.Items.ToList();
        await Assert.That(items[0].Title).IsEqualTo("Hot Discussion");
        await Assert.That(items[0].PostCountToday).IsEqualTo(15);
        await Assert.That(items[1].Title).IsEqualTo("Another Active");
    }

    [Test]
    public async Task GetTopActiveDiscussionsTodayAsync_WithSpaceFilter_PassesSpaceId()
    {
        // Arrange
        const string spaceId = "space-123";

        _mockDiscussionRepo.Setup(r => r.GetTopActiveDiscussionsSinceAsync(
                It.IsAny<DateTime>(), null, SpaceId.From(spaceId), null, 5))
            .ReturnsAsync([]);

        // Act
        await _useCase.GetTopActiveDiscussionsTodayAsync(spaceId: spaceId);

        // Assert
        _mockDiscussionRepo.Verify(r => r.GetTopActiveDiscussionsSinceAsync(
            It.IsAny<DateTime>(), null, It.Is<SpaceId>(s => s.Value == spaceId), null, 5), Times.Once);
    }

    #endregion

    #region GetUserActivityHistoryAsync Tests

    [Test]
    public async Task GetUserActivityHistoryAsync_WithExistingUser_ReturnsActivityData()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var userId = user.PublicId;

        _mockUserRepo.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        var today = DateTime.UtcNow.Date;
        var discussionActivity = new List<(DateTime Date, int Count)>
        {
            (today, 2),
            (today.AddDays(-1), 1)
        };
        var postActivity = new List<(DateTime Date, int Count)>
        {
            (today, 5),
            (today.AddDays(-2), 3)
        };

        _mockDiscussionRepo.Setup(r => r.GetActivityByDateAsync(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(discussionActivity);
        _mockPostRepo.Setup(r => r.GetActivityByDateAsync(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(postActivity);

        // Act
        var result = await _useCase.GetUserActivityHistoryAsync(userId.Value, 30);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Days).IsEqualTo(30);
        await Assert.That(result.Value.Data).Count().IsEqualTo(30);

        // Verify the activity data contains correct values for today
        var todayData = result.Value.Data.FirstOrDefault(d => d.Date == today);
        await Assert.That(todayData).IsNotNull();
        await Assert.That(todayData!.Discussions).IsEqualTo(2);
        await Assert.That(todayData.Posts).IsEqualTo(5);
        await Assert.That(todayData.Total).IsEqualTo(7);
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();

        _mockUserRepo.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.GetUserActivityHistoryAsync(userId.Value);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_WithInvalidDays_DefaultsTo30()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepo.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);
        _mockDiscussionRepo.Setup(r => r.GetActivityByDateAsync(user.PublicId, It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _mockPostRepo.Setup(r => r.GetActivityByDateAsync(user.PublicId, It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act - days = 0 should default to 30
        var result = await _useCase.GetUserActivityHistoryAsync(user.PublicId.Value, 0);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Days).IsEqualTo(30);
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_WithOver90Days_DefaultsTo30()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepo.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);
        _mockDiscussionRepo.Setup(r => r.GetActivityByDateAsync(user.PublicId, It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _mockPostRepo.Setup(r => r.GetActivityByDateAsync(user.PublicId, It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act - days = 100 should default to 30
        var result = await _useCase.GetUserActivityHistoryAsync(user.PublicId.Value, 100);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Days).IsEqualTo(30);
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_DataIsSortedByDate()
    {
        // Arrange
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _mockUserRepo.Setup(r => r.GetByPublicIdAsync(user.PublicId))
            .ReturnsAsync(user);
        _mockDiscussionRepo.Setup(r => r.GetActivityByDateAsync(user.PublicId, It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _mockPostRepo.Setup(r => r.GetActivityByDateAsync(user.PublicId, It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        // Act
        var result = await _useCase.GetUserActivityHistoryAsync(user.PublicId.Value, 7);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        var data = result.Value!.Data.ToList();
        for (var i = 1; i < data.Count; i++)
        {
            await Assert.That(data[i].Date).IsGreaterThanOrEqualTo(data[i - 1].Date);
        }
    }

    #endregion

    #region GetPlatformStatsAsync Tests

    [Test]
    public async Task GetPlatformStatsAsync_ReturnsStats()
    {
        // Arrange
        var stats = new PlatformStatsDto(HubCount: 3, SpaceCount: 15, DiscussionCount: 500, ReplyCount: 5000);

        _mockStatsRepo.Setup(r => r.GetPlatformStatsAsync())
            .ReturnsAsync(stats);

        // Act
        var result = await _useCase.GetPlatformStatsAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.HubCount).IsEqualTo(3);
        await Assert.That(result.SpaceCount).IsEqualTo(15);
        await Assert.That(result.DiscussionCount).IsEqualTo(500);
        await Assert.That(result.ReplyCount).IsEqualTo(5000);
    }

    #endregion

    #region GetHubStatsAsync Tests

    [Test]
    public async Task GetHubStatsAsync_WithExistingHub_ReturnsStats()
    {
        // Arrange
        var stats = new HubStatsDto("hub-1", "Main Hub", "Description", SpaceCount: 5, DiscussionCount: 100, ReplyCount: 1000);

        _mockStatsRepo.Setup(r => r.GetHubStatsAsync("hub-1"))
            .ReturnsAsync(stats);

        // Act
        var result = await _useCase.GetHubStatsAsync("hub-1");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo("Main Hub");
        await Assert.That(result.Value.SpaceCount).IsEqualTo(5);
    }

    [Test]
    public async Task GetHubStatsAsync_WithNonExistentHub_ReturnsFailure()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetHubStatsAsync("non-existent"))
            .ReturnsAsync((HubStatsDto?)null);

        // Act
        var result = await _useCase.GetHubStatsAsync("non-existent");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Hub not found");
    }

    #endregion

    #region GetSpaceStatsAsync Tests

    [Test]
    public async Task GetSpaceStatsAsync_WithExistingSpace_ReturnsStats()
    {
        // Arrange
        var stats = new SpaceStatsDto("space-1", "General", null, DiscussionCount: 50, ReplyCount: 500, FollowerCount: 25);

        _mockStatsRepo.Setup(r => r.GetSpaceStatsAsync("space-1"))
            .ReturnsAsync(stats);

        // Act
        var result = await _useCase.GetSpaceStatsAsync("space-1");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo("General");
        await Assert.That(result.Value.DiscussionCount).IsEqualTo(50);
    }

    [Test]
    public async Task GetSpaceStatsAsync_WithNonExistentSpace_ReturnsFailure()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetSpaceStatsAsync("non-existent"))
            .ReturnsAsync((SpaceStatsDto?)null);

        // Act
        var result = await _useCase.GetSpaceStatsAsync("non-existent");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Space not found");
    }

    #endregion

    #region GetCommunityStatsAsync Tests

    [Test]
    public async Task GetCommunityStatsAsync_WithExistingCommunity_ReturnsStats()
    {
        // Arrange
        var stats = new CommunityStatsDto("community-1", "Test Community", "Desc",
            HubCount: 2, SpaceCount: 10, DiscussionCount: 200, ReplyCount: 2000);

        _mockStatsRepo.Setup(r => r.GetCommunityStatsAsync("community-1"))
            .ReturnsAsync(stats);

        // Act
        var result = await _useCase.GetCommunityStatsAsync("community-1");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo("Test Community");
        await Assert.That(result.Value.HubCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetCommunityStatsAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetCommunityStatsAsync("non-existent"))
            .ReturnsAsync((CommunityStatsDto?)null);

        // Act
        var result = await _useCase.GetCommunityStatsAsync("non-existent");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Community not found");
    }

    #endregion

    #region GetUserStatsAsync Tests

    [Test]
    public async Task GetUserStatsAsync_WithExistingUser_ReturnsStats()
    {
        // Arrange
        var stats = new UserStatsDto("user-1", "TestUser",
            DiscussionCount: 20, ReplyCount: 150, FollowerCount: 10);

        _mockStatsRepo.Setup(r => r.GetUserStatsAsync("user-1"))
            .ReturnsAsync(stats);

        // Act
        var result = await _useCase.GetUserStatsAsync("user-1");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DisplayName).IsEqualTo("TestUser");
        await Assert.That(result.Value.DiscussionCount).IsEqualTo(20);
    }

    [Test]
    public async Task GetUserStatsAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetUserStatsAsync("non-existent"))
            .ReturnsAsync((UserStatsDto?)null);

        // Act
        var result = await _useCase.GetUserStatsAsync("non-existent");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region GetDiscussionStatsAsync Tests

    [Test]
    public async Task GetDiscussionStatsAsync_WithExistingDiscussion_ReturnsStats()
    {
        // Arrange
        var stats = new DiscussionStatsDto("disc-1", "Active Discussion", ReplyCount: 45, FollowerCount: 12);

        _mockStatsRepo.Setup(r => r.GetDiscussionStatsAsync("disc-1"))
            .ReturnsAsync(stats);

        // Act
        var result = await _useCase.GetDiscussionStatsAsync("disc-1");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo("Active Discussion");
        await Assert.That(result.Value.ReplyCount).IsEqualTo(45);
    }

    [Test]
    public async Task GetDiscussionStatsAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetDiscussionStatsAsync("non-existent"))
            .ReturnsAsync((DiscussionStatsDto?)null);

        // Act
        var result = await _useCase.GetDiscussionStatsAsync("non-existent");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
    }

    #endregion

    #region GetTopActiveSpacesTodayAsync Tests

    [Test]
    public async Task GetTopActiveSpacesTodayAsync_ReturnsResults()
    {
        // Arrange
        var spaces = new List<TopActiveSpaceDto>
        {
            new("space-1", "General", "general", 20, "hub-1", "main", "Main Hub"),
            new("space-2", "Off Topic", "off-topic", 15, "hub-1", "main", "Main Hub")
        };

        _mockStatsRepo.Setup(r => r.GetTopActiveSpacesTodayAsync(null, null, 5))
            .ReturnsAsync(spaces);

        // Act
        var result = await _useCase.GetTopActiveSpacesTodayAsync();

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Name).IsEqualTo("General");
        await Assert.That(result[0].PostCountToday).IsEqualTo(20);
    }

    [Test]
    public async Task GetTopActiveSpacesTodayAsync_WithHubFilter_PassesHubId()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetTopActiveSpacesTodayAsync("hub-1", null, 5))
            .ReturnsAsync([]);

        // Act
        await _useCase.GetTopActiveSpacesTodayAsync(hubId: "hub-1");

        // Assert
        _mockStatsRepo.Verify(r => r.GetTopActiveSpacesTodayAsync("hub-1", null, 5), Times.Once);
    }

    [Test]
    public async Task GetTopActiveSpacesTodayAsync_WithCommunityFilter_PassesCommunityId()
    {
        // Arrange
        _mockStatsRepo.Setup(r => r.GetTopActiveSpacesTodayAsync(null, "community-1", 5))
            .ReturnsAsync([]);

        // Act
        await _useCase.GetTopActiveSpacesTodayAsync(communityId: "community-1");

        // Assert
        _mockStatsRepo.Verify(r => r.GetTopActiveSpacesTodayAsync(null, "community-1", 5), Times.Once);
    }

    #endregion
}
