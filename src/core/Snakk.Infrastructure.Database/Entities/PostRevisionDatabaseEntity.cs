namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("PostRevision")]
public class PostRevisionDatabaseEntity
{
    [Key]
    public int Id { get; set; }

    public int PostId { get; set; }
    public required string PostPublicId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EditedByUserId { get; set; }
    public required string EditedByUserPublicId { get; set; }
    public int RevisionNumber { get; set; }

    // Navigation properties
    public virtual PostDatabaseEntity Post { get; set; } = null!;
    public virtual UserDatabaseEntity EditedByUser { get; set; } = null!;
}
