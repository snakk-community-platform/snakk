using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class StatisticsUseCaseTests
{
    private readonly IPostRepository _postRepo = Substitute.For<IPostRepository>();
    private readonly IDiscussionRepository _discussionRepo = Substitute.For<IDiscussionRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IStatsRepository _statsRepo = Substitute.For<IStatsRepository>();
    private readonly IManageScopeDataService _manageScopeData = Substitute.For<IManageScopeDataService>();
    private StatisticsUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new StatisticsUseCase(_postRepo, _discussionRepo, _userRepo, _statsRepo, _manageScopeData);
    }

    #region GetTopContributorsTodayAsync Tests

    [Test]
    public async Task GetTopContributorsTodayAsync_WithContributors_ReturnsEnrichedResults()
    {
        var userId1 = UserId.New();
        var userId2 = UserId.New();
        var topContributors = new List<(UserId UserId, int PostCount)> { (userId1, 10), (userId2, 5) };

        _postRepo.GetTopContributorsSinceAsync(Arg.Any<DateTime>(), null, null, null, 5).Returns(topContributors);
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([
            new UserAvatarSlim(userId1.Value, "TopUser1", "avatar1.png", null, null, 0),
            new UserAvatarSlim(userId2.Value, "TopUser2", null, null, null, 0)
        ]);

        var result = await _useCase.GetTopContributorsTodayAsync(DateTime.UtcNow.AddHours(-24));

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
        var userId = UserId.New();
        var topContributors = new List<(UserId UserId, int PostCount)> { (userId, 3) };

        _postRepo.GetTopContributorsSinceAsync(Arg.Any<DateTime>(), null, null, null, 5).Returns(topContributors);
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([]);

        var result = await _useCase.GetTopContributorsTodayAsync(DateTime.UtcNow.AddHours(-24));

        await Assert.That(result.IsSuccess).IsTrue();
        var items = result.Value!.Items.ToList();
        await Assert.That(items[0].DisplayName).IsEqualTo("Deleted User");
    }

    [Test]
    public async Task GetTopContributorsTodayAsync_WithHubFilter_PassesHubId()
    {
        const string hubId = "hub-123";
        _postRepo.GetTopContributorsSinceAsync(Arg.Any<DateTime>(), HubId.From(hubId), null, null, 5).Returns([]);
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([]);

        await _useCase.GetTopContributorsTodayAsync(DateTime.UtcNow.AddHours(-24), hubId: hubId);

        await _postRepo.Received(1).GetTopContributorsSinceAsync(Arg.Any<DateTime>(), Arg.Is<HubId>(h => h.Value == hubId), null, null, 5);
    }

    [Test]
    public async Task GetTopContributorsTodayAsync_WithCustomLimit_PassesLimit()
    {
        _postRepo.GetTopContributorsSinceAsync(Arg.Any<DateTime>(), null, null, null, 10).Returns([]);
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([]);

        await _useCase.GetTopContributorsTodayAsync(DateTime.UtcNow.AddHours(-24), limit: 10);

        await _postRepo.Received(1).GetTopContributorsSinceAsync(Arg.Any<DateTime>(), null, null, null, 10);
    }

    #endregion

    #region GetTopActiveDiscussionsTodayAsync Tests

    [Test]
    public async Task GetTopActiveDiscussionsTodayAsync_ReturnsResults()
    {
        var topDiscussions = new List<TopActiveDiscussion>
        {
            new(DiscussionId.New(), "Hot Discussion", "hot-discussion", 15, "space-1", "general", "General", "hub-1", "main", "Main Hub", "user-1", "Author1", "test-community"),
            new(DiscussionId.New(), "Another Active", "another-active", 8, "space-2", "off-topic", "Off Topic", "hub-1", "main", "Main Hub", "user-2", "Author2", "test-community")
        };
        _discussionRepo.GetTopActiveDiscussionsSinceAsync(Arg.Any<DateTime>(), null, null, null, 5, null).Returns(topDiscussions);

        var result = await _useCase.GetTopActiveDiscussionsTodayAsync(DateTime.UtcNow.AddHours(-24));

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
        const string spaceId = "space-123";
        _discussionRepo.GetTopActiveDiscussionsSinceAsync(Arg.Any<DateTime>(), null, SpaceId.From(spaceId), null, 5, null).Returns([]);

        await _useCase.GetTopActiveDiscussionsTodayAsync(DateTime.UtcNow.AddHours(-24), spaceId: spaceId);

        await _discussionRepo.Received(1).GetTopActiveDiscussionsSinceAsync(Arg.Any<DateTime>(), null, Arg.Is<SpaceId>(s => s.Value == spaceId), null, 5, null);
    }

    #endregion

    #region GetUserActivityHistoryAsync Tests

    [Test]
    public async Task GetUserActivityHistoryAsync_WithExistingUser_ReturnsActivityData()
    {
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var userId = user.PublicId;
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([new UserAvatarSlim(userId.Value, "TestUser", null, null, null, 0)]);

        var today = DateTime.UtcNow.Date;
        var discussionActivity = new List<(DateTime Date, int Count)> { (today, 2), (today.AddDays(-1), 1) };
        var postActivity = new List<(DateTime Date, int Count)> { (today, 5), (today.AddDays(-2), 3) };

        _discussionRepo.GetActivityByDateAsync(userId, Arg.Any<DateTime>()).Returns(discussionActivity);
        _postRepo.GetActivityByDateAsync(userId, Arg.Any<DateTime>()).Returns(postActivity);

        var result = await _useCase.GetUserActivityHistoryAsync(userId.Value, 30);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Days).IsEqualTo(30);
        await Assert.That(result.Value.Data).Count().IsEqualTo(30);

        var todayData = result.Value.Data.FirstOrDefault(d => d.Date == today);
        await Assert.That(todayData).IsNotNull();
        await Assert.That(todayData!.Discussions).IsEqualTo(2);
        await Assert.That(todayData.Posts).IsEqualTo(5);
        await Assert.That(todayData.Total).IsEqualTo(7);
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_WithNonExistentUser_ReturnsFailure()
    {
        var userId = UserId.New();
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([]);

        var result = await _useCase.GetUserActivityHistoryAsync(userId.Value);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_WithInvalidDays_DefaultsTo30()
    {
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([new UserAvatarSlim(user.PublicId.Value, "TestUser", null, null, null, 0)]);
        _discussionRepo.GetActivityByDateAsync(user.PublicId, Arg.Any<DateTime>()).Returns([]);
        _postRepo.GetActivityByDateAsync(user.PublicId, Arg.Any<DateTime>()).Returns([]);

        var result = await _useCase.GetUserActivityHistoryAsync(user.PublicId.Value, 0);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Days).IsEqualTo(30);
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_WithOver90Days_DefaultsTo30()
    {
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([new UserAvatarSlim(user.PublicId.Value, "TestUser", null, null, null, 0)]);
        _discussionRepo.GetActivityByDateAsync(user.PublicId, Arg.Any<DateTime>()).Returns([]);
        _postRepo.GetActivityByDateAsync(user.PublicId, Arg.Any<DateTime>()).Returns([]);

        var result = await _useCase.GetUserActivityHistoryAsync(user.PublicId.Value, 400);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Days).IsEqualTo(30);
    }

    [Test]
    public async Task GetUserActivityHistoryAsync_DataIsSortedByDate()
    {
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        _userRepo.GetAvatarSlimByPublicIdsAsync(Arg.Any<IEnumerable<UserId>>()).Returns([new UserAvatarSlim(user.PublicId.Value, "TestUser", null, null, null, 0)]);
        _discussionRepo.GetActivityByDateAsync(user.PublicId, Arg.Any<DateTime>()).Returns([]);
        _postRepo.GetActivityByDateAsync(user.PublicId, Arg.Any<DateTime>()).Returns([]);

        var result = await _useCase.GetUserActivityHistoryAsync(user.PublicId.Value, 7);

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
        var stats = new PlatformStatsDto(HubCount: 3, SpaceCount: 15, DiscussionCount: 500, ReplyCount: 5000);
        _statsRepo.GetPlatformStatsAsync().Returns(stats);

        var result = await _useCase.GetPlatformStatsAsync();

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
        var stats = new HubStatsDto("hub-1", "Main Hub", "Description", SpaceCount: 5, DiscussionCount: 100, ReplyCount: 1000);
        _statsRepo.GetHubStatsAsync("hub-1").Returns(stats);

        var result = await _useCase.GetHubStatsAsync("hub-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo("Main Hub");
        await Assert.That(result.Value.SpaceCount).IsEqualTo(5);
    }

    [Test]
    public async Task GetHubStatsAsync_WithNonExistentHub_ReturnsFailure()
    {
        _statsRepo.GetHubStatsAsync("non-existent").Returns((HubStatsDto?)null);

        var result = await _useCase.GetHubStatsAsync("non-existent");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Hub not found");
    }

    #endregion

    #region GetSpaceStatsAsync Tests

    [Test]
    public async Task GetSpaceStatsAsync_WithExistingSpace_ReturnsStats()
    {
        var stats = new SpaceStatsDto("space-1", "General", null, DiscussionCount: 50, ReplyCount: 500, FollowerCount: 25);
        _statsRepo.GetSpaceStatsAsync("space-1").Returns(stats);

        var result = await _useCase.GetSpaceStatsAsync("space-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo("General");
        await Assert.That(result.Value.DiscussionCount).IsEqualTo(50);
    }

    [Test]
    public async Task GetSpaceStatsAsync_WithNonExistentSpace_ReturnsFailure()
    {
        _statsRepo.GetSpaceStatsAsync("non-existent").Returns((SpaceStatsDto?)null);

        var result = await _useCase.GetSpaceStatsAsync("non-existent");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Space not found");
    }

    #endregion

    #region GetCommunityStatsAsync Tests

    [Test]
    public async Task GetCommunityStatsAsync_WithExistingCommunity_ReturnsStats()
    {
        var stats = new CommunityStatsDto("community-1", "Test Community", "Desc", HubCount: 2, SpaceCount: 10, DiscussionCount: 200, ReplyCount: 2000);
        _statsRepo.GetCommunityStatsAsync("community-1").Returns(stats);

        var result = await _useCase.GetCommunityStatsAsync("community-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Name).IsEqualTo("Test Community");
        await Assert.That(result.Value.HubCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetCommunityStatsAsync_WithNonExistentCommunity_ReturnsFailure()
    {
        _statsRepo.GetCommunityStatsAsync("non-existent").Returns((CommunityStatsDto?)null);

        var result = await _useCase.GetCommunityStatsAsync("non-existent");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Community not found");
    }

    #endregion

    #region GetUserStatsAsync Tests

    [Test]
    public async Task GetUserStatsAsync_WithExistingUser_ReturnsStats()
    {
        var stats = new UserStatsDto("user-1", "TestUser", DiscussionCount: 20, ReplyCount: 150, FollowerCount: 10);
        _statsRepo.GetUserStatsAsync("user-1").Returns(stats);

        var result = await _useCase.GetUserStatsAsync("user-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DisplayName).IsEqualTo("TestUser");
        await Assert.That(result.Value.DiscussionCount).IsEqualTo(20);
    }

    [Test]
    public async Task GetUserStatsAsync_WithNonExistentUser_ReturnsFailure()
    {
        _statsRepo.GetUserStatsAsync("non-existent").Returns((UserStatsDto?)null);

        var result = await _useCase.GetUserStatsAsync("non-existent");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User not found");
    }

    #endregion

    #region GetDiscussionStatsAsync Tests

    [Test]
    public async Task GetDiscussionStatsAsync_WithExistingDiscussion_ReturnsStats()
    {
        var stats = new DiscussionStatsDto("disc-1", "Active Discussion", ReplyCount: 45, FollowerCount: 12);
        _statsRepo.GetDiscussionStatsAsync("disc-1").Returns(stats);

        var result = await _useCase.GetDiscussionStatsAsync("disc-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo("Active Discussion");
        await Assert.That(result.Value.ReplyCount).IsEqualTo(45);
    }

    [Test]
    public async Task GetDiscussionStatsAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        _statsRepo.GetDiscussionStatsAsync("non-existent").Returns((DiscussionStatsDto?)null);

        var result = await _useCase.GetDiscussionStatsAsync("non-existent");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
    }

    #endregion

    #region GetTopActiveSpacesTodayAsync Tests

    [Test]
    public async Task GetTopActiveSpacesTodayAsync_ReturnsResults()
    {
        var spaces = new List<TopActiveSpaceDto>
        {
            new("space-1", "General", "general", 20, "hub-1", "main", "Main Hub", "test-community"),
            new("space-2", "Off Topic", "off-topic", 15, "hub-1", "main", "Main Hub", "test-community")
        };
        _statsRepo.GetTopActiveSpacesSinceAsync(Arg.Any<DateTime>(), null, null, 5).Returns(spaces);

        var result = await _useCase.GetTopActiveSpacesTodayAsync(DateTime.UtcNow.AddHours(-24));

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Name).IsEqualTo("General");
        await Assert.That(result[0].PostCountToday).IsEqualTo(20);
    }

    [Test]
    public async Task GetTopActiveSpacesTodayAsync_WithHubFilter_PassesHubId()
    {
        _statsRepo.GetTopActiveSpacesSinceAsync(Arg.Any<DateTime>(), "hub-1", null, 5).Returns([]);

        await _useCase.GetTopActiveSpacesTodayAsync(DateTime.UtcNow.AddHours(-24), hubId: "hub-1");

        await _statsRepo.Received(1).GetTopActiveSpacesSinceAsync(Arg.Any<DateTime>(), "hub-1", null, 5);
    }

    [Test]
    public async Task GetTopActiveSpacesTodayAsync_WithCommunityFilter_PassesCommunityId()
    {
        _statsRepo.GetTopActiveSpacesSinceAsync(Arg.Any<DateTime>(), null, "community-1", 5).Returns([]);

        await _useCase.GetTopActiveSpacesTodayAsync(DateTime.UtcNow.AddHours(-24), communityId: "community-1");

        await _statsRepo.Received(1).GetTopActiveSpacesSinceAsync(Arg.Any<DateTime>(), null, "community-1", 5);
    }

    #endregion
}
