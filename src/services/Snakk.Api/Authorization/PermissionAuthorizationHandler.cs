using Microsoft.AspNetCore.Authorization;
using Snakk.Api.Services;
using Snakk.Application.Services;

namespace Snakk.Api.Authorization;

/// <summary>
/// Authorization handler that checks user permissions with hierarchical scope support
/// </summary>
public class PermissionAuthorizationHandler(
    IPermissionService permissionService,
    ICurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = currentUserService.GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Permission check failed: No user ID found");
            context.Fail();
            return;
        }

        // Extract scope public ID from route if specified
        string? scopePublicId = null;

        if (!string.IsNullOrEmpty(requirement.ScopeIdRouteKey))
        {
            var httpContext = httpContextAccessor.HttpContext;

            if (httpContext is not null
                && httpContext.Request.RouteValues.TryGetValue(requirement.ScopeIdRouteKey, out var routeValue))
            {
                scopePublicId = routeValue?.ToString();
            }
        }

        var hasPermission = await permissionService.UserHasPermissionAsync(
            userId,
            requirement.PermissionName,
            requirement.Scope,
            scopePublicId);

        if (hasPermission)
        {
            logger.LogDebug(
                "User {UserId} granted access to {Permission} in {Scope}:{ScopePublicId}",
                userId,
                requirement.PermissionName,
                requirement.Scope ?? "global",
                scopePublicId);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "User {UserId} denied access to {Permission} in {Scope}:{ScopePublicId}",
                userId,
                requirement.PermissionName,
                requirement.Scope ?? "global",
                scopePublicId);
            context.Fail();
        }
    }
}
