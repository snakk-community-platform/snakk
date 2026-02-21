namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using System.Security.Claims;

public static class ManageContextEndpoints
{
    public static void MapManageContextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/manage")
            .WithTags("Management")
            .RequireAuthorization();

        group.MapGet("/resolve", ResolveScopeAsync)
            .WithName("ResolveManageScope");
    }

    private static async Task<IResult> ResolveScopeAsync(
        string communitySlug,
        string? hubSlug,
        string? spaceSlug,
        ClaimsPrincipal user,
        SnakkDbContext context,
        IManagePermissionService permissionService)
    {
        var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        // Resolve community
        var community = await context.Communities
            .Where(c => c.Slug == communitySlug)
            .Select(c => new { c.Id, c.Name, c.Slug })
            .FirstOrDefaultAsync();

        if (community == null)
            return Results.NotFound(new { error = "Community not found" });

        // If only community slug provided, return community scope
        if (string.IsNullOrEmpty(hubSlug))
        {
            var communityPermissions = await permissionService.GetPermissionsForScopeAsync(userId, "Community", community.Id);
            if (!communityPermissions.HasAnyPermission)
                return Results.Forbid();

            return Results.Ok(new ManageScopeDto
            {
                ScopeType = "Community",
                ScopeId = community.Id,
                ScopeName = community.Name,
                CommunitySlug = community.Slug,
                CommunityName = community.Name,
                CommunityId = community.Id,
                Permissions = communityPermissions.GetGrantedPermissions()
            });
        }

        // Resolve hub
        var hub = await context.Hubs
            .Where(h => h.Slug == hubSlug && h.CommunityId == community.Id)
            .Select(h => new { h.Id, h.Name, h.Slug })
            .FirstOrDefaultAsync();

        if (hub == null)
            return Results.NotFound(new { error = "Hub not found" });

        // If no space slug, return hub scope
        if (string.IsNullOrEmpty(spaceSlug))
        {
            var hubPermissions = await permissionService.GetPermissionsForScopeAsync(userId, "Hub", hub.Id);
            if (!hubPermissions.HasAnyPermission)
                return Results.Forbid();

            return Results.Ok(new ManageScopeDto
            {
                ScopeType = "Hub",
                ScopeId = hub.Id,
                ScopeName = hub.Name,
                CommunitySlug = community.Slug,
                HubSlug = hub.Slug,
                CommunityName = community.Name,
                CommunityId = community.Id,
                HubName = hub.Name,
                HubId = hub.Id,
                Permissions = hubPermissions.GetGrantedPermissions()
            });
        }

        // Resolve space
        var space = await context.Spaces
            .Where(s => s.Slug == spaceSlug && s.HubId == hub.Id)
            .Select(s => new { s.Id, s.Name, s.Slug })
            .FirstOrDefaultAsync();

        if (space == null)
            return Results.NotFound(new { error = "Space not found" });

        var spacePermissions = await permissionService.GetPermissionsForScopeAsync(userId, "Space", space.Id);
        if (!spacePermissions.HasAnyPermission)
            return Results.Forbid();

        return Results.Ok(new ManageScopeDto
        {
            ScopeType = "Space",
            ScopeId = space.Id,
            ScopeName = space.Name,
            CommunitySlug = community.Slug,
            HubSlug = hub.Slug,
            SpaceSlug = space.Slug,
            CommunityName = community.Name,
            CommunityId = community.Id,
            HubName = hub.Name,
            HubId = hub.Id,
            SpaceName = space.Name,
            SpaceId = space.Id,
            Permissions = spacePermissions.GetGrantedPermissions()
        });
    }
}
