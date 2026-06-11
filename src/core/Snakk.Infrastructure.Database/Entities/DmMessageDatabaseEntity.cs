namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DmMessage")]
public class DmMessageDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int ConversationId { get; set; }
    public string? ConversationPublicId { get; set; }
    public virtual DmConversationDatabaseEntity Conversation { get; set; } = null!;

    public int SenderUserId { get; set; }
    public string? SenderUserPublicId { get; set; }
    public virtual UserDatabaseEntity Sender { get; set; } = null!;

    public required string Content { get; set; }

    public required DateTime CreatedAt { get; set; }

    public bool HiddenFromSender { get; set; }
    public bool HiddenFromRecipient { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
