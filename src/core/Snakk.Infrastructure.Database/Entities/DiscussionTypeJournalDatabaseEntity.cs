namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionTypeJournal")]
public class DiscussionTypeJournalDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public virtual ICollection<DiscussionTypeJournalEntryPostDatabaseEntity> Entries { get; set; } = [];
}
