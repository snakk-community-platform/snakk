namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Security;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class SecurityService(SnakkDbContext context, IDbContextFactory<SnakkDbContext> dbFactory) : ISecurityService
{
    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    private static IQueryable<Database.Entities.AuditLogDatabaseEntity> ApplyAuditLogFilters(
        IQueryable<Database.Entities.AuditLogDatabaseEntity> query,
        string? category, string? action, string? actorUserId,
        DateTime? from, DateTime? to)
    {
        if (!string.IsNullOrEmpty(category))
            query = query.Where(a => a.Category == category);
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(actorUserId))
            query = query.Where(a => a.ActorUser!.PublicId == actorUserId);
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);
        return query;
    }

    public async Task<AuditLogsResponse> GetAuditLogsAsync(
        int page,
        string? category = null,
        string? action = null,
        string? actorUserId = null,
        string? fromDate = null,
        string? toDate = null)
    {
        const int pageSize = 50;
        var offset = (page - 1) * pageSize;

        DateTime? from = !string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var f) ? f : null;
        DateTime? to   = !string.IsNullOrEmpty(toDate)   && DateTime.TryParse(toDate,   out var t) ? t : null;

        var countTask = ReadAsync(db =>
            ApplyAuditLogFilters(db.AuditLogs.AsQueryable(), category, action, actorUserId, from, to)
                .CountAsync());

        var listTask = ReadAsync(db =>
            ApplyAuditLogFilters(db.AuditLogs.AsQueryable(), category, action, actorUserId, from, to)
                .OrderByDescending(a => a.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.PublicId,
                    Action = a.Action,
                    Category = a.Category,
                    TargetType = a.TargetType,
                    TargetId = a.TargetId,
                    Details = a.Details,
                    IpAddress = a.IpAddress,
                    Success = a.Success,
                    CreatedAt = a.CreatedAt,
                    Severity = ((AuditLogSeverityEnum)a.SeverityId).ToString(),
                    ActorUsername = a.ActorUser != null ? a.ActorUser.DisplayName : null,
                })
                .ToListAsync());

        await Task.WhenAll(countTask, listTask);

        return new AuditLogsResponse
        {
            Logs = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AuditLogDto?> GetAuditLogByIdAsync(string id) =>
        await context.AuditLogs
            .Where(a => a.PublicId == id)
            .Select(a => new AuditLogDto
            {
                Id = a.PublicId,
                Action = a.Action,
                Category = a.Category,
                TargetType = a.TargetType,
                TargetId = a.TargetId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                Success = a.Success,
                CreatedAt = a.CreatedAt,

                Severity = ((AuditLogSeverityEnum)a.SeverityId).ToString(),

                ActorUsername = a.ActorUser != null ? a.ActorUser.DisplayName : null,
            })
            .FirstOrDefaultAsync();

    public async Task<List<FailedLoginDto>> GetFailedLoginsAsync(int page, int hours = 24)
    {
        const int pageSize = 50;
        var offset = (page - 1) * pageSize;
        var since = DateTime.UtcNow.AddHours(-hours);

        return await context.AuditLogs
            .Where(a =>
                a.Action == "Login"
                && !a.Success
                && a.CreatedAt >= since)
            .GroupBy(a => a.IpAddress)
            .Select(g => new FailedLoginDto
            {
                IpAddress = g.Key ?? "unknown",
                AttemptCount = g.Count(),
                LastAttempt = g.Max(a => a.CreatedAt),
                Username = g
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.ActorUser != null ? a.ActorUser.DisplayName : null)
                    .First()
            })
            .OrderByDescending(f => f.AttemptCount)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<ActiveSessionDto>> GetActiveSessionsAsync()
    {
        var now = DateTime.UtcNow;

        return await context.RefreshTokens
            .Where(t =>
                t.ExpiresAt > now
                && t.RevokedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new ActiveSessionDto
            {
                Id = t.TokenValue,
                UserId = t.User.PublicId,
                Username = t.User.DisplayName ?? "",
                Email = t.User.Email,
                CreatedAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt
            })
            .ToListAsync();
    }

    public async Task<List<SuspiciousActivityDto>> GetSuspiciousActivitiesAsync(int page, int hours = 24)
    {
        const int pageSize = 50;
        var offset = (page - 1) * pageSize;
        var since = DateTime.UtcNow.AddHours(-hours);

        return await context.AuditLogs
            .Where(a =>
                a.CreatedAt >= since
                && a.SeverityId == (int)AuditLogSeverityEnum.Warning)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(a => new SuspiciousActivityDto
            {
                Id = a.PublicId,
                Action = a.Action,
                Category = a.Category,
                TargetType = a.TargetType,
                TargetId = a.TargetId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt,

                Severity = ((AuditLogSeverityEnum)a.SeverityId).ToString(),

                ActorUsername = a.ActorUser != null ? a.ActorUser.DisplayName : null,
            })
            .ToListAsync();
    }

    public async Task<UserDataExportDto> ExportUserDataAsync(
        string userId,
        string adminUserId,
        string? ipAddress,
        string? userAgent)
    {
        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .Select(u => new {
                u.Id,
                u.DisplayName,
                u.Email })
            .FirstOrDefaultAsync();

        if (user is null)
            throw new InvalidOperationException("User not found");

        var exportId = Guid.NewGuid().ToString("N");

        // Log the export request
        await LogAuditAsync(
            action: "ExportUserData",
            category: "Compliance",
            actorUserId: adminUserId,
            targetType: "User",
            targetId: userId,
            details: $"GDPR data export requested for user {user.Email}",
            ipAddress: ipAddress,
            userAgent: userAgent,
            success: true,
            severity: AuditLogSeverityEnum.Info);

        return new UserDataExportDto
        {
            ExportId = exportId,
            Message = $"User data export initiated for {user.DisplayName}"
        };
    }

    public async Task LogAuditAsync(
        string action,
        string category,
        string? actorUserId = null,
        string? targetType = null,
        string? targetId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool success = true,
        AuditLogSeverityEnum severity = AuditLogSeverityEnum.Info)
    {
        int? actorUserIdInt = null;

        if (!string.IsNullOrEmpty(actorUserId))
        {
            var actorUser = await context.Users
                .Where(u => u.PublicId == actorUserId)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (actorUser != 0)
                actorUserIdInt = actorUser;
        }

        var auditLog = new AuditLogDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            ActorUserId = actorUserIdInt,
            Action = action,
            Category = category,
            TargetType = targetType,
            TargetId = targetId,
            Details = details,
            IpAddress = ipAddress,
            Success = success,
            SeverityId = (int)severity,
            CreatedAt = DateTime.UtcNow
        };

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();
    }
}
