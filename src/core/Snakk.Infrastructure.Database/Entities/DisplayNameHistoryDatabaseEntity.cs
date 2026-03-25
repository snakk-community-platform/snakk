namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("DisplayNameHistory")]
public class DisplayNameHistoryDatabaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string PreviousName { get; set; }
    public required string NewName { get; set; }
    public required DateTime ChangedAt { get; set; }
    public int? ChangedByUserId { get; set; }

    // Navigation properties
    public virtual UserDatabaseEntity User { get; set; } = null!;
    public virtual UserDatabaseEntity? ChangedByUser { get; set; }
}
