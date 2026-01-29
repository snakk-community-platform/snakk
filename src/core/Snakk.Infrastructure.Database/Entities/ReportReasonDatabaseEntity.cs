namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("ReportReason")]
public class ReportReasonDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Reason name and description
    public required string Name { get; set; }        // e.g., "Spam", "Harassment", "Off-topic"
    public string? Description { get; set; }         // Longer explanation for users

    // Scope - where this reason is available
    // If all null, it's a global reason (platform-wide)
    // Otherwise, it's specific to that entity and its children
    public int? CommunityId { get; set; }
    public virtual CommunityDatabaseEntity? Community { get; set; }

    public int? HubId { get; set; }
    public virtual HubDatabaseEntity? Hub { get; set; }

    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }

    // Who created this reason (for entity-specific reasons)
    public int? CreatedByUserId { get; set; }
    public virtual UserDatabaseEntity? CreatedByUser { get; set; }

    public required DateTime CreatedAt { get; set; }

    // Ordering for display
    public int DisplayOrder { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
