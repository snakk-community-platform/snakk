using Microsoft.AspNetCore.Authorization;
using Snakk.Api.Services;
using Snakk.Application.Services;

namespace Snakk.Api.Authorization;

/// <summary>
/// Authorization handler that checks user permissions with hierarchical scope support
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = _currentUserService.GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Permission check failed: No user ID found");
            context.Fail();
            return;
        }

        // Extract scope ID from route if specified
        int? scopeId = null;
        if (!string.IsNullOrEmpty(requirement.ScopeIdRouteKey))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Request.RouteValues.TryGetValue(requirement.ScopeIdRouteKey, out var routeValue))
            {
                if (int.TryParse(routeValue?.ToString(), out var parsedId))
                {
                    scopeId = parsedId;
                }
            }
        }

        var hasPermission = await _permissionService.UserHasPermissionAsync(
            userId,
            requirement.PermissionName,
            requirement.Scope,
            scopeId);

        if (hasPermission)
        {
            _logger.LogDebug("User {UserId} granted access to {Permission} in {Scope}:{ScopeId}",
                userId, requirement.PermissionName, requirement.Scope ?? "global", scopeId);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("User {UserId} denied access to {Permission} in {Scope}:{ScopeId}",
                userId, requirement.PermissionName, requirement.Scope ?? "global", scopeId);
            context.Fail();
        }
    }
}
