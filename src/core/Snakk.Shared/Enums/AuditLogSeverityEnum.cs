namespace Snakk.Shared.Enums;

public enum AuditLogSeverityEnum
{
    Info = 1,      // Informational events (e.g., user login, settings change)
    Warning = 2,   // Warning events (e.g., failed login attempt, suspicious activity)
    Error = 3,     // Error events (e.g., operation failed, validation error)
    Critical = 4   // Critical events (e.g., security breach, data loss, system failure)
}
