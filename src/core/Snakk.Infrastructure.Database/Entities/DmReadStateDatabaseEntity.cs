namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("DmReadState")]
[PrimaryKey(nameof(UserId), nameof(ConversationId))]
public class DmReadStateDatabaseEntity
{
    public int UserId { get; set; }
    public int ConversationId { get; set; }
    public required DateTime LastReadAt { get; set; }

    public virtual UserDatabaseEntity User { get; set; } = null!;
    public virtual DmConversationDatabaseEntity Conversation { get; set; } = null!;
}
