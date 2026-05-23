using System.Security.Claims;

namespace Snakk.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? GetCurrentUserId() =>
        httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? GetCurrentUserDisplayName() =>
        httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Name)?.Value;

    public string? GetCurrentUserEmail() =>
        httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Email)?.Value;

    public bool IsAuthenticated() =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsEmailVerified()
    {
        var claim = httpContextAccessor.HttpContext?.User
            .FindFirst("EmailVerified")?.Value;

        return claim == "True";
    }

    public string? GetCurrentUserRole() =>
        httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Role)?.Value;

    public string? GetAvatarFileName() =>
        httpContextAccessor.HttpContext?.User
            .FindFirst("AvatarFileName")?.Value;
}
