using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class SoftDeleteQueryFilterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;

    public SoftDeleteQueryFilterTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    [Test]
    public async Task SoftDeletedCommunity_IsExcludedFromQuery()
    {
        var community = await _builder.CreateCommunityAsync();
        community.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var communities = await readContext.Communities.ToListAsync();

        await Assert.That(communities.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SoftDeletedHub_IsExcludedFromQuery()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        hub.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var hubs = await readContext.Hubs.ToListAsync();

        await Assert.That(hubs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SoftDeletedSpace_IsExcludedFromQuery()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        space.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var spaces = await readContext.Spaces.ToListAsync();

        await Assert.That(spaces.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SoftDeletedDiscussion_IsExcludedFromQuery()
    {
        var (user, community, hub, space, discussion, _) = await _builder.CreateFullHierarchyAsync();
        discussion.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var discussions = await readContext.Discussions.ToListAsync();

        await Assert.That(discussions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SoftDeletedPost_IsExcludedFromQuery()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        post.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var posts = await readContext.Posts.ToListAsync();

        await Assert.That(posts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SoftDeletedUser_IsExcludedFromQuery()
    {
        var user = await _builder.CreateUserAsync();
        user.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var users = await readContext.Users.ToListAsync();

        await Assert.That(users.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SoftDeletedReportReason_IsExcludedFromQuery()
    {
        var reason = new ReportReasonDatabaseEntity
        {
            PublicId = $"rr_{Guid.NewGuid():N}",
            Name = "Test Reason",
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.ReportReasons.Add(reason);
        await _db.Context.SaveChangesAsync();

        reason.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var reasons = await readContext.ReportReasons.ToListAsync();

        await Assert.That(reasons.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IgnoreQueryFilters_ReturnsSoftDeletedEntities()
    {
        var community = await _builder.CreateCommunityAsync();
        community.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var communities = await readContext.Communities
            .IgnoreQueryFilters()
            .ToListAsync();

        await Assert.That(communities.Count).IsEqualTo(1);
        await Assert.That(communities[0].IsDeleted).IsTrue();
    }

    [Test]
    public async Task MixOfActiveAndDeleted_OnlyActiveReturned()
    {
        var activeCommunity = await _builder.CreateCommunityAsync(name: "Active Community");
        var deletedCommunity = await _builder.CreateCommunityAsync(name: "Deleted Community");
        deletedCommunity.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        using var readContext = _db.CreateSeparateContext();
        var communities = await readContext.Communities.ToListAsync();

        await Assert.That(communities.Count).IsEqualTo(1);
        await Assert.That(communities[0].Name).IsEqualTo("Active Community");
    }

    [Test]
    public async Task RestoreSoftDeletedEntity_BecomesVisibleAgain()
    {
        var user = await _builder.CreateUserAsync(displayName: "Restored User");

        // Soft delete the user
        user.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        // Verify it is excluded
        using var readContext1 = _db.CreateSeparateContext();
        var usersAfterDelete = await readContext1.Users.ToListAsync();
        await Assert.That(usersAfterDelete.Count).IsEqualTo(0);

        // Restore the user
        user.IsDeleted = false;
        await _db.Context.SaveChangesAsync();

        // Verify it is visible again
        using var readContext2 = _db.CreateSeparateContext();
        var usersAfterRestore = await readContext2.Users.ToListAsync();
        await Assert.That(usersAfterRestore.Count).IsEqualTo(1);
        await Assert.That(usersAfterRestore[0].DisplayName).IsEqualTo("Restored User");
    }
}
