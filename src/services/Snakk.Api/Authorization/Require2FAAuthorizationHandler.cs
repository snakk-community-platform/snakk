namespace Snakk.Api.Authorization;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using IAuthorizationService = Snakk.Application.Services.IAuthorizationService;

/// <summary>
/// Authorization handler that enforces 2FA for admin users
/// </summary>
public class Require2FAAuthorizationHandler(
    IAuthorizationService authService,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<Require2FARequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        Require2FARequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            context.Fail();
            return;
        }

        // Get user ID from claims
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
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
        var has2FA = await authService.UserHas2FAEnabledAsync(userIdClaim.Value);

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

        // Check that the JWT token carries the tfa=1 claim (emitted when 2FA was used at login)
        var twoFactorVerified = context.User.FindFirst(Snakk.Application.Auth.CustomClaimTypes.TwoFactorEnabled)?.Value == "1";

        if (!twoFactorVerified)
        {
            // 2FA enabled on the account but token was issued without 2FA — re-authentication required
            httpContext.Response.StatusCode = 403;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = "2FA_NOT_VERIFIED",
                message = "Please sign in with two-factor authentication to access this resource",
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
