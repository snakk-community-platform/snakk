namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("CommunityAllowedDiscussionType")]
public class CommunityAllowedDiscussionTypeDatabaseEntity
{
    public int CommunityId { get; set; }
    public virtual CommunityDatabaseEntity Community { get; set; } = null!;

    public int DiscussionType { get; set; } // Maps to DiscussionTypeEnum
}
