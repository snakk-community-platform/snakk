namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("UserRole")]
public class UserRoleDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    // Role type: GlobalAdmin, CommunityAdmin, CommunityMod, HubMod, SpaceMod
    public required string Role { get; set; }

    // Scope - only one of these is set based on role type
    // GlobalAdmin: all null
    // CommunityAdmin/CommunityMod: CommunityId set
    // HubMod: HubId set
    // SpaceMod: SpaceId set
    public int? CommunityId { get; set; }
    public virtual CommunityDatabaseEntity? Community { get; set; }

    public int? HubId { get; set; }
    public virtual HubDatabaseEntity? Hub { get; set; }

    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }

    // Assignment tracking
    public int AssignedByUserId { get; set; }
    public virtual UserDatabaseEntity AssignedByUser { get; set; } = null!;

    public required DateTime AssignedAt { get; set; }

    // Soft revocation for audit trail
    public DateTime? RevokedAt { get; set; }
    public int? RevokedByUserId { get; set; }
    public virtual UserDatabaseEntity? RevokedByUser { get; set; }
}
