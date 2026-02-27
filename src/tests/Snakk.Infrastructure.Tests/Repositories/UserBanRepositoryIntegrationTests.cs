using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class UserBanRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly UserBanRepository _repository;

    public UserBanRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new UserBanRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region Helper Methods

    private async Task<UserBanDatabaseEntity> CreateBanAsync(
        int userId,
        int bannedByUserId,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null,
        string? reason = null,
        DateTime? expiresAt = null,
        DateTime? unbannedAt = null,
        int? unbannedByUserId = null,
        string? publicId = null)
    {
        var ban = new UserBanDatabaseEntity
        {
            PublicId = publicId ?? $"ban-{Guid.NewGuid():N}",
            UserId = userId,
            BannedByUserId = bannedByUserId,
            BanTypeId = (int)BanTypeEnum.WriteOnly,
            Reason = reason ?? "Test ban",
            BannedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            UnbannedAt = unbannedAt,
            UnbannedByUserId = unbannedByUserId
        };
        _db.Context.UserBans.Add(ban);
        await _db.Context.SaveChangesAsync();
        return ban;
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingBan_ReturnsWithNavigationProperties()
    {
        var user = await _builder.CreateUserAsync("BannedUser");
        var admin = await _builder.CreateUserAsync("AdminUser");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        var ban = await CreateBanAsync(
            user.Id, admin.Id,
            communityId: community.Id,
            hubId: hub.Id,
            spaceId: space.Id,
            publicId: "ban-nav-test");

        var result = await _repository.GetByPublicIdAsync("ban-nav-test");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo("ban-nav-test");
        await Assert.That(result.User).IsNotNull();
        await Assert.That(result.User.DisplayName).IsEqualTo("BannedUser");
        await Assert.That(result.BannedByUser).IsNotNull();
        await Assert.That(result.BannedByUser.DisplayName).IsEqualTo("AdminUser");
        await Assert.That(result.Community).IsNotNull();
        await Assert.That(result.Community!.Id).IsEqualTo(community.Id);
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Hub!.Id).IsEqualTo(hub.Id);
        await Assert.That(result.Space).IsNotNull();
        await Assert.That(result.Space!.Id).IsEqualTo(space.Id);
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent-ban-id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetActiveBansForUserAsync Tests

    [Test]
    public async Task GetActiveBansForUserAsync_ReturnsNonExpiredNonUnbanned()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();

        // Active ban (no expiry, not unbanned)
        var activeBan = await CreateBanAsync(user.Id, admin.Id, communityId: community.Id);

        // Active ban with future expiry
        var futureExpiryBan = await CreateBanAsync(
            user.Id, admin.Id,
            expiresAt: DateTime.UtcNow.AddDays(7));

        var result = (await _repository.GetActiveBansForUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetActiveBansForUserAsync_ExcludesExpiredBans()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();

        // Expired ban
        await CreateBanAsync(
            user.Id, admin.Id,
            expiresAt: DateTime.UtcNow.AddDays(-1));

        // Active ban (no expiry)
        var activeBan = await CreateBanAsync(user.Id, admin.Id);

        var result = (await _repository.GetActiveBansForUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo(activeBan.Id);
    }

    [Test]
    public async Task GetActiveBansForUserAsync_ExcludesUnbannedUsers()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();

        // Unbanned ban
        await CreateBanAsync(
            user.Id, admin.Id,
            unbannedAt: DateTime.UtcNow,
            unbannedByUserId: admin.Id);

        // Active ban
        var activeBan = await CreateBanAsync(user.Id, admin.Id);

        var result = (await _repository.GetActiveBansForUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo(activeBan.Id);
    }

    #endregion

    #region IsUserBannedAsync Tests

    [Test]
    public async Task IsUserBannedAsync_PlatformBan_BlocksEverything()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Platform-wide ban (all scope fields null)
        await CreateBanAsync(user.Id, admin.Id);

        // Should be banned at every scope level
        var bannedGlobally = await _repository.IsUserBannedAsync(user.Id);
        var bannedInCommunity = await _repository.IsUserBannedAsync(user.Id, communityId: community.Id);
        var bannedInHub = await _repository.IsUserBannedAsync(user.Id, hubId: hub.Id);
        var bannedInSpace = await _repository.IsUserBannedAsync(user.Id, spaceId: space.Id);

        await Assert.That(bannedGlobally).IsTrue();
        await Assert.That(bannedInCommunity).IsTrue();
        await Assert.That(bannedInHub).IsTrue();
        await Assert.That(bannedInSpace).IsTrue();
    }

    [Test]
    public async Task IsUserBannedAsync_CommunityBan_BlocksHubAccess()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        // Community-scoped ban
        await CreateBanAsync(user.Id, admin.Id, communityId: community.Id);

        // Should be banned in the community and its hub
        var bannedInCommunity = await _repository.IsUserBannedAsync(user.Id, communityId: community.Id);
        var bannedInHub = await _repository.IsUserBannedAsync(user.Id, hubId: hub.Id);

        await Assert.That(bannedInCommunity).IsTrue();
        await Assert.That(bannedInHub).IsTrue();
    }

    [Test]
    public async Task IsUserBannedAsync_CommunityBan_DoesNotBlockOtherCommunity()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community1 = await _builder.CreateCommunityAsync();
        var community2 = await _builder.CreateCommunityAsync();

        // Ban in community1 only
        await CreateBanAsync(user.Id, admin.Id, communityId: community1.Id);

        var bannedInCommunity1 = await _repository.IsUserBannedAsync(user.Id, communityId: community1.Id);
        var bannedInCommunity2 = await _repository.IsUserBannedAsync(user.Id, communityId: community2.Id);

        await Assert.That(bannedInCommunity1).IsTrue();
        await Assert.That(bannedInCommunity2).IsFalse();
    }

    [Test]
    public async Task IsUserBannedAsync_NoBan_ReturnsFalse()
    {
        var user = await _builder.CreateUserAsync();

        var result = await _repository.IsUserBannedAsync(user.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetBansForCommunityAsync Tests

    [Test]
    public async Task GetBansForCommunityAsync_ReturnsCorrectBans()
    {
        var user1 = await _builder.CreateUserAsync("User1");
        var user2 = await _builder.CreateUserAsync("User2");
        var admin = await _builder.CreateUserAsync("Admin");
        var community1 = await _builder.CreateCommunityAsync();
        var community2 = await _builder.CreateCommunityAsync();

        // Two bans in community1
        await CreateBanAsync(user1.Id, admin.Id, communityId: community1.Id);
        await CreateBanAsync(user2.Id, admin.Id, communityId: community1.Id);

        // One ban in community2
        await CreateBanAsync(user1.Id, admin.Id, communityId: community2.Id);

        var result = (await _repository.GetBansForCommunityAsync(community1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        // Verify navigation properties are loaded
        await Assert.That(result[0].User).IsNotNull();
        await Assert.That(result[0].BannedByUser).IsNotNull();
    }

    #endregion

    #region GetBansForHubAsync Tests

    [Test]
    public async Task GetBansForHubAsync_ReturnsCorrectBans()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub1 = await _builder.CreateHubAsync(community.Id);
        var hub2 = await _builder.CreateHubAsync(community.Id);

        await CreateBanAsync(user.Id, admin.Id, hubId: hub1.Id);
        await CreateBanAsync(user.Id, admin.Id, hubId: hub2.Id);

        var result = (await _repository.GetBansForHubAsync(hub1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].HubId).IsEqualTo(hub1.Id);
    }

    #endregion

    #region GetBansForSpaceAsync Tests

    [Test]
    public async Task GetBansForSpaceAsync_ReturnsCorrectBans()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id);
        var space2 = await _builder.CreateSpaceAsync(hub.Id);

        await CreateBanAsync(user.Id, admin.Id, spaceId: space1.Id);
        await CreateBanAsync(user.Id, admin.Id, spaceId: space2.Id);

        var result = (await _repository.GetBansForSpaceAsync(space1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].SpaceId).IsEqualTo(space1.Id);
    }

    #endregion
}
