namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("SpaceRule")]
public class SpaceRuleDatabaseEntity
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Many-to-one: each rule belongs to one space
    public int SpaceId { get; set; }
    public virtual SpaceDatabaseEntity Space { get; set; } = null!;
}
