using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class HubMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        var entity = new HubDatabaseEntity
        {
            PublicId = "hub_abc123",
            Name = "Test Hub",
            Slug = "test-hub",
            Description = "A test hub",
            AllowAnonymousReading = true,
            RequireEmailConfirmation = false,
            CreatedAt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            LastModifiedAt = new DateTime(2024, 6, 16, 12, 0, 0, DateTimeKind.Utc),
            CommunityId = 1,
            Community = new CommunityDatabaseEntity
            {
                PublicId = "comm_parent",
                Name = "Parent Community",
                Slug = "parent",
                CreatedAt = DateTime.UtcNow,
                VisibilityId = 1
            }
        };

        var hub = entity.FromPersistence();

        await Assert.That(hub).IsNotNull();
        await Assert.That(hub.PublicId.Value).IsEqualTo("hub_abc123");
        await Assert.That(hub.Name).IsEqualTo("Test Hub");
        await Assert.That(hub.Slug).IsEqualTo("test-hub");
        await Assert.That(hub.Description).IsEqualTo("A test hub");
        await Assert.That(hub.AllowAnonymousReading).IsTrue();
        await Assert.That(hub.RequireEmailConfirmation).IsFalse();
        await Assert.That(hub.CommunityId.Value).IsEqualTo("comm_parent");
    }

    [Test]
    public async Task FromPersistence_WithNullDescription_MapsNull()
    {
        var entity = new HubDatabaseEntity
        {
            PublicId = "hub_nodesc",
            Name = "No Desc Hub",
            Slug = "no-desc",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Community = new CommunityDatabaseEntity
            {
                PublicId = "comm_nd",
                Name = "Community",
                Slug = "community",
                CreatedAt = DateTime.UtcNow,
                VisibilityId = 1
            }
        };

        var hub = entity.FromPersistence();

        await Assert.That(hub.Description).IsNull();
    }

    #endregion

    #region FromPersistenceWithCommunityId Tests

    [Test]
    public async Task FromPersistenceWithCommunityId_UsesSeparateCommunityId()
    {
        var entity = new HubDatabaseEntity
        {
            PublicId = "hub_separate",
            Name = "Hub",
            Slug = "hub",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow,
            CommunityId = 99 // DB ID, irrelevant for domain
        };

        var hub = entity.FromPersistenceWithCommunityId("comm_external");

        await Assert.That(hub.CommunityId.Value).IsEqualTo("comm_external");
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        var communityId = CommunityId.From("comm_for_hub");
        var hub = Hub.Create(communityId, "New Hub", "new-hub", "New description");

        var entity = hub.ToPersistence(42);

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(hub.PublicId.Value);
        await Assert.That(entity.CommunityId).IsEqualTo(42);
        await Assert.That(entity.Name).IsEqualTo("New Hub");
        await Assert.That(entity.Slug).IsEqualTo("new-hub");
        await Assert.That(entity.Description).IsEqualTo("New description");
    }

    [Test]
    public async Task ToPersistence_SetsTimestamps()
    {
        var hub = Hub.Create(CommunityId.From("c1"), "Hub", "hub", "desc");

        var entity = hub.ToPersistence(1);

        await Assert.That((DateTime.UtcNow - entity.CreatedAt).TotalSeconds).IsLessThan(2);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        var communityPublicId = "comm_roundtrip";
        var communityId = CommunityId.From(communityPublicId);
        var original = Hub.Create(communityId, "Round Trip Hub", "round-trip", "Description");

        var entity = original.ToPersistence(1);
        entity.Community = new CommunityDatabaseEntity
        {
            PublicId = communityPublicId,
            Name = "Community",
            Slug = "community",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = 1
        };
        var reconstructed = entity.FromPersistence();

        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.CommunityId).IsEqualTo(communityId);
        await Assert.That(reconstructed.Name).IsEqualTo(original.Name);
        await Assert.That(reconstructed.Slug).IsEqualTo(original.Slug);
        await Assert.That(reconstructed.Description).IsEqualTo(original.Description);
    }

    #endregion
}
