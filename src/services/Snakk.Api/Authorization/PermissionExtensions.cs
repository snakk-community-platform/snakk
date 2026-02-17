using Microsoft.AspNetCore.Authorization;

namespace Snakk.Api.Authorization;

/// <summary>
/// Extension methods for permission-based authorization in endpoints
/// </summary>
public static class PermissionExtensions
{
    /// <summary>
    /// Requires the user to have a specific permission (global scope)
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionName)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(new AuthorizeAttribute
        {
            Policy = $"Permission:{permissionName}"
        });
    }

    /// <summary>
    /// Requires the user to have a specific permission within a scope (with hierarchical checking)
    /// </summary>
    /// <param name="permissionName">The permission name to check</param>
    /// <param name="scope">The scope type (Community, Hub, Space, Discussion, Post)</param>
    /// <param name="scopeIdRouteKey">The route parameter name containing the scope ID</param>
    public static TBuilder RequirePermission<TBuilder>(
        this TBuilder builder,
        string permissionName,
        string scope,
        string scopeIdRouteKey)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(policy => policy.AddRequirements(
            new PermissionRequirement(permissionName, scope, scopeIdRouteKey)));
    }

    /// <summary>
    /// Convenience method: Requires user to be a Global Admin
    /// </summary>
    public static TBuilder RequireGlobalAdmin<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequirePermission("GlobalAdmin");
    }

    /// <summary>
    /// Convenience method: Requires user to be Community Admin or higher for the specified community
    /// </summary>
    public static TBuilder RequireCommunityAdmin<TBuilder>(this TBuilder builder, string communityIdRouteKey = "communityId")
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequirePermission("ManageCommunity", "Community", communityIdRouteKey);
    }

    /// <summary>
    /// Convenience method: Requires user to be Hub Moderator or higher for the specified hub
    /// </summary>
    public static TBuilder RequireHubModerator<TBuilder>(this TBuilder builder, string hubIdRouteKey = "hubId")
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequirePermission("ManageHub", "Hub", hubIdRouteKey);
    }

    /// <summary>
    /// Convenience method: Requires user to be Space Moderator or higher for the specified space
    /// </summary>
    public static TBuilder RequireSpaceModerator<TBuilder>(this TBuilder builder, string spaceIdRouteKey = "spaceId")
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequirePermission("ManageSpace", "Space", spaceIdRouteKey);
    }

    /// <summary>
    /// Convenience method: Requires any moderator role (Community, Hub, or Space) for the specified content
    /// </summary>
    public static TBuilder RequireAnyModerator<TBuilder>(this TBuilder builder, string scope, string scopeIdRouteKey)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequirePermission("Moderate", scope, scopeIdRouteKey);
    }
}
