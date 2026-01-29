namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Report")]
public class ReportDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Who reported
    public int ReporterUserId { get; set; }
    public virtual UserDatabaseEntity ReporterUser { get; set; } = null!;

    // What was reported (one of these is set)
    public int? ReportedPostId { get; set; }
    public virtual PostDatabaseEntity? ReportedPost { get; set; }

    public int? ReportedDiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity? ReportedDiscussion { get; set; }

    public int? ReportedUserId { get; set; }
    public virtual UserDatabaseEntity? ReportedUser { get; set; }

    // Report reason
    public int? ReasonId { get; set; }
    public virtual ReportReasonDatabaseEntity? Reason { get; set; }

    // Additional details from reporter
    public string? Details { get; set; }

    // Status: Pending, Resolved, Dismissed
    public required string Status { get; set; }

    // Timestamps
    public required DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Who resolved (if resolved/dismissed)
    public int? ResolvedByUserId { get; set; }
    public virtual UserDatabaseEntity? ResolvedByUser { get; set; }

    // Resolution note from moderator
    public string? ResolutionNote { get; set; }

    // The entity scope where this report was created (for bubble-up visibility)
    // This is determined from the reported content's location
    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }

    public int? HubId { get; set; }
    public virtual HubDatabaseEntity? Hub { get; set; }

    public int? CommunityId { get; set; }
    public virtual CommunityDatabaseEntity? Community { get; set; }

    // Navigation to comments
    public virtual ICollection<ReportCommentDatabaseEntity> Comments { get; set; } = [];
}
