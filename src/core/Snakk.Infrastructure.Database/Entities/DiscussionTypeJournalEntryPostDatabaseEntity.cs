namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeJournalEntryPost")]
public class DiscussionTypeJournalEntryPostDatabaseEntity
{
    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int JournalId { get; set; }
    public virtual DiscussionTypeJournalDatabaseEntity Journal { get; set; } = null!;
}
