using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class UserRoleRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly UserRoleRepository _repository;

    public UserRoleRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new UserRoleRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingRole_ReturnsWithNavigationProperties()
    {
        var user = await _builder.CreateUserAsync("RoleOwner");
        var assignedBy = await _builder.CreateUserAsync("Assigner");
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        var role = await _builder.AssignRoleAsync(
            user.Id, UserRoleTypeEnum.SpaceMod, assignedBy.Id,
            communityId: community.Id, hubId: hub.Id, spaceId: space.Id);

        var result = await _repository.GetByPublicIdAsync(role.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(role.PublicId);
        await Assert.That(result.User).IsNotNull();
        await Assert.That(result.User.DisplayName).IsEqualTo("RoleOwner");
        await Assert.That(result.Community).IsNotNull();
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Space).IsNotNull();
        await Assert.That(result.AssignedByUser).IsNotNull();
        await Assert.That(result.AssignedByUser.DisplayName).IsEqualTo("Assigner");
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetActiveRolesForUserAsync Tests

    [Test]
    public async Task GetActiveRolesForUserAsync_ReturnsActiveRolesOnly()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();

        var activeRole = await _builder.AssignRoleAsync(
            user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);

        var revokedRole = await _builder.AssignRoleAsync(
            user.Id, UserRoleTypeEnum.CommunityMod, admin.Id, communityId: community.Id);
        revokedRole.RevokedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetActiveRolesForUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PublicId).IsEqualTo(activeRole.PublicId);
    }

    [Test]
    public async Task GetActiveRolesForUserAsync_UserWithNoRoles_ReturnsEmpty()
    {
        var user = await _builder.CreateUserAsync();

        var result = (await _repository.GetActiveRolesForUserAsync(user.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetActiveRolesForCommunityAsync Tests

    [Test]
    public async Task GetActiveRolesForCommunityAsync_ReturnsScopedRoles()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community1 = await _builder.CreateCommunityAsync();
        var community2 = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community1.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityMod, admin.Id, communityId: community2.Id);

        var result = (await _repository.GetActiveRolesForCommunityAsync(community1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].CommunityId).IsEqualTo(community1.Id);
    }

    #endregion

    #region GetActiveRolesForHubAsync Tests

    [Test]
    public async Task GetActiveRolesForHubAsync_ReturnsScopedRoles()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub1 = await _builder.CreateHubAsync(community.Id);
        var hub2 = await _builder.CreateHubAsync(community.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hub1.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hub2.Id);

        var result = (await _repository.GetActiveRolesForHubAsync(hub1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].HubId).IsEqualTo(hub1.Id);
    }

    #endregion

    #region GetActiveRolesForSpaceAsync Tests

    [Test]
    public async Task GetActiveRolesForSpaceAsync_ReturnsScopedRoles()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id);
        var space2 = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space1.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space2.Id);

        var result = (await _repository.GetActiveRolesForSpaceAsync(space1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].SpaceId).IsEqualTo(space1.Id);
    }

    #endregion

    #region GetGlobalAdminsAsync Tests

    [Test]
    public async Task GetGlobalAdminsAsync_ReturnsOnlyGlobalAdminRoles()
    {
        var admin = await _builder.CreateUserAsync();
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);
        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);

        var result = (await _repository.GetGlobalAdminsAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(admin.Id);
        await Assert.That(result[0].RoleId).IsEqualTo((int)UserRoleTypeEnum.GlobalAdmin);
    }

    #endregion

    #region HasRoleAtOrAboveAsync Tests

    [Test]
    public async Task HasRoleAtOrAboveAsync_ExactMatch_ReturnsTrue()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);

        var result = await _repository.HasRoleAtOrAboveAsync(user.Id, "CommunityAdmin", communityId: community.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasRoleAtOrAboveAsync_InvalidRoleString_ReturnsFalse()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var result = await _repository.HasRoleAtOrAboveAsync(user.Id, "NotARealRole");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasRoleAtOrAboveAsync_RevokedRole_ReturnsFalse()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();

        var role = await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);
        role.RevokedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        var result = await _repository.HasRoleAtOrAboveAsync(user.Id, "GlobalAdmin");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region CanModerateAsync Tests

    [Test]
    public async Task CanModerateAsync_GlobalAdmin_CanModerateAnywhere()
    {
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var result = await _repository.CanModerateAsync(admin.Id, communityId: community.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_CommunityAdmin_CanModerateCommunity()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);

        var result = await _repository.CanModerateAsync(user.Id, communityId: community.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_HubMod_CanModerateHub()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hub.Id);

        var result = await _repository.CanModerateAsync(user.Id, hubId: hub.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_HubMod_CanModerateSpaceWithinHub()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hub.Id);

        var result = await _repository.CanModerateAsync(user.Id, spaceId: space.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_SpaceMod_CanModerateOwnSpace()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space.Id);

        var result = await _repository.CanModerateAsync(user.Id, spaceId: space.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_SpaceMod_CannotModerateOtherSpace()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id);
        var space2 = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.SpaceMod, admin.Id, spaceId: space1.Id);

        var result = await _repository.CanModerateAsync(user.Id, spaceId: space2.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region CanAdministerAsync Tests

    [Test]
    public async Task CanAdministerAsync_GlobalAdmin_CanAdministerAnywhere()
    {
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        var result = await _repository.CanAdministerAsync(admin.Id, hubId: hub.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanAdministerAsync_CommunityAdmin_CanAdministerHubViaCommunity()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.CommunityAdmin, admin.Id, communityId: community.Id);

        var result = await _repository.CanAdministerAsync(user.Id, hubId: hub.Id);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanAdministerAsync_HubMod_CannotAdminister()
    {
        var user = await _builder.CreateUserAsync();
        var admin = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);

        await _builder.AssignRoleAsync(user.Id, UserRoleTypeEnum.HubMod, admin.Id, hubId: hub.Id);

        var result = await _repository.CanAdministerAsync(user.Id, hubId: hub.Id);

        await Assert.That(result).IsFalse();
    }

    #endregion
}
