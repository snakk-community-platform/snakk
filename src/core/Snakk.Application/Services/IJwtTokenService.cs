namespace Snakk.Application.Services;

using System.Security.Claims;
using Snakk.Domain.Entities;

public interface IJwtTokenService
{
    string GenerateToken(
        string userId,
        string displayName,
        string? email,
        bool emailVerified,
        string? oAuthProvider,
        string? role = null,
        string? avatarFileName = null);

    string GenerateToken(User user);

    ClaimsPrincipal? ValidateToken(string token);
}
