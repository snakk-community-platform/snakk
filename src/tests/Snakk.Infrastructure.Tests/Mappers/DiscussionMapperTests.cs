using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class DiscussionMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        var spacePublicId = Guid.NewGuid().ToString();
        var userPublicId = Guid.NewGuid().ToString();
        var createdAt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastModifiedAt = new DateTime(2024, 6, 16, 12, 0, 0, DateTimeKind.Utc);
        var lastActivityAt = new DateTime(2024, 6, 17, 8, 0, 0, DateTimeKind.Utc);

        var entity = new DiscussionDatabaseEntity
        {
            PublicId = "disc_abc123",
            Title = "Test Discussion",
            Slug = "test-discussion",
            CreatedAt = createdAt,
            LastModifiedAt = lastModifiedAt,
            LastActivityAt = lastActivityAt,
            IsPinned = true,
            IsLocked = false,
            SpaceId = 1,
            Space = new SpaceDatabaseEntity
            {
                PublicId = spacePublicId,
                Name = "Space",
                Slug = "space",
                CreatedAt = DateTime.UtcNow,
                HubId = 1,
                Hub = new HubDatabaseEntity
                {
                    PublicId = "hub1",
                    Name = "Hub",
                    Slug = "hub",
                    CreatedAt = DateTime.UtcNow,
                    CommunityId = 1
                }
            },
            CreatedByUserId = 1,
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow
            }
        };

        var discussion = entity.FromPersistence();

        await Assert.That(discussion).IsNotNull();
        await Assert.That(discussion.PublicId.Value).IsEqualTo("disc_abc123");
        await Assert.That(discussion.SpaceId.Value).IsEqualTo(spacePublicId);
        await Assert.That(discussion!.CreatedByUserId!.Value).IsEqualTo(userPublicId);
        await Assert.That(discussion.Title).IsEqualTo("Test Discussion");
        await Assert.That(discussion.Slug).IsEqualTo("test-discussion");
        await Assert.That(discussion.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(discussion.LastModifiedAt).IsEqualTo(lastModifiedAt);
        await Assert.That(discussion.LastActivityAt).IsEqualTo(lastActivityAt);
        await Assert.That(discussion.IsPinned).IsTrue();
        await Assert.That(discussion.IsLocked).IsFalse();
    }

    [Test]
    public async Task FromPersistence_WithNullLastActivityAt_MapsNull()
    {
        var entity = new DiscussionDatabaseEntity
        {
            PublicId = "disc_noactivity",
            Title = "No Activity",
            Slug = "no-activity",
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = null,
            Space = new SpaceDatabaseEntity
            {
                PublicId = "s1",
                Name = "S",
                Slug = "s",
                CreatedAt = DateTime.UtcNow,
                HubId = 1,
                Hub = new HubDatabaseEntity { PublicId = "h1", Name = "H", Slug = "h", CreatedAt = DateTime.UtcNow, CommunityId = 1 }
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = "u1",
                DisplayName = "User",
                Email = "u@e.com",
                CreatedAt = DateTime.UtcNow
            }
        };

        var discussion = entity.FromPersistence();

        await Assert.That(discussion.LastActivityAt).IsNull();
    }

    [Test]
    public async Task FromPersistence_LockedDiscussion_MapsIsLockedTrue()
    {
        var entity = new DiscussionDatabaseEntity
        {
            PublicId = "disc_locked",
            Title = "Locked",
            Slug = "locked",
            CreatedAt = DateTime.UtcNow,
            IsLocked = true,
            Space = new SpaceDatabaseEntity
            {
                PublicId = "s2",
                Name = "S",
                Slug = "s",
                CreatedAt = DateTime.UtcNow,
                HubId = 1,
                Hub = new HubDatabaseEntity { PublicId = "h2", Name = "H", Slug = "h", CreatedAt = DateTime.UtcNow, CommunityId = 1 }
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = "u2",
                DisplayName = "User",
                Email = "u@e.com",
                CreatedAt = DateTime.UtcNow
            }
        };

        var discussion = entity.FromPersistence();

        await Assert.That(discussion.IsLocked).IsTrue();
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        var discussion = Discussion.Create(
            SpaceId.From("space1"),
            UserId.From("user1"),
            "My Discussion",
            "my-discussion");

        var entity = discussion.ToPersistence();

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(discussion.PublicId.Value);
        await Assert.That(entity.Title).IsEqualTo("My Discussion");
        await Assert.That(entity.Slug).IsEqualTo("my-discussion");
        await Assert.That(entity.IsPinned).IsFalse();
        await Assert.That(entity.IsLocked).IsFalse();
    }

    [Test]
    public async Task ToPersistence_DoesNotSetForeignKeys()
    {
        // SpaceId and CreatedByUserId are set by the repository adapter
        var discussion = Discussion.Create(
            SpaceId.From("space1"),
            UserId.From("user1"),
            "Title",
            "slug");

        var entity = discussion.ToPersistence();

        await Assert.That(entity.SpaceId).IsEqualTo(0);
        await Assert.That(entity.CreatedByUserId).IsEqualTo(0);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        var spacePublicId = "space_rt";
        var userPublicId = "user_rt";
        var original = Discussion.Create(
            SpaceId.From(spacePublicId),
            UserId.From(userPublicId),
            "Round Trip Discussion",
            "round-trip");

        var entity = original.ToPersistence();
        entity.Space = new SpaceDatabaseEntity
        {
            PublicId = spacePublicId,
            Name = "Space",
            Slug = "space",
            CreatedAt = DateTime.UtcNow,
            HubId = 1,
            Hub = new HubDatabaseEntity { PublicId = "h_rt", Name = "Hub", Slug = "hub", CreatedAt = DateTime.UtcNow, CommunityId = 1 }
        };
        entity.CreatedByUser = new UserDatabaseEntity
        {
            PublicId = userPublicId,
            DisplayName = "User",
            Email = "u@e.com",
            CreatedAt = DateTime.UtcNow
        };
        var reconstructed = entity.FromPersistence();

        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.Title).IsEqualTo(original.Title);
        await Assert.That(reconstructed.Slug).IsEqualTo(original.Slug);
        await Assert.That(reconstructed.IsPinned).IsEqualTo(original.IsPinned);
        await Assert.That(reconstructed.IsLocked).IsEqualTo(original.IsLocked);
    }

    #endregion
}
