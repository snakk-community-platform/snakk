namespace Snakk.Application.Services;

using Snakk.Application.DTOs.Security;
using Snakk.Shared.Enums;

public interface ISecurityService
{
    Task<AuditLogsResponse> GetAuditLogsAsync(
        int page,
        string? category = null,
        string? action = null,
        string? actorUserId = null,
        string? fromDate = null,
        string? toDate = null);

    Task<AuditLogDto?> GetAuditLogByIdAsync(string id);

    Task<List<FailedLoginDto>> GetFailedLoginsAsync(int page, int hours = 24);

    Task<List<ActiveSessionDto>> GetActiveSessionsAsync();

    Task<List<SuspiciousActivityDto>> GetSuspiciousActivitiesAsync(int page, int hours = 24);

    Task<UserDataExportDto> ExportUserDataAsync(string userId, string adminUserId, string? ipAddress, string? userAgent);

    Task LogAuditAsync(
        string action,
        string category,
        string? actorUserId = null,
        string? targetType = null,
        string? targetId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool success = true,
        AuditLogSeverityEnum severity = AuditLogSeverityEnum.Info);
}
