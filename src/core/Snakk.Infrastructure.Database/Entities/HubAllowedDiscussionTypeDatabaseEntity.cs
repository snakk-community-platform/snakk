namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("HubAllowedDiscussionType")]
public class HubAllowedDiscussionTypeDatabaseEntity
{
    public int HubId { get; set; }
    public virtual HubDatabaseEntity Hub { get; set; } = null!;

    public int DiscussionType { get; set; } // Maps to DiscussionTypeEnum
}
