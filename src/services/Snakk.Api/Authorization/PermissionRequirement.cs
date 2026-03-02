using Microsoft.AspNetCore.Authorization;

namespace Snakk.Api.Authorization;

/// <summary>
/// Authorization requirement for checking user permissions with hierarchical scope support
/// </summary>
public class PermissionRequirement(
    string permissionName,
    string? scope = null,
    string? scopeIdRouteKey = null) : IAuthorizationRequirement
{
    public string PermissionName { get; } = permissionName;
    public string? Scope { get; } = scope;
    public string? ScopeIdRouteKey { get; } = scopeIdRouteKey;
}
