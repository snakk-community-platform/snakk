namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.DTOs.Security;
using Snakk.Application.Services;
using System.Security.Claims;

public static class AdminSecurityEndpoints
{
    public static void MapAdminSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/security")
            .WithTags("Admin - Security & Audit")
            .RequireAuthorization(policy => policy.RequireRole("GlobalAdmin"));

        // Audit Logs
        group.MapGet("/audit-logs", GetAuditLogsAsync)
            .WithName("AdminGetAuditLogs")
            .Produces<AuditLogsResponse>();

        group.MapGet("/audit-logs/{id}", GetAuditLogAsync)
            .WithName("AdminGetAuditLog")
            .Produces<AuditLogDto>();

        // Security Monitoring
        group.MapGet("/failed-logins", GetFailedLoginsAsync)
            .WithName("AdminGetFailedLogins")
            .Produces<FailedLoginsResponse>();

        group.MapGet("/active-sessions", GetActiveSessionsAsync)
            .WithName("AdminGetActiveSessions")
            .Produces<ActiveAdminSessionsResponse>();

        group.MapGet("/suspicious-activities", GetSuspiciousActivitiesAsync)
            .WithName("AdminGetSuspiciousActivities")
            .Produces<SuspiciousActivitiesResponse>();

        // Compliance Tools
        group.MapPost("/user-data-export/{userId}", ExportUserDataAsync)
            .WithName("AdminExportUserData")
            .Produces<UserDataExportDto>();

        group.MapGet("/user-data-export/{exportId}", GetUserDataExportAsync)
            .WithName("AdminGetUserDataExport")
            .Produces<UserDataExportStatusResponse>();
    }

    // ==================== Audit Logs ====================

    private static async Task<IResult> GetAuditLogsAsync(
        [FromQuery] int page = 1,
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? actorUserId = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        ISecurityService securityService = null!)
    {
        if (page < 1)
            return Results.BadRequest(new { error = "Page must be >= 1" });

        var result = await securityService.GetAuditLogsAsync(
            page,
            category,
            action,
            actorUserId,
            fromDate,
            toDate);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAuditLogAsync(
        string id,
        ISecurityService securityService)
    {
        var log = await securityService.GetAuditLogByIdAsync(id);

        if (log is null)
            return Results.NotFound(new { error = "Audit log not found" });

        return Results.Ok(log);
    }

    // ==================== Security Monitoring ====================

    private static async Task<IResult> GetFailedLoginsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int hours = 24,
        ISecurityService securityService = null!)
    {
        if (page < 1)
            return Results.BadRequest(new { error = "Page must be >= 1" });

        if (hours < 1 || hours > 168) // Max 1 week
            return Results.BadRequest(new { error = "Hours must be between 1 and 168" });

        var failedLogins = await securityService.GetFailedLoginsAsync(page, hours);

        // Filter for suspicious IPs (5+ failures)
        var suspiciousIps = failedLogins
            .Where(f => f.AttemptCount >= 5)
            .Select(f => new {
                f.IpAddress,
                failureCount = f.AttemptCount,
                lastAttempt = f.LastAttempt })
            .ToList();

        return TypedResults.Ok(new FailedLoginsResponse(
            failedLogins,
            suspiciousIps.Select(s => new SuspiciousIpResponse(s.IpAddress, s.failureCount, s.lastAttempt)),
            failedLogins.Count,
            page,
            50));
    }

    private static async Task<IResult> GetActiveSessionsAsync(
        ISecurityService securityService)
    {
        var activeSessions = await securityService.GetActiveSessionsAsync();

        return TypedResults.Ok(new ActiveAdminSessionsResponse(activeSessions, activeSessions.Count));
    }

    private static async Task<IResult> GetSuspiciousActivitiesAsync(
        [FromQuery] int page = 1,
        ISecurityService securityService = null!)
    {
        if (page < 1)
            return Results.BadRequest(new { error = "Page must be >= 1" });

        var activities = await securityService.GetSuspiciousActivitiesAsync(page);

        return TypedResults.Ok(new SuspiciousActivitiesResponse(activities, activities.Count));
    }

    // ==================== Compliance Tools ====================

    private static async Task<IResult> ExportUserDataAsync(
        string userId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ISecurityService securityService)
    {
        var adminUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(adminUserId))
            return Results.Unauthorized();

        try
        {
            var result = await securityService.ExportUserDataAsync(
                userId,
                adminUserId,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.FirstOrDefault());

            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { error = "Resource not found." });
        }
    }

    private static Task<IResult> GetUserDataExportAsync(
        string exportId) =>
        // This would retrieve the export status from a job queue
        // For now, return a placeholder
        Task.FromResult<IResult>(TypedResults.Ok(new UserDataExportStatusResponse(
            exportId,
            "pending",
            "Export is being processed")));
}
