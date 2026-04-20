namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Save")]
public class UserSaveDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int? DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity? Discussion { get; set; }

    public int? PostId { get; set; }
    public virtual PostDatabaseEntity? Post { get; set; }

    public required DateTime CreatedAt { get; set; }
}
