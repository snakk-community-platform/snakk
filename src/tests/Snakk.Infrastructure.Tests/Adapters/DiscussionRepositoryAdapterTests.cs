using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Infrastructure.Tests.Adapters;

public class DiscussionRepositoryAdapterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly DiscussionRepositoryAdapter _adapter;

    public DiscussionRepositoryAdapterTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var databaseRepo = new DiscussionRepository(_db.Context);
        _adapter = new DiscussionRepositoryAdapter(databaseRepo, _db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingDiscussion_ReturnsMappedDomainEntity()
    {
        var (user, community, hub, space, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _adapter.GetByPublicIdAsync(DiscussionId.From(discussion.PublicId));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId.Value).IsEqualTo(discussion.PublicId);
        await Assert.That(result.Title).IsEqualTo(discussion.Title);
        await Assert.That(result.Slug).IsEqualTo(discussion.Slug);
        await Assert.That(result.SpaceId.Value).IsEqualTo(space.PublicId);
        await Assert.That(result.CreatedByUserId.Value).IsEqualTo(user.PublicId);
    }

    #endregion

    #region GetBySpaceIdAsync Tests

    [Test]
    public async Task GetBySpaceIdAsync_ExistingSpace_ResolvesPublicIdAndReturnsDiscussions()
    {
        var (user, community, hub, space, discussion, _) = await _builder.CreateFullHierarchyAsync();
        // Create a second discussion in the same space
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion", "second-discussion");

        var result = (await _adapter.GetBySpaceIdAsync(SpaceId.From(space.PublicId))).ToList();

        await Assert.That(result).HasCount().EqualTo(2);
    }

    [Test]
    public async Task GetBySpaceIdAsync_NonExistentSpace_ReturnsEmpty()
    {
        var result = (await _adapter.GetBySpaceIdAsync(SpaceId.From("nonexistent_space_id"))).ToList();

        await Assert.That(result).HasCount().EqualTo(0);
    }

    #endregion

    #region GetPagedBySpaceIdAsync Tests

    [Test]
    public async Task GetPagedBySpaceIdAsync_MultipleDiscussions_ReturnsPagedResultWithHasMoreItems()
    {
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Create 5 discussions
        for (int i = 1; i <= 5; i++)
        {
            await _builder.CreateDiscussionAsync(space.Id, user.Id, $"Discussion {i}", $"discussion-{i}");
        }

        // Request page of 3
        var result = await _adapter.GetPagedBySpaceIdAsync(SpaceId.From(space.PublicId), 0, 3);

        await Assert.That(result.Items.Count()).IsEqualTo(3);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.PageSize).IsEqualTo(3);
        await Assert.That(result.Offset).IsEqualTo(0);
    }

    #endregion

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_ValidDiscussion_ResolvesForeignKeysAndPersists()
    {
        var user = await _builder.CreateUserAsync("Author");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        var domainDiscussion = Discussion.Create(
            SpaceId.From(space.PublicId),
            UserId.From(user.PublicId),
            "New Discussion Title",
            "new-discussion-title");

        await _adapter.AddAsync(domainDiscussion);

        // Verify persisted using a separate context
        var verifyContext = _db.CreateSeparateContext();
        var persisted = verifyContext.Discussions.FirstOrDefault(d => d.PublicId == domainDiscussion.PublicId.Value);

        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.Title).IsEqualTo("New Discussion Title");
        await Assert.That(persisted.Slug).IsEqualTo("new-discussion-title");
        await Assert.That(persisted.SpaceId).IsEqualTo(space.Id);
        await Assert.That(persisted.CreatedByUserId).IsEqualTo(user.Id);
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ExistingDiscussion_PatchesFieldsCorrectly()
    {
        var (user, community, hub, space, discussion, _) = await _builder.CreateFullHierarchyAsync();

        // Get the domain entity
        var domainDiscussion = (await _adapter.GetByPublicIdAsync(DiscussionId.From(discussion.PublicId)))!;

        // Modify it - use the Unlock/Lock/Pin methods
        domainDiscussion.Pin();
        domainDiscussion.Lock();

        await _adapter.UpdateAsync(domainDiscussion);

        // Verify changes persisted
        var verifyContext = _db.CreateSeparateContext();
        var verified = verifyContext.Discussions.FirstOrDefault(d => d.Id == discussion.Id);

        await Assert.That(verified).IsNotNull();
        await Assert.That(verified!.IsPinned).IsTrue();
        await Assert.That(verified.IsLocked).IsTrue();
        await Assert.That(verified.LastModifiedAt).IsNotNull();
    }

    #endregion

    #region GetRecentAsync Tests

    [Test]
    public async Task GetRecentAsync_MultipleDiscussions_ReturnsRecentItems()
    {
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        for (int i = 1; i <= 5; i++)
        {
            await _builder.CreateDiscussionAsync(space.Id, user.Id, $"Discussion {i}", $"discussion-{i}");
        }

        var result = (await _adapter.GetRecentAsync(3)).ToList();

        await Assert.That(result).HasCount().EqualTo(3);
    }

    #endregion
}
