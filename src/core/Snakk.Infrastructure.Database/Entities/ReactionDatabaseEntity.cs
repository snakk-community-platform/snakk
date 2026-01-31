namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Reaction")]
public class ReactionDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int TypeId { get; set; }
    public virtual Lookups.ReactionTypeLookup Type { get; set; } = null!;
    public required DateTime CreatedAt { get; set; }
}
