namespace Snakk.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Application.Services;
using System.Security.Claims;

public static class AdminSecurityEndpoints
{
    public static void MapAdminSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/security")
            .WithTags("Admin - Security & Audit")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // Audit Logs
        group.MapGet("/audit-logs", GetAuditLogsAsync)
            .WithName("AdminGetAuditLogs");

        group.MapGet("/audit-logs/{id}", GetAuditLogAsync)
            .WithName("AdminGetAuditLog");

        // Security Monitoring
        group.MapGet("/failed-logins", GetFailedLoginsAsync)
            .WithName("AdminGetFailedLogins");

        group.MapGet("/active-sessions", GetActiveSessionsAsync)
            .WithName("AdminGetActiveSessions");

        group.MapGet("/suspicious-activities", GetSuspiciousActivitiesAsync)
            .WithName("AdminGetSuspiciousActivities");

        // Compliance Tools
        group.MapPost("/user-data-export/{userId}", ExportUserDataAsync)
            .WithName("AdminExportUserData");

        group.MapGet("/user-data-export/{exportId}", GetUserDataExportAsync)
            .WithName("AdminGetUserDataExport");
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

        if (log == null)
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
            .Select(f => new
            {
                ipAddress = f.IpAddress,
                failureCount = f.AttemptCount,
                lastAttempt = f.LastAttempt
            })
            .ToList();

        return Results.Ok(new
        {
            failures = failedLogins,
            suspiciousIps,
            total = failedLogins.Count,
            page,
            pageSize = 50
        });
    }

    private static async Task<IResult> GetActiveSessionsAsync(
        ISecurityService securityService)
    {
        var activeSessions = await securityService.GetActiveSessionsAsync();

        return Results.Ok(new
        {
            sessions = activeSessions,
            total = activeSessions.Count
        });
    }

    private static async Task<IResult> GetSuspiciousActivitiesAsync(
        [FromQuery] int page = 1,
        ISecurityService securityService = null!)
    {
        if (page < 1)
            return Results.BadRequest(new { error = "Page must be >= 1" });

        var activities = await securityService.GetSuspiciousActivitiesAsync(page);

        return Results.Ok(new
        {
            activities,
            total = activities.Count
        });
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
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
    }

    private static Task<IResult> GetUserDataExportAsync(
        string exportId)
    {
        // This would retrieve the export status from a job queue
        // For now, return a placeholder
        return Task.FromResult(Results.Ok(new
        {
            exportId,
            status = "pending",
            message = "Export is being processed"
        }));
    }
}
