namespace Snakk.Web.Endpoints;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public static class RealtimeTokenEndpoints
{
    public static void MapRealtimeTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/bff/realtime-token", GetRealtimeToken)
            .RequireAuthorization();
    }

    private static IResult GetRealtimeToken(HttpContext context, IConfiguration configuration)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var displayName = context.User.FindFirstValue(ClaimTypes.Name) ?? "";

        var jwtKey = configuration["Realtime:JwtKey"]
            ?? throw new InvalidOperationException("Realtime:JwtKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        const int expiresInSeconds = 3600;

        var token = new JwtSecurityToken(
            issuer: "Snakk",
            audience: "Snakk-Realtime",
            claims: [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.Name, displayName)
            ],
            expires: DateTime.UtcNow.AddSeconds(expiresInSeconds),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Results.Ok(new { token = tokenString, expiresInSeconds });
    }
}
