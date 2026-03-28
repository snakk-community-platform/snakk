namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class SessionManagementEndpoints
{
    public static void MapSessionManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions")
            .WithTags("Session Management")
            .RequireAuthorization();

        group.MapGet("/", GetActiveSessionsAsync)
            .WithName("GetActiveSessions")
            .Produces<ActiveSessionsResponse>();

        group.MapDelete("/{sessionId}", RevokeSessionAsync)
            .WithName("RevokeSession")
            .Produces<MessageResponse>();

        group.MapPost("/revoke-all", RevokeAllSessionsAsync)
            .WithName("RevokeAllSessions")
            .Produces<MessageResponse>();

        group.MapPost("/refresh", RefreshTokenAsync)
            .WithName("SessionRefreshToken")
            .Produces<Application.DTOs.Auth.SessionRefreshResponse>()
            .AllowAnonymous(); // Needs refresh token from cookie
    }

    private static async Task<IResult> GetActiveSessionsAsync(
        HttpContext httpContext,
        ISessionManagementService sessionService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var currentToken = httpContext.Request.Cookies["refresh_token"];
        var result = await sessionService.GetActiveSessionsAsync(userIdClaim.Value, currentToken);

        return TypedResults.Ok(new ActiveSessionsResponse(result.ActiveCount, result.Sessions));
    }

    private static async Task<IResult> RevokeSessionAsync(
        string sessionId,
        HttpContext httpContext,
        ISessionManagementService sessionService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var success = await sessionService.RevokeSessionAsync(sessionId, userIdClaim.Value);

        if (!success)
            return Results.NotFound(new { error = "Session not found" });

        return TypedResults.Ok(new MessageResponse("Session revoked successfully"));
    }

    private static async Task<IResult> RevokeAllSessionsAsync(
        HttpContext httpContext,
        ITokenService tokenService)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = UserId.From(userIdClaim.Value);
        await tokenService.RevokeAllUserTokensAsync(userId, "User revoked all sessions");

        // Clear current session cookies
        httpContext.Response.Cookies.Delete("access_token");
        httpContext.Response.Cookies.Delete("refresh_token");

        return TypedResults.Ok(new MessageResponse("All sessions revoked successfully. You have been logged out."));
    }

    private static async Task<IResult> RefreshTokenAsync(
        HttpContext httpContext,
        ITokenService tokenService)
    {
        var refreshToken = httpContext.Request.Cookies["refresh_token"];

        if (string.IsNullOrEmpty(refreshToken))
            return Results.Unauthorized();

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var newAccessToken = await tokenService.RefreshAccessTokenAsync(refreshToken, ipAddress, httpContext.Request.Headers.UserAgent.ToString());

        if (newAccessToken is null)
        {
            // Refresh token invalid or expired
            httpContext.Response.Cookies.Delete("access_token");
            httpContext.Response.Cookies.Delete("refresh_token");

            return Results.Unauthorized();
        }

        // Update access token cookie
        httpContext.Response.Cookies.Append("access_token", newAccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        return TypedResults.Ok(new Application.DTOs.Auth.SessionRefreshResponse
        {
            AccessToken = newAccessToken,
            Message = "Token refreshed successfully"
        });
    }
}
