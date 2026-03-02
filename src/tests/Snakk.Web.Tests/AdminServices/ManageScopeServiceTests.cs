extern alias AdminWeb;

using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using AdminWeb::Snakk.AdminWeb.Services;
using Snakk.Application.DTOs.Management;
using Snakk.Shared.Enums;
using Snakk.Web.Tests.Helpers;

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
        return new ManageScopeDto
        {
            ScopeType = scopeType,
            ScopeName = $"Test {scopeType}",
            CommunitySlug = communitySlug,
            HubSlug = hubSlug,
            SpaceSlug = spaceSlug,
            CommunityName = "Test Community",
            HubName = hubSlug != null ? "Test Hub" : null,
            SpaceName = spaceSlug != null ? "Test Space" : null,
            Permissions = permissions ?? []
        };
    }

    private static (ManageScopeService Service, MockApiHandler Handler) CreateService()
    {
        var handler = new MockApiHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var logger = new Mock<ILogger<ManageScopeService>>();
        var service = new ManageScopeService(httpClient, logger.Object);
        return (service, handler);
    }

    // ===== GetManageUrl Tests =====

    [Test]
    public async Task GetManageUrl_CommunityScope_ReturnsCommunityManageUrl()
    {
        var scope = CreateScope("Community", communitySlug: "gaming");

        var result = ManageScopeService.GetManageUrl(scope);

        await Assert.That(result).IsEqualTo("/c/gaming/manage");
    }

    [Test]
    public async Task GetManageUrl_HubScope_ReturnsHubManageUrl()
    {
        var scope = CreateScope("Hub", communitySlug: "gaming", hubSlug: "fps");

        var result = ManageScopeService.GetManageUrl(scope);

        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/manage");
    }

    [Test]
    public async Task GetManageUrl_SpaceScope_ReturnsSpaceManageUrl()
    {
        var scope = CreateScope("Space", communitySlug: "gaming", hubSlug: "fps", spaceSlug: "valorant");

        var result = ManageScopeService.GetManageUrl(scope);

        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/s/valorant/manage");
    }

    // ===== GetSiteUrl Tests =====

    [Test]
    public async Task GetSiteUrl_CommunityScope_ReturnsCommunityUrl()
    {
        var scope = CreateScope("Community", communitySlug: "gaming");

        var result = ManageScopeService.GetSiteUrl(scope);

        await Assert.That(result).IsEqualTo("/c/gaming");
    }

    [Test]
    public async Task GetSiteUrl_HubScope_ReturnsHubUrl()
    {
        var scope = CreateScope("Hub", communitySlug: "gaming", hubSlug: "fps");

        var result = ManageScopeService.GetSiteUrl(scope);

        await Assert.That(result).IsEqualTo("/c/gaming/h/fps");
    }

    [Test]
    public async Task GetSiteUrl_SpaceScope_ReturnsSpaceUrl()
    {
        var scope = CreateScope("Space", communitySlug: "gaming", hubSlug: "fps", spaceSlug: "valorant");

        var result = ManageScopeService.GetSiteUrl(scope);

        await Assert.That(result).IsEqualTo("/c/gaming/h/fps/valorant");
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

    // ===== ResolveScopeAsync Tests =====

    [Test]
    public async Task ResolveScopeAsync_NotFound_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetupResponse("/api/manage/resolve", HttpStatusCode.NotFound);

        var result = await service.ResolveScopeAsync("nonexistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveScopeAsync_Forbidden_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetupResponse("/api/manage/resolve", HttpStatusCode.Forbidden);

        var result = await service.ResolveScopeAsync("restricted");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveScopeAsync_HttpRequestException_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetupResponse("/api/manage/resolve", _ =>
            throw new HttpRequestException("Connection refused"));

        var result = await service.ResolveScopeAsync("gaming");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveScopeAsync_Success_ReturnsDeserializedScope()
    {
        var (service, handler) = CreateService();
        var expectedScope = CreateScope("Community", communitySlug: "gaming", permissions:
        [
            ManagePermissionEnum.ViewDashboard,
            ManagePermissionEnum.ManageContent
        ]);

        handler.SetupJsonResponse("/api/manage/resolve", expectedScope);

        var result = await service.ResolveScopeAsync("gaming");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ScopeType).IsEqualTo("Community");
        await Assert.That(result.CommunitySlug).IsEqualTo("gaming");
        await Assert.That(result.ScopeName).IsEqualTo("Test Community");
    }
}
