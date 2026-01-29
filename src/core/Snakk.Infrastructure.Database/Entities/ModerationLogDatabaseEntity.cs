namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("ModerationLog")]
public class ModerationLogDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    // Who performed the action
    public int ActorUserId { get; set; }
    public virtual UserDatabaseEntity ActorUser { get; set; } = null!;

    // Action type: e.g., "DeletePost", "DeleteDiscussion", "BanUser", "UnbanUser", 
    // "AssignRole", "RevokeRole", "ResolveReport", "DismissReport", "EditPost", "LockDiscussion"
    public required string Action { get; set; }

    // Target entity (the thing that was moderated)
    public int? TargetPostId { get; set; }
    public virtual PostDatabaseEntity? TargetPost { get; set; }

    public int? TargetDiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity? TargetDiscussion { get; set; }

    public int? TargetUserId { get; set; }
    public virtual UserDatabaseEntity? TargetUser { get; set; }

    public int? TargetReportId { get; set; }
    public virtual ReportDatabaseEntity? TargetReport { get; set; }

    public int? TargetUserRoleId { get; set; }
    public virtual UserRoleDatabaseEntity? TargetUserRole { get; set; }

    public int? TargetUserBanId { get; set; }
    public virtual UserBanDatabaseEntity? TargetUserBan { get; set; }

    // Scope where the action was performed
    public int? CommunityId { get; set; }
    public virtual CommunityDatabaseEntity? Community { get; set; }

    public int? HubId { get; set; }
    public virtual HubDatabaseEntity? Hub { get; set; }

    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }

    // Additional context (JSON for flexibility)
    public string? Details { get; set; }

    // Reason/note for the action
    public string? Reason { get; set; }

    public required DateTime CreatedAt { get; set; }
}
