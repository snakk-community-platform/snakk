namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("CommunityRule")]
public class CommunityRuleDatabaseEntity
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Many-to-one: each rule belongs to one community
    public int CommunityId { get; set; }
    public virtual CommunityDatabaseEntity Community { get; set; } = null!;
}
