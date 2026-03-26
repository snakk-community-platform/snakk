namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DiscussionJournal")]
public class DiscussionJournalDatabaseEntity
{
    public int Id { get; set; }

    public int DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity Discussion { get; set; } = null!;

    public virtual ICollection<JournalEntryPostDatabaseEntity> Entries { get; set; } = [];
}
