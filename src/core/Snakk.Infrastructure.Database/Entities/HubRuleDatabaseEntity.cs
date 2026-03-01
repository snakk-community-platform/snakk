namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("HubRule")]
public class HubRuleDatabaseEntity
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Many-to-one: each rule belongs to one hub
    public int HubId { get; set; }
    public virtual HubDatabaseEntity Hub { get; set; } = null!;
}
