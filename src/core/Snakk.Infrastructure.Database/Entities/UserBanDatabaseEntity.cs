namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("UserBan")]
public class UserBanDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    // Ban type: WriteOnly or ReadWrite
    public required string BanType { get; set; }

    // Scope - the entity level where the ban applies
    // Ban inherits down the tree (community ban affects all hubs/spaces/discussions in that community)
    public int? CommunityId { get; set; }
    public virtual CommunityDatabaseEntity? Community { get; set; }

    public int? HubId { get; set; }
    public virtual HubDatabaseEntity? Hub { get; set; }

    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }

    // If all scope fields are null, it's a platform-wide ban (GlobalAdmin only)

    // Reason for ban
    public string? Reason { get; set; }

    // Timing
    public required DateTime BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; } // null = permanent

    // Who issued the ban
    public int BannedByUserId { get; set; }
    public virtual UserDatabaseEntity BannedByUser { get; set; } = null!;

    // Unbanning (soft)
    public DateTime? UnbannedAt { get; set; }
    public int? UnbannedByUserId { get; set; }
    public virtual UserDatabaseEntity? UnbannedByUser { get; set; }
}
