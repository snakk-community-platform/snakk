namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Groups")]
public class GroupDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public int CommunityId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual CommunityDatabaseEntity Community { get; set; } = null!;
    public virtual ICollection<GroupMemberDatabaseEntity> Members { get; set; } = [];
}
