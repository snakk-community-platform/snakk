namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("JournalEntryPost")]
public class JournalEntryPostDatabaseEntity
{
    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int JournalId { get; set; }
    public virtual DiscussionJournalDatabaseEntity Journal { get; set; } = null!;
}
