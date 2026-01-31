namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Notification")]
public class NotificationDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int RecipientUserId { get; set; }
    public virtual UserDatabaseEntity RecipientUser { get; set; } = null!;

    public int TypeId { get; set; }
    public virtual Lookups.NotificationTypeLookup Type { get; set; } = null!;
    public required string Title { get; set; }
    public string? Body { get; set; }

    // Source references (nullable)
    public int? SourcePostId { get; set; }
    public virtual PostDatabaseEntity? SourcePost { get; set; }

    public int? SourceDiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity? SourceDiscussion { get; set; }

    public int? SourceSpaceId { get; set; }
    public virtual SpaceDatabaseEntity? SourceSpace { get; set; }

    public int? ActorUserId { get; set; }
    public virtual UserDatabaseEntity? ActorUser { get; set; }

    public bool IsRead { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
