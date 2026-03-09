namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("SpaceAllowedDiscussionType")]
public class SpaceAllowedDiscussionTypeDatabaseEntity
{
    public int SpaceId { get; set; }
    public virtual SpaceDatabaseEntity Space { get; set; } = null!;

    public int DiscussionType { get; set; } // Maps to DiscussionTypeEnum
}
