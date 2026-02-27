using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class CommunityMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        var entity = new CommunityDatabaseEntity
        {
            PublicId = "comm_abc123",
            Name = "Test Community",
            Slug = "test-community",
            Description = "A test community",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            ExposeToPlatformFeed = true,
            CreatedAt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            LastModifiedAt = new DateTime(2024, 6, 16, 12, 0, 0, DateTimeKind.Utc)
        };

        var community = entity.FromPersistence();

        await Assert.That(community).IsNotNull();
        await Assert.That(community.PublicId.Value).IsEqualTo("comm_abc123");
        await Assert.That(community.Name).IsEqualTo("Test Community");
        await Assert.That(community.Slug).IsEqualTo("test-community");
        await Assert.That(community.Description).IsEqualTo("A test community");
        await Assert.That(community.ExposeToPlatformFeed).IsTrue();
        await Assert.That(community.CreatedAt).IsEqualTo(new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc));
        await Assert.That(community.LastModifiedAt).IsEqualTo(new DateTime(2024, 6, 16, 12, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task FromPersistence_WithPublicListedVisibility_MapsCorrectly()
    {
        var entity = new CommunityDatabaseEntity
        {
            PublicId = "comm_vis1",
            Name = "Listed",
            Slug = "listed",
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            CreatedAt = DateTime.UtcNow
        };

        var community = entity.FromPersistence();

        await Assert.That(community.Visibility).IsEqualTo(CommunityVisibility.PublicListed);
    }

    [Test]
    public async Task FromPersistence_WithPublicUnlistedVisibility_MapsCorrectly()
    {
        var entity = new CommunityDatabaseEntity
        {
            PublicId = "comm_vis2",
            Name = "Unlisted",
            Slug = "unlisted",
            VisibilityId = (int)CommunityVisibilityEnum.PublicUnlisted,
            CreatedAt = DateTime.UtcNow
        };

        var community = entity.FromPersistence();

        await Assert.That(community.Visibility).IsEqualTo(CommunityVisibility.PublicUnlisted);
    }

    [Test]
    public async Task FromPersistence_WithNullDescription_MapsNull()
    {
        var entity = new CommunityDatabaseEntity
        {
            PublicId = "comm_nodesc",
            Name = "No Desc",
            Slug = "no-desc",
            Description = null,
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed,
            CreatedAt = DateTime.UtcNow
        };

        var community = entity.FromPersistence();

        await Assert.That(community.Description).IsNull();
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        var community = Community.Create("My Community", "my-community", "Description text");

        var entity = community.ToPersistence();

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(community.PublicId.Value);
        await Assert.That(entity.Name).IsEqualTo("My Community");
        await Assert.That(entity.Slug).IsEqualTo("my-community");
        await Assert.That(entity.Description).IsEqualTo("Description text");
        await Assert.That(entity.VisibilityId).IsEqualTo((int)CommunityVisibilityEnum.PublicListed);
    }

    [Test]
    public async Task ToPersistence_SetsTimestamps()
    {
        var community = Community.Create("Timestamps", "timestamps", "desc");

        var entity = community.ToPersistence();

        await Assert.That((DateTime.UtcNow - entity.CreatedAt).TotalSeconds).IsLessThan(2);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        var original = Community.Create("Round Trip", "round-trip", "Test description");

        var entity = original.ToPersistence();
        var reconstructed = entity.FromPersistence();

        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.Name).IsEqualTo(original.Name);
        await Assert.That(reconstructed.Slug).IsEqualTo(original.Slug);
        await Assert.That(reconstructed.Description).IsEqualTo(original.Description);
        await Assert.That(reconstructed.ExposeToPlatformFeed).IsEqualTo(original.ExposeToPlatformFeed);
    }

    #endregion
}
