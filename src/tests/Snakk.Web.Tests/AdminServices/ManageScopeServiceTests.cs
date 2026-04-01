extern alias Admin;

using Grpc.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Admin::Snakk.Admin.Services;
using Snakk.Application.DTOs.Management;
using Snakk.Protos.Manage;
using Snakk.Shared.Enums;

namespace Snakk.Web.Tests.AdminServices;

/// <summary>
/// Tests for ManageScopeService which resolves management scopes (Community/Hub/Space)
/// and builds URLs for the admin panel.
/// </summary>
public class ManageScopeServiceTests
{
    // ===== Helper Methods =====

    private static ManageScopeDto CreateScope(
        string scopeType,
        string communitySlug = "test-community",
        string? hubSlug = null,
        string? spaceSlug = null,
        List<ManagePermissionEnum>? permissions = null)
    {
        var publicId = Guid.NewGuid().ToString();
        return new ManageScopeDto
        {
            ScopeType = scopeType,
            ScopePublicId = publicId,
            ScopeName = $"Test {scopeType}",
            CommunitySlug = communitySlug,
            HubSlug = hubSlug,
            SpaceSlug = spaceSlug,
            CommunityName = "Test Community",
            HubName = hubSlug is not null ? "Test Hub" : null,
            SpaceName = spaceSlug is not null ? "Test Space" : null,
            CommunityPublicId = publicId,
            Permissions = permissions ?? []
        };
    }

    // ===== GetManageUrl Tests =====

    [Test]
    public async Task GetManageUrl_CommunityScope_ReturnsCommunityManageUrl()
    {
        var scope = CreateScope("Community", communitySlug: "gaming");

        var result = ManageScopeService.GetManageUrl(scope);

        await Assert.That(result).IsEqualTo("c/gaming");
    }

    [Test]
    public async Task GetManageUrl_HubScope_ReturnsHubManageUrl()
    {
        var scope = CreateScope("Hub", communitySlug: "gaming", hubSlug: "fps");

        var result = ManageScopeService.GetManageUrl(scope);

        await Assert.That(result).IsEqualTo("c/gaming/h/fps");
    }

    [Test]
    public async Task GetManageUrl_SpaceScope_ReturnsSpaceManageUrl()
    {
        var scope = CreateScope("Space", communitySlug: "gaming", hubSlug: "fps", spaceSlug: "valorant");

        var result = ManageScopeService.GetManageUrl(scope);

        await Assert.That(result).IsEqualTo("c/gaming/h/fps/s/valorant");
    }

    // ===== GetSiteUrl Tests (multi-community) =====

    [Test]
    public async Task GetSiteUrl_MultiCommunity_CommunityScope_ReturnsCommunityUrl()
    {
        var scope = CreateScope("Community", communitySlug: "gaming");

        var result = ManageScopeService.GetSiteUrl(scope, isMultiCommunity: true);

        await Assert.That(result).IsEqualTo("/c/gaming");
    }

    [Test]
    public async Task GetSiteUrl_MultiCommunity_HubScope_ReturnsHubUrl()
    {
        var scope = CreateScope("Hub", communitySlug: "gaming", hubSlug: "fps");

        var result = ManageScopeService.GetSiteUrl(scope, isMultiCommunity: true);

        await Assert.That(result).IsEqualTo("/c/gaming/h/fps");
    }

    [Test]
    public async Task GetSiteUrl_MultiCommunity_SpaceScope_ReturnsSpaceUrl()
    {
        var scope = CreateScope("Space", communitySlug: "gaming", hubSlug: "fps", spaceSlug: "valorant");

        var result = ManageScopeService.GetSiteUrl(scope, isMultiCommunity: true);

        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/valorant");
    }

    // ===== GetSiteUrl Tests (single-community) =====

    [Test]
    public async Task GetSiteUrl_SingleCommunity_HubScope_ReturnsFlatHubUrl()
    {
        var scope = CreateScope("Hub", communitySlug: "main", hubSlug: "fps");

        var result = ManageScopeService.GetSiteUrl(scope, isMultiCommunity: false);

        await Assert.That(result).IsEqualTo("/h/fps");
    }

    [Test]
    public async Task GetSiteUrl_SingleCommunity_SpaceScope_ReturnsFlatSpaceUrl()
    {
        var scope = CreateScope("Space", communitySlug: "main", hubSlug: "fps", spaceSlug: "valorant");

        var result = ManageScopeService.GetSiteUrl(scope, isMultiCommunity: false);

        await Assert.That(result).IsEqualTo("/h/fps/valorant");
    }

    [Test]
    public async Task GetSiteUrl_SingleCommunity_CommunityScope_ReturnsRoot()
    {
        var scope = CreateScope("Community", communitySlug: "main");

        var result = ManageScopeService.GetSiteUrl(scope, isMultiCommunity: false);

        await Assert.That(result).IsEqualTo("/");
    }

    // ===== HasPermission Tests =====

    [Test]
    public async Task HasPermission_PermissionExists_ReturnsTrue()
    {
        var scope = CreateScope("Community", permissions:
        [
            ManagePermissionEnum.ViewDashboard,
            ManagePermissionEnum.ManageContent
        ]);

        var result = ManageScopeService.HasPermission(scope, ManagePermissionEnum.ManageContent);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasPermission_PermissionMissing_ReturnsFalse()
    {
        var scope = CreateScope("Community", permissions:
        [
            ManagePermissionEnum.ViewDashboard
        ]);

        var result = ManageScopeService.HasPermission(scope, ManagePermissionEnum.ManageTeam);

        await Assert.That(result).IsFalse();
    }
}
