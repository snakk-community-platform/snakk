using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class UniqueConstraintTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;

    public UniqueConstraintTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    [Test]
    public async Task DuplicateUserPublicId_ThrowsOnSave()
    {
        var user1 = await _builder.CreateUserAsync();

        // Use a separate context to bypass EF change tracker's alternate key conflict detection
        using var insertContext = _db.CreateSeparateContext();
        insertContext.Users.Add(new UserDatabaseEntity
        {
            PublicId = user1.PublicId,
            DisplayName = "Duplicate User",
            Email = "unique@test.com",
            CreatedAt = DateTime.UtcNow
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateUserEmail_ThrowsOnSave()
    {
        await _builder.CreateUserAsync(email: "same@test.com");

        // Use a separate context to avoid change tracker conflicts
        using var insertContext = _db.CreateSeparateContext();
        insertContext.Users.Add(new UserDatabaseEntity
        {
            PublicId = $"u_{Guid.NewGuid():N}",
            DisplayName = "Another User",
            Email = "same@test.com",
            CreatedAt = DateTime.UtcNow
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateCommunitySlug_ThrowsOnSave()
    {
        await _builder.CreateCommunityAsync(slug: "my-community");

        using var insertContext = _db.CreateSeparateContext();
        insertContext.Communities.Add(new CommunityDatabaseEntity
        {
            PublicId = $"comm_{Guid.NewGuid():N}",
            Name = "Another Community",
            Slug = "my-community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateCommunityPublicId_ThrowsOnSave()
    {
        var community1 = await _builder.CreateCommunityAsync();

        using var insertContext = _db.CreateSeparateContext();
        insertContext.Communities.Add(new CommunityDatabaseEntity
        {
            PublicId = community1.PublicId,
            Name = "Another Community",
            Slug = $"community-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateHubPublicId_ThrowsOnSave()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub1 = await _builder.CreateHubAsync(community.Id);

        using var insertContext = _db.CreateSeparateContext();
        insertContext.Hubs.Add(new HubDatabaseEntity
        {
            PublicId = hub1.PublicId,
            CommunityId = community.Id,
            Name = "Duplicate Hub",
            Slug = $"hub-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateSpacePublicId_ThrowsOnSave()
    {
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id);

        using var insertContext = _db.CreateSeparateContext();
        insertContext.Spaces.Add(new SpaceDatabaseEntity
        {
            PublicId = space1.PublicId,
            HubId = hub.Id,
            Name = "Duplicate Space",
            Slug = $"space-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateDiscussionPublicId_ThrowsOnSave()
    {
        var (user, community, hub, space, discussion1, _) = await _builder.CreateFullHierarchyAsync();

        using var insertContext = _db.CreateSeparateContext();
        insertContext.Discussions.Add(new DiscussionDatabaseEntity
        {
            PublicId = discussion1.PublicId,
            SpaceId = space.Id,
            CreatedByUserId = user.Id,
            Title = "Duplicate Discussion",
            Slug = $"discussion-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task DuplicateReportReasonPublicId_ThrowsOnSave()
    {
        var reason = new ReportReasonDatabaseEntity
        {
            PublicId = "rr_duplicate_test",
            Name = "First Reason",
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.ReportReasons.Add(reason);
        await _db.Context.SaveChangesAsync();

        using var insertContext = _db.CreateSeparateContext();
        insertContext.ReportReasons.Add(new ReportReasonDatabaseEntity
        {
            PublicId = "rr_duplicate_test",
            Name = "Second Reason",
            CreatedAt = DateTime.UtcNow
        });

        var act = async () => { await insertContext.SaveChangesAsync(); };
        await Assert.That(act).ThrowsException();
    }
}
