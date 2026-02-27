using Snakk.Shared.Helpers;

namespace Snakk.Shared.Tests.Helpers;

public class AvatarHelperTests
{
    [Test]
    public async Task GetAvatarPath_ReturnsCorrectFormatWithShard()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var path = AvatarHelper.GetAvatarPath(publicId, AvatarEntityType.User);

        // Assert
        // Format should be "{shard}/{publicId}.svg"
        await Assert.That(path).Contains("/");
        await Assert.That(path).EndsWith($"{publicId}.svg");
        // Shard folder should be 2 hex chars
        var shard = path.Split('/')[0];
        await Assert.That(shard.Length).IsEqualTo(2);
    }

    [Test]
    public async Task GetAvatarPath_WithRevision_IncludesRevisionInFilename()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var path = AvatarHelper.GetAvatarPath(publicId, AvatarEntityType.User, revision: 5);

        // Assert
        await Assert.That(path).Contains($"{publicId}-r5.svg");
    }

    [Test]
    public async Task GetAvatarPath_WithoutRevision_NoRevisionInFilename()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var path = AvatarHelper.GetAvatarPath(publicId, AvatarEntityType.User, revision: 0);

        // Assert
        await Assert.That(path).EndsWith($"{publicId}.svg");
        await Assert.That(path).DoesNotContain("-r");
    }

    [Test]
    public async Task GetFullRelativePath_IncludesEntityFolder()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var path = AvatarHelper.GetFullRelativePath(publicId, AvatarEntityType.User);

        // Assert
        await Assert.That(path).StartsWith("avatars/generated/users/");
        await Assert.That(path).EndsWith($"{publicId}.svg");
    }

    [Test]
    public async Task GetAvatarUrl_StartsWithSlash()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var url = AvatarHelper.GetAvatarUrl(publicId, AvatarEntityType.User);

        // Assert
        await Assert.That(url).StartsWith("/");
        await Assert.That(url).StartsWith("/avatars/generated/users/");
    }

    [Test]
    public async Task GetShardFolder_ReturnsTwoCharHexString()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var shard = AvatarHelper.GetShardFolder(publicId);

        // Assert
        await Assert.That(shard.Length).IsEqualTo(2);
        // Verify it's valid hex
        var isHex = int.TryParse(shard, System.Globalization.NumberStyles.HexNumber, null, out _);
        await Assert.That(isHex).IsTrue();
    }

    [Test]
    public async Task GetEntityFolder_MapsUser_ToUsers()
    {
        // Act
        var folder = AvatarHelper.GetEntityFolder(AvatarEntityType.User);

        // Assert
        await Assert.That(folder).IsEqualTo("users");
    }

    [Test]
    public async Task GetEntityFolder_MapsCommunity_ToCommunities()
    {
        // Act
        var folder = AvatarHelper.GetEntityFolder(AvatarEntityType.Community);

        // Assert
        await Assert.That(folder).IsEqualTo("communities");
    }

    [Test]
    public async Task GetEntityFolder_MapsHub_ToHubs()
    {
        // Act
        var folder = AvatarHelper.GetEntityFolder(AvatarEntityType.Hub);

        // Assert
        await Assert.That(folder).IsEqualTo("hubs");
    }

    [Test]
    public async Task GetEntityFolder_MapsSpace_ToSpaces()
    {
        // Act
        var folder = AvatarHelper.GetEntityFolder(AvatarEntityType.Space);

        // Assert
        await Assert.That(folder).IsEqualTo("spaces");
    }

    [Test]
    public async Task GetEntityFolder_ThrowsForUnknownType()
    {
        // Arrange
        var unknownType = (AvatarEntityType)999;

        // Act & Assert
        await Assert.That(() => AvatarHelper.GetEntityFolder(unknownType)).Throws<ArgumentException>();
    }

    [Test]
    public async Task SamePublicId_AlwaysProducesSameShard_Deterministic()
    {
        // Arrange
        var publicId = "01HXYZ12345ABCDEF";

        // Act
        var shard1 = AvatarHelper.GetShardFolder(publicId);
        var shard2 = AvatarHelper.GetShardFolder(publicId);
        var shard3 = AvatarHelper.GetShardFolder(publicId);

        // Assert
        await Assert.That(shard1).IsEqualTo(shard2);
        await Assert.That(shard2).IsEqualTo(shard3);
    }

    [Test]
    public async Task DifferentPublicIds_DistributeAcrossShards()
    {
        // Arrange - use enough IDs to likely get different shard values
        var ids = new[]
        {
            "01H000000000000000000001",
            "01H000000000000000000002",
            "01HZZZZZZZZZZZZZZZZZZZZZ",
            "01HAAAAAAAAAAAAAAAAAAAAA",
            "01HBBBBBBBBBBBBBBBBBBBBB"
        };

        // Act
        var shards = ids.Select(AvatarHelper.GetShardFolder).ToHashSet();

        // Assert - at least some should be different (not all the same shard)
        await Assert.That(shards.Count).IsGreaterThanOrEqualTo(2);
    }
}
