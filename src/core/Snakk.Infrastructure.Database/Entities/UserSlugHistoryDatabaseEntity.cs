namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("UserSlugHistory")]
public class UserSlugHistoryDatabaseEntity
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required string OldSlug { get; set; }
    public required DateTime SupersededAt { get; set; }

    public virtual UserDatabaseEntity User { get; set; } = null!;
}
