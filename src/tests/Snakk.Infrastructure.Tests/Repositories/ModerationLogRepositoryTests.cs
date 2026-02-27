using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

namespace Snakk.Infrastructure.Tests.Repositories;

public class ModerationLogRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ModerationLogRepository _repository;

    public ModerationLogRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new ModerationLogRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingLog_ReturnsWithNavigationProperties()
    {
        var actor = await _builder.CreateUserAsync("Moderator");
        var targetUser = await _builder.CreateUserAsync("TargetUser");
        var (_, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var log = await _builder.CreateModerationLogAsync(
            actor.Id, 1,
            targetPostId: post.Id,
            targetDiscussionId: discussion.Id,
            targetUserId: targetUser.Id,
            communityId: community.Id,
            hubId: hub.Id,
            spaceId: space.Id,
            reason: "Spam content",
            details: "Removed spam post");

        var result = await _repository.GetByPublicIdAsync(log.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(log.PublicId);
        await Assert.That(result.ActorUser).IsNotNull();
        await Assert.That(result.ActorUser.DisplayName).IsEqualTo("Moderator");
        await Assert.That(result.TargetPost).IsNotNull();
        await Assert.That(result.TargetDiscussion).IsNotNull();
        await Assert.That(result.TargetUser).IsNotNull();
        await Assert.That(result.TargetUser!.DisplayName).IsEqualTo("TargetUser");
        await Assert.That(result.Community).IsNotNull();
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Space).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetLogsForCommunityAsync Tests

    [Test]
    public async Task GetLogsForCommunityAsync_ReturnsLogsForCommunityOnly()
    {
        var actor = await _builder.CreateUserAsync("Moderator");
        var community1 = await _builder.CreateCommunityAsync("Community One");
        var community2 = await _builder.CreateCommunityAsync("Community Two");

        await _builder.CreateModerationLogAsync(actor.Id, 1, communityId: community1.Id);
        await _builder.CreateModerationLogAsync(actor.Id, 1, communityId: community1.Id);
        await _builder.CreateModerationLogAsync(actor.Id, 1, communityId: community2.Id);

        var result = await _repository.GetLogsForCommunityAsync(community1.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetLogsForCommunityAsync_Pagination_ReturnsCorrectPage()
    {
        var actor = await _builder.CreateUserAsync("Moderator");
        var community = await _builder.CreateCommunityAsync();

        for (int i = 0; i < 5; i++)
        {
            await _builder.CreateModerationLogAsync(actor.Id, 1, communityId: community.Id);
        }

        var result = await _repository.GetLogsForCommunityAsync(community.Id, 0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(2);

        var result2 = await _repository.GetLogsForCommunityAsync(community.Id, 2, 2);

        await Assert.That(result2.Items.Count()).IsEqualTo(2);
        await Assert.That(result2.HasMoreItems).IsTrue();

        var result3 = await _repository.GetLogsForCommunityAsync(community.Id, 4, 2);

        await Assert.That(result3.Items.Count()).IsEqualTo(1);
        await Assert.That(result3.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetLogsForCommunityAsync_EmptyCommunity_ReturnsEmptyResult()
    {
        var community = await _builder.CreateCommunityAsync();

        var result = await _repository.GetLogsForCommunityAsync(community.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    [Test]
    public async Task GetLogsForCommunityAsync_DtoMapsFieldsCorrectly()
    {
        var actor = await _builder.CreateUserAsync("ActorName");
        var targetUser = await _builder.CreateUserAsync("TargetName");
        var community = await _builder.CreateCommunityAsync("TestCommunity");
        var hub = await _builder.CreateHubAsync(community.Id, "TestHub");
        var space = await _builder.CreateSpaceAsync(hub.Id, "TestSpace");
        var discussion = await _builder.CreateDiscussionAsync(space.Id, actor.Id, "TestDiscussion");
        var post = await _builder.CreatePostAsync(discussion.Id, actor.Id, isFirstPost: true);

        await _builder.CreateModerationLogAsync(
            actor.Id,
            (int)ModerationActionEnum.DeletePost,
            targetPostId: post.Id,
            targetDiscussionId: discussion.Id,
            targetUserId: targetUser.Id,
            communityId: community.Id,
            hubId: hub.Id,
            spaceId: space.Id,
            reason: "Test reason",
            details: "Test details");

        var result = await _repository.GetLogsForCommunityAsync(community.Id, 0, 10);
        var dto = result.Items.First();

        await Assert.That(dto.ActorUserPublicId).IsEqualTo(actor.PublicId);
        await Assert.That(dto.ActorUserDisplayName).IsEqualTo("ActorName");
        await Assert.That(dto.Action).IsEqualTo(ModerationActionEnum.DeletePost.ToString());
        await Assert.That(dto.TargetPostPublicId).IsEqualTo(post.PublicId);
        await Assert.That(dto.TargetDiscussionPublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(dto.TargetDiscussionTitle).IsEqualTo("TestDiscussion");
        await Assert.That(dto.TargetUserPublicId).IsEqualTo(targetUser.PublicId);
        await Assert.That(dto.TargetUserDisplayName).IsEqualTo("TargetName");
        await Assert.That(dto.CommunityPublicId).IsEqualTo(community.PublicId);
        await Assert.That(dto.CommunityName).IsEqualTo("TestCommunity");
        await Assert.That(dto.HubPublicId).IsEqualTo(hub.PublicId);
        await Assert.That(dto.HubName).IsEqualTo("TestHub");
        await Assert.That(dto.SpacePublicId).IsEqualTo(space.PublicId);
        await Assert.That(dto.SpaceName).IsEqualTo("TestSpace");
        await Assert.That(dto.Details).IsEqualTo("Test details");
        await Assert.That(dto.Reason).IsEqualTo("Test reason");
    }

    #endregion

    #region GetLogsByActorAsync Tests

    [Test]
    public async Task GetLogsByActorAsync_ReturnsLogsBySpecificActor()
    {
        var actor1 = await _builder.CreateUserAsync("Actor1");
        var actor2 = await _builder.CreateUserAsync("Actor2");
        var community = await _builder.CreateCommunityAsync();

        await _builder.CreateModerationLogAsync(actor1.Id, 1, communityId: community.Id);
        await _builder.CreateModerationLogAsync(actor1.Id, 1, communityId: community.Id);
        await _builder.CreateModerationLogAsync(actor2.Id, 1, communityId: community.Id);

        var result = await _repository.GetLogsByActorAsync(actor1.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        foreach (var dto in result.Items)
        {
            await Assert.That(dto.ActorUserPublicId).IsEqualTo(actor1.PublicId);
        }
    }

    [Test]
    public async Task GetLogsByActorAsync_NoLogs_ReturnsEmptyResult()
    {
        var actor = await _builder.CreateUserAsync();

        var result = await _repository.GetLogsByActorAsync(actor.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
    }

    #endregion

    #region GetLogsForTargetUserAsync Tests

    [Test]
    public async Task GetLogsForTargetUserAsync_ReturnsLogsTargetingSpecificUser()
    {
        var actor = await _builder.CreateUserAsync("Actor");
        var target1 = await _builder.CreateUserAsync("Target1");
        var target2 = await _builder.CreateUserAsync("Target2");
        var community = await _builder.CreateCommunityAsync();

        await _builder.CreateModerationLogAsync(actor.Id, 1, targetUserId: target1.Id, communityId: community.Id);
        await _builder.CreateModerationLogAsync(actor.Id, 1, targetUserId: target1.Id, communityId: community.Id);
        await _builder.CreateModerationLogAsync(actor.Id, 1, targetUserId: target2.Id, communityId: community.Id);

        var result = await _repository.GetLogsForTargetUserAsync(target1.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        foreach (var dto in result.Items)
        {
            await Assert.That(dto.TargetUserPublicId).IsEqualTo(target1.PublicId);
        }
    }

    [Test]
    public async Task GetLogsForTargetUserAsync_NoLogsForUser_ReturnsEmptyResult()
    {
        var target = await _builder.CreateUserAsync();

        var result = await _repository.GetLogsForTargetUserAsync(target.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
    }

    #endregion
}
