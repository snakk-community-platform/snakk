using Microsoft.AspNetCore.Authorization;

namespace Snakk.Api.Authorization;

/// <summary>
/// Authorization requirement for checking user permissions with hierarchical scope support
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionName { get; }
    public string? Scope { get; }
    public string? ScopeIdRouteKey { get; }

    public PermissionRequirement(string permissionName, string? scope = null, string? scopeIdRouteKey = null)
    {
        PermissionName = permissionName;
        Scope = scope;
        ScopeIdRouteKey = scopeIdRouteKey;
    }
}
