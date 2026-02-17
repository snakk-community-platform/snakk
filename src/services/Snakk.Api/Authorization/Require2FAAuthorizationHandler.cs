namespace Snakk.Api.Authorization;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using IAuthorizationService = Snakk.Application.Services.IAuthorizationService;

/// <summary>
/// Authorization handler that enforces 2FA for admin users
/// </summary>
public class Require2FAAuthorizationHandler : AuthorizationHandler<Require2FARequirement>
{
    private readonly IAuthorizationService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Require2FAAuthorizationHandler(
        IAuthorizationService authService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        Require2FARequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            context.Fail();
            return;
        }

        // Get user ID from claims
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            context.Fail();
            return;
        }

        // Check if user has admin roles
        var hasAdminRole = context.User.IsInRole("GlobalAdmin") || context.User.IsInRole("CommunityAdmin");
        if (!hasAdminRole)
        {
            // Not an admin, no 2FA required
            context.Succeed(requirement);
            return;
        }

        // Admin user - check if 2FA is enabled
        var has2FA = await _authService.UserHas2FAEnabledAsync(userIdClaim.Value);

        if (!has2FA)
        {
            // Admin without 2FA enabled - fail authorization
            httpContext.Response.StatusCode = 403;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "2FA_REQUIRED",
                message = "Two-factor authentication is required for admin accounts",
                setupUrl = "/api/auth/2fa/setup"
            });
            context.Fail();
            return;
        }

        // Check if 2FA was verified in this session (claim added during login)
        var twoFactorVerified = context.User.FindFirst("2fa_verified")?.Value == "true";
        if (!twoFactorVerified)
        {
            // 2FA enabled but not verified in this session
            httpContext.Response.StatusCode = 403;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "2FA_NOT_VERIFIED",
                message = "Please verify your 2FA code",
                verifyUrl = "/api/auth/2fa/verify"
            });
            context.Fail();
            return;
        }

        // All checks passed
        context.Succeed(requirement);
    }
}

public class Require2FARequirement : IAuthorizationRequirement
{
}
