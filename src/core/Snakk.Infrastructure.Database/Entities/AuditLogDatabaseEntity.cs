namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("AuditLog")]
public class AuditLogDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Who performed the action
    public int? ActorUserId { get; set; }
    public virtual UserDatabaseEntity? ActorUser { get; set; }

    // What action was performed
    public required string Action { get; set; } // e.g., "BanUser", "DeleteDiscussion", "ChangeUserRole"
    public required string Category { get; set; } // e.g., "User", "Content", "Moderation", "Security", "System"

    // Target of the action
    public string? TargetType { get; set; } // e.g., "User", "Discussion", "Post", "Community"
    public string? TargetId { get; set; } // PublicId of the target entity
    public string? TargetDisplayName { get; set; } // For easier display

    // Action details
    public string? Details { get; set; } // JSON or text description of the action
    public string? Reason { get; set; } // Reason for the action (e.g., ban reason)

    // Request metadata
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Result
    public bool Success { get; set; } // Whether the action succeeded
    public string? ErrorMessage { get; set; } // If failed, why

    // Timestamp
    public required DateTime CreatedAt { get; set; }

    // Severity level for filtering
    public int SeverityId { get; set; } // Maps to AuditLogSeverityEnum (Info, Warning, Error, Critical)
}
