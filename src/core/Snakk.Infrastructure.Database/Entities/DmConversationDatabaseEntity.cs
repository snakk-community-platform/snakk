namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DmConversation")]
public class DmConversationDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int InitiatorUserId { get; set; }
    public string? InitiatorUserPublicId { get; set; }
    public virtual UserDatabaseEntity InitiatorUser { get; set; } = null!;

    public int RecipientUserId { get; set; }
    public string? RecipientUserPublicId { get; set; }
    public virtual UserDatabaseEntity RecipientUser { get; set; } = null!;

    public required DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }

    [MaxLength(200)]
    public string? LastMessageExcerpt { get; set; }

    public bool HiddenFromInitiator { get; set; }
    public bool HiddenFromRecipient { get; set; }

    public bool InitiatorPinned { get; set; }
    public bool RecipientPinned { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<DmMessageDatabaseEntity> Messages { get; set; } = [];
    public virtual ICollection<DmReadStateDatabaseEntity> ReadStates { get; set; } = [];
}
