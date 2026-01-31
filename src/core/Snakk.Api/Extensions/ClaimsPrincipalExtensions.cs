using System.Security.Claims;
using Snakk.Domain.ValueObjects;

namespace Snakk.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static UserId? GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        return UserId.From(userIdClaim);
    }

    public static string? GetUserIdString(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public static string? GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    public static string? GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Role)?.Value;
    }

    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        return user.GetRole()?.Equals("Admin", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public static bool IsMod(this ClaimsPrincipal user)
    {
        var role = user.GetRole();
        return (role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) ?? false) ||
               (role?.Equals("Mod", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
