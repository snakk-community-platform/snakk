using System.Security.Claims;

namespace Snakk.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public string? GetCurrentUserDisplayName()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Name)?.Value;
    }

    public string? GetCurrentUserEmail()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Email)?.Value;
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    public string? GetOAuthProvider()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirst("OAuthProvider")?.Value;
    }

    public bool IsEmailVerified()
    {
        var claim = _httpContextAccessor.HttpContext?.User
            .FindFirst("EmailVerified")?.Value;
        return claim == "True";
    }
}
