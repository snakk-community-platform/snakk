namespace Snakk.Application.DTOs.Security;

public class AuditLogDto
{
    public required string Id { get; set; }
    public string? ActorUsername { get; set; }
    public required string Action { get; set; }
    public required string Category { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public bool Success { get; set; }
    public required string Severity { get; set; }
    public required DateTime CreatedAt { get; set; }
}

public class AuditLogsResponse
{
    public required List<AuditLogDto> Logs { get; set; }
    public required int Total { get; set; }
    public required int Page { get; set; }
    public required int PageSize { get; set; }
}

public class FailedLoginDto
{
    public required string IpAddress { get; set; }
    public required int AttemptCount { get; set; }
    public required DateTime LastAttempt { get; set; }
    public string? Username { get; set; }
}

public class ActiveSessionDto
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

public class SuspiciousActivityDto
{
    public required string Id { get; set; }
    public string? ActorUsername { get; set; }
    public required string Action { get; set; }
    public required string Category { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public required string Severity { get; set; }
    public required DateTime CreatedAt { get; set; }
}

public class UserDataExportDto
{
    public required string ExportId { get; set; }
    public required string Message { get; set; }
}
