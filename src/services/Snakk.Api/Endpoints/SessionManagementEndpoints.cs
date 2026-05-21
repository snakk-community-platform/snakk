namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using System.Security.Claims;

public static class SessionManagementEndpoints
{
    public static void MapSessionManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sessions")
            .WithTags("Session Management")
            .RequireAuthorization();

        group.MapGet("/", GetActiveSessionsAsync)
            .WithName("GetActiveSessions");

        group.MapDelete("/{sessionId}", RevokeSessionAsync)
            .WithName("RevokeSession")
            .RequireRateLimiting("flood-post");

        group.MapPost("/revoke-all", RevokeAllSessionsAsync)
            .WithName("RevokeAllSessions")
            .RequireRateLimiting("flood-post");

        group.MapGet("/login-history", GetLoginHistoryAsync)
            .WithName("GetLoginHistory");

        // Cookie-based token refresh (reads refresh_token cookie)
        app.MapPost("/sessions/refresh", RefreshTokenFromCookieAsync)
            .WithName("RefreshSessionToken")
            .WithTags("Session Management")
            .RequireRateLimiting("auth")
            .AllowAnonymous();
    }

    private static async Task<IResult> GetActiveSessionsAsync(
        HttpContext httpContext,
        ISessionManagementService sessionService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var tokenHash = httpContext.Request.Headers["X-Current-Refresh-Token-Hash"].FirstOrDefault();
        var result = await sessionService.GetActiveSessionsAsync(userId, tokenHash, ct);

        return Results.Ok(new ActiveSessionsResponse(result.ActiveCount, result.Sessions));
    }

    private static async Task<IResult> RevokeSessionAsync(
        string sessionId,
        HttpContext httpContext,
        ISessionManagementService sessionService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var success = await sessionService.RevokeSessionAsync(sessionId, userId, ct);
        if (!success) return Results.NotFound(new { error = "Session not found." });

        return Results.Ok(new MessageResponse("Session revoked."));
    }

    private static async Task<IResult> RevokeAllSessionsAsync(
        HttpContext httpContext,
        ISessionManagementService sessionService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        await sessionService.RevokeAllExceptAsync(userId, string.Empty, ct);

        return Results.Ok(new MessageResponse("All sessions revoked."));
    }

    private static async Task<IResult> RefreshTokenFromCookieAsync(
        HttpContext httpContext,
        AuthenticationUseCase authUseCase,
        CancellationToken ct)
    {
        var refreshToken = httpContext.Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Results.Unauthorized();

        var result = await authUseCase.RefreshTokenAsync(refreshToken);
        if (!result.IsSuccess)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            accessToken = result.Value.newRefreshToken.Value,
            message = "Token refreshed."
        });
    }

    private static async Task<IResult> GetLoginHistoryAsync(
        HttpContext httpContext,
        ISessionManagementService sessionService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var result = await sessionService.GetLoginHistoryAsync(userId, limit: 20, ct);

        return Results.Ok(result.Entries.Select(e => new
        {
            id = e.Id,
            createdAt = e.CreatedAt,
            success = e.Success,
            ipAddress = e.IpAddress,
            deviceHint = e.DeviceHint
        }));
    }
}
