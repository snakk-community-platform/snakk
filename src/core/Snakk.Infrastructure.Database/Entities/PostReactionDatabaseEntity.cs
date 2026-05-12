namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Reaction")]
public class PostReactionDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int PostId { get; set; }
    public string? PostPublicId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int UserId { get; set; }
    public string? UserPublicId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int TypeId { get; set; } // Maps to ReactionTypeEnum
    public required DateTime CreatedAt { get; set; }
}
