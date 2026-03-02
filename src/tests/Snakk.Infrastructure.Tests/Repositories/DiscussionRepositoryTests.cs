using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class DiscussionRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly DiscussionRepository _repository;

    public DiscussionRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new DiscussionRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingDiscussion_ReturnsWithSpaceAndCreatedByUser()
    {
        var (user, _, _, space, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByIdAsync(discussion.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(discussion.Id);
        await Assert.That(result.Space).IsNotNull();
        await Assert.That(result.Space.Id).IsEqualTo(space.Id);
        await Assert.That(result.CreatedByUser).IsNotNull();
        await Assert.That(result.CreatedByUser.Id).IsEqualTo(user.Id);
    }

    [Test]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetForUpdateAsync Tests

    [Test]
    public async Task GetForUpdateAsync_ExistingDiscussion_ReturnsWithNavigationsLoaded()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForUpdateAsync(discussion.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(result.Space).IsNotNull();
        await Assert.That(result.CreatedByUser).IsNotNull();
    }

    [Test]
    public async Task GetForUpdateAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetForUpdateAsync("disc_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_MultipleDiscussions_ReturnsAllWithIncludes()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Second Discussion");

        var result = (await _repository.GetAllAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Space).IsNotNull();
        await Assert.That(result[0].CreatedByUser).IsNotNull();
        await Assert.That(result[1].Space).IsNotNull();
        await Assert.That(result[1].CreatedByUser).IsNotNull();
    }

    #endregion

    #region GetForDisplayAsync Tests

    [Test]
    public async Task GetForDisplayAsync_ExistingDiscussion_ReturnsDtoWithCorrectFields()
    {
        var (user, _, _, space, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForDisplayAsync(discussion.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(result.Title).IsEqualTo(discussion.Title);
        await Assert.That(result.Slug).IsEqualTo(discussion.Slug);
        await Assert.That(result.SpacePublicId).IsEqualTo(space.PublicId);
        await Assert.That(result.SpaceName).IsEqualTo(space.Name);
        await Assert.That(result.CreatedByUserPublicId).IsEqualTo(user.PublicId);
        await Assert.That(result.CreatedByUserDisplayName).IsEqualTo(user.DisplayName);
    }

    [Test]
    public async Task GetForDisplayAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetForDisplayAsync("disc_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingDiscussion_ReturnsEntity()
    {
        var (_, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByPublicIdAsync(discussion.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(result.Space).IsNotNull();
        await Assert.That(result.CreatedByUser).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("disc_nonexistent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySlugAsync Tests

    [Test]
    public async Task GetBySlugAsync_ExistingDiscussion_ReturnsEntity()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Slug Test", "slug-test");

        var result = await _repository.GetBySlugAsync("slug-test");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Slug).IsEqualTo("slug-test");
        await Assert.That(result.Title).IsEqualTo("Slug Test");
        await Assert.That(result.Space).IsNotNull();
        await Assert.That(result.CreatedByUser).IsNotNull();
    }

    [Test]
    public async Task GetBySlugAsync_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetBySlugAsync("nonexistent-slug");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetBySpaceIdAsync Tests

    [Test]
    public async Task GetBySpaceIdAsync_MultipleDiscussions_ReturnsOnlyForSpace()
    {
        var (user, community, hub, space, _, _) = await _builder.CreateFullHierarchyAsync();
        var otherSpace = await _builder.CreateSpaceAsync(hub.Id, "Other Space");
        await _builder.CreateDiscussionAsync(space.Id, user.Id, "Discussion In Space");
        await _builder.CreateDiscussionAsync(otherSpace.Id, user.Id, "Discussion In Other Space");

        var result = (await _repository.GetBySpaceIdAsync(space.Id)).ToList();

        // The original hierarchy creates one discussion + one more = 2 for this space
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(d => d.SpaceId == space.Id)).IsTrue();
    }

    [Test]
    public async Task GetBySpaceIdAsync_PinnedDiscussionAppearsFirst()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();

        // Create an older unpinned discussion
        var unpinned = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Unpinned Discussion");
        unpinned.LastActivityAt = DateTime.UtcNow.AddHours(1);
        await _db.Context.SaveChangesAsync();

        // Create a pinned discussion with older activity
        var pinned = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Pinned Discussion");
        pinned.IsPinned = true;
        pinned.LastActivityAt = DateTime.UtcNow.AddHours(-1);
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetBySpaceIdAsync(space.Id)).ToList();

        // Pinned should be first regardless of LastActivityAt
        await Assert.That(result[0].IsPinned).IsTrue();
        await Assert.That(result[0].Title).IsEqualTo("Pinned Discussion");
    }

    #endregion

    #region GetPagedBySpaceIdAsync Tests

    [Test]
    public async Task GetPagedBySpaceIdAsync_Pagination_HasMoreItemsCorrect()
    {
        var (user, _, _, space, _, _) = await _builder.CreateFullHierarchyAsync();

        // Create additional discussions (hierarchy already has 1)
        for (var i = 0; i < 3; i++)
        {
            var disc = await _builder.CreateDiscussionAsync(space.Id, user.Id, $"Discussion {i:D2}");
            disc.LastActivityAt = DateTime.UtcNow.AddMinutes(-i);
            await _db.Context.SaveChangesAsync();
        }

        // Total = 4 discussions (1 from hierarchy + 3 new), pageSize = 2
        var result = await _repository.GetPagedBySpaceIdAsync(space.Id, 0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.PageSize).IsEqualTo(2);
    }

    [Test]
    public async Task GetPagedBySpaceIdAsync_EmptySpace_ReturnsEmptyResult()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var emptySpace = await _builder.CreateSpaceAsync(hub.Id, "Empty Space");

        var result = await _repository.GetPagedBySpaceIdAsync(emptySpace.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetRecentAsync Tests

    [Test]
    public async Task GetRecentAsync_ReturnsCorrectCountOrderedByMostRecent()
    {
        var (user, _, _, space, hierarchyDiscussion, _) = await _builder.CreateFullHierarchyAsync();

        // Push the hierarchy discussion far into the past so it does not interfere
        hierarchyDiscussion.LastActivityAt = DateTime.UtcNow.AddDays(-10);
        await _db.Context.SaveChangesAsync();

        // Create discussions with different LastActivityAt
        var oldest = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Oldest");
        oldest.LastActivityAt = DateTime.UtcNow.AddHours(-3);
        await _db.Context.SaveChangesAsync();

        var middle = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Middle");
        middle.LastActivityAt = DateTime.UtcNow.AddHours(-1);
        await _db.Context.SaveChangesAsync();

        var newest = await _builder.CreateDiscussionAsync(space.Id, user.Id, "Newest");
        newest.LastActivityAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetRecentAsync(2)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Title).IsEqualTo("Newest");
        await Assert.That(result[1].Title).IsEqualTo("Middle");
    }

    #endregion
}
