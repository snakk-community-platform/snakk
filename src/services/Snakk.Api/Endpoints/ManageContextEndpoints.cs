namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using System.Security.Claims;

public static class ManageContextEndpoints
{
    public static void MapManageContextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/manage")
            .WithTags("Management")
            .RequireAuthorization();

        group.MapGet("/resolve", ResolveScopeAsync)
            .WithName("ResolveManageScope")
            .Produces<ManageScopeDto>();
    }

    private static async Task<IResult> ResolveScopeAsync(
        string communityId,
        string? hubId,
        string? spaceId,
        ClaimsPrincipal user,
        IManageScopeDataService scopeData,
        IManagePermissionService permissionService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        // Resolve community
        var community = await scopeData.GetCommunityByPublicIdAsync(communityId, ct);

        if (community is null)
            return Results.NotFound(new { error = "Community not found" });

        // If only community provided, return community scope
        if (string.IsNullOrEmpty(hubId))
        {
            var communityPermissions = await permissionService.GetPermissionsForScopeAsync(
                userId,
                "Community",
                communityId);

            if (!communityPermissions.HasAnyPermission)
                return Results.Forbid();

            return Results.Ok(new ManageScopeDto
            {
                ScopeType = "Community",
                ScopePublicId = community.PublicId,
                ScopeName = community.Name,
                CommunitySlug = community.Slug,
                CommunityName = community.Name,
                CommunityPublicId = community.PublicId,
                Permissions = communityPermissions.GetGrantedPermissions()
            });
        }

        // Resolve hub (must belong to this community)
        var hub = await scopeData.GetHubByPublicIdInCommunityAsync(hubId, community.DbId, ct);

        if (hub is null)
            return Results.NotFound(new { error = "Hub not found" });

        // If no space, return hub scope
        if (string.IsNullOrEmpty(spaceId))
        {
            var hubPermissions = await permissionService.GetPermissionsForScopeAsync(userId, "Hub", hubId);

            if (!hubPermissions.HasAnyPermission)
                return Results.Forbid();

            return Results.Ok(new ManageScopeDto
            {
                ScopeType = "Hub",
                ScopePublicId = hub.PublicId,
                ScopeName = hub.Name,
                CommunitySlug = community.Slug,
                HubSlug = hub.Slug,
                CommunityName = community.Name,
                HubName = hub.Name,
                CommunityPublicId = community.PublicId,
                Permissions = hubPermissions.GetGrantedPermissions()
            });
        }

        // Resolve space (must belong to this hub)
        var space = await scopeData.GetSpaceByPublicIdInHubAsync(spaceId, hub.DbId, ct);

        if (space is null)
            return Results.NotFound(new { error = "Space not found" });

        var spacePermissions = await permissionService.GetPermissionsForScopeAsync(userId, "Space", spaceId);

        if (!spacePermissions.HasAnyPermission)
            return Results.Forbid();

        return Results.Ok(new ManageScopeDto
        {
            ScopeType = "Space",
            ScopePublicId = space.PublicId,
            ScopeName = space.Name,
            CommunitySlug = community.Slug,
            HubSlug = hub.Slug,
            SpaceSlug = space.Slug,
            CommunityName = community.Name,
            HubName = hub.Name,
            SpaceName = space.Name,
            CommunityPublicId = community.PublicId,
            Permissions = spacePermissions.GetGrantedPermissions()
        });
    }
}
