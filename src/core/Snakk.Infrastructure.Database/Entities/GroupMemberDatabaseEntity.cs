namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("GroupMembers")]
public class GroupMemberDatabaseEntity
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public int AddedByUserId { get; set; }
    public DateTime AddedAt { get; set; }

    // Navigation properties
    public virtual GroupDatabaseEntity Group { get; set; } = null!;
    public virtual UserDatabaseEntity User { get; set; } = null!;
    public virtual UserDatabaseEntity AddedByUser { get; set; } = null!;
}
