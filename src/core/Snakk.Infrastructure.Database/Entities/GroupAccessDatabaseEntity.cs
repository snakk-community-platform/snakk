namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("GroupAccess")]
public class GroupAccessDatabaseEntity
{
    public int Id { get; set; }

    // Which group this grant belongs to
    public int GroupId { get; set; }
    public virtual GroupDatabaseEntity Group { get; set; } = null!;

    // Permissions
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }

    public DateTime CreatedAt { get; set; }

    // Scope — exactly one FK is set (same pattern as Rule).
    // CommunityId only = community-level grant.
    // HubId only = hub-level grant (applies to hub + all child spaces).
    // SpaceId only = space-level grant.
    public int? CommunityId { get; set; }
    public virtual CommunityDatabaseEntity? Community { get; set; }

    public int? HubId { get; set; }
    public virtual HubDatabaseEntity? Hub { get; set; }

    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }
}
