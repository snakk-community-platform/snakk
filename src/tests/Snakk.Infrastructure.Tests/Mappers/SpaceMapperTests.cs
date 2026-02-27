using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class SpaceMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        var entity = new SpaceDatabaseEntity
        {
            PublicId = "space_abc123",
            Name = "Test Space",
            Slug = "test-space",
            Description = "A test space",
            AllowAnonymousReading = true,
            RequireEmailConfirmation = false,
            CreatedAt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            LastModifiedAt = new DateTime(2024, 6, 16, 12, 0, 0, DateTimeKind.Utc),
            HubId = 1,
            Hub = new HubDatabaseEntity
            {
                PublicId = "hub_parent",
                Name = "Parent Hub",
                Slug = "parent-hub",
                CreatedAt = DateTime.UtcNow,
                CommunityId = 1
            }
        };

        var space = entity.FromPersistence();

        await Assert.That(space).IsNotNull();
        await Assert.That(space.PublicId.Value).IsEqualTo("space_abc123");
        await Assert.That(space.HubId.Value).IsEqualTo("hub_parent");
        await Assert.That(space.Name).IsEqualTo("Test Space");
        await Assert.That(space.Slug).IsEqualTo("test-space");
        await Assert.That(space.Description).IsEqualTo("A test space");
        await Assert.That(space.AllowAnonymousReading).IsTrue();
        await Assert.That(space.RequireEmailConfirmation).IsFalse();
        await Assert.That(space.CreatedAt).IsEqualTo(new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task FromPersistence_WithNullDescription_MapsNull()
    {
        var entity = new SpaceDatabaseEntity
        {
            PublicId = "space_nodesc",
            Name = "No Desc",
            Slug = "no-desc",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Hub = new HubDatabaseEntity
            {
                PublicId = "hub_nd",
                Name = "Hub",
                Slug = "hub",
                CreatedAt = DateTime.UtcNow,
                CommunityId = 1
            }
        };

        var space = entity.FromPersistence();

        await Assert.That(space.Description).IsNull();
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        var space = Space.Create(HubId.From("hub_test"), "New Space", "new-space", "Description");

        var entity = space.ToPersistence();

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(space.PublicId.Value);
        await Assert.That(entity.Name).IsEqualTo("New Space");
        await Assert.That(entity.Slug).IsEqualTo("new-space");
        await Assert.That(entity.Description).IsEqualTo("Description");
    }

    [Test]
    public async Task ToPersistence_DoesNotSetHubId()
    {
        // HubId is set by the repository adapter, not the mapper
        var space = Space.Create(HubId.From("hub_test"), "Space", "space", "desc");

        var entity = space.ToPersistence();

        // HubId should be default (0) since it's set by the repository
        await Assert.That(entity.HubId).IsEqualTo(0);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        var hubPublicId = "hub_roundtrip";
        var original = Space.Create(HubId.From(hubPublicId), "Round Trip Space", "round-trip", "Description");

        var entity = original.ToPersistence();
        entity.Hub = new HubDatabaseEntity
        {
            PublicId = hubPublicId,
            Name = "Hub",
            Slug = "hub",
            CreatedAt = DateTime.UtcNow,
            CommunityId = 1
        };
        var reconstructed = entity.FromPersistence();

        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.HubId.Value).IsEqualTo(hubPublicId);
        await Assert.That(reconstructed.Name).IsEqualTo(original.Name);
        await Assert.That(reconstructed.Slug).IsEqualTo(original.Slug);
        await Assert.That(reconstructed.Description).IsEqualTo(original.Description);
    }

    #endregion
}
