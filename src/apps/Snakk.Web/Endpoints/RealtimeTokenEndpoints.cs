namespace Snakk.Web.Endpoints;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Snakk.Web.Services;

public static class RealtimeTokenEndpoints
{
    private static readonly JwtSecurityTokenHandler _jwtHandler = new();

    public static void MapRealtimeTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/bff/realtime-token", GetRealtimeToken)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetRealtimeToken(HttpContext context, IConfiguration configuration, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var displayName = context.User.FindFirstValue(ClaimTypes.Name) ?? "";

        var jwtKey = configuration["Realtime:JwtKey"]
            ?? throw new InvalidOperationException("Realtime:JwtKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        const int expiresInSeconds = 3600;

        var currentUser = await apiClient.GetCurrentUserAsync();
        var hidePresence = currentUser?.HidePresence ?? false;
        var avatarUrl = currentUser?.AvatarUrl ?? "";

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Name, displayName)
        };
        if (hidePresence)
            claims.Add(new Claim("presence_hidden", "true"));
        if (!string.IsNullOrEmpty(avatarUrl))
            claims.Add(new Claim("avatar_url", avatarUrl));

        var token = new JwtSecurityToken(
            issuer: "Snakk",
            audience: "Snakk-Realtime",
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(expiresInSeconds),
            signingCredentials: credentials);

        var tokenString = _jwtHandler.WriteToken(token);

        return Results.Ok(new { token = tokenString, expiresInSeconds });
    }
}
