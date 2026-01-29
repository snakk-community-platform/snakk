namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Mention")]
public class MentionDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int MentionedUserId { get; set; }
    public virtual UserDatabaseEntity MentionedUser { get; set; } = null!;

    public required DateTime CreatedAt { get; set; }
}
