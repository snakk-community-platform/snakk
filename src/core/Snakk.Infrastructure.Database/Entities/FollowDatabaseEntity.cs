namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Follow")]
public class FollowDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }

    public int UserId { get; set; }
    public virtual UserDatabaseEntity User { get; set; } = null!;

    public int TargetTypeId { get; set; }
    public virtual Lookups.FollowTargetTypeLookup TargetType { get; set; } = null!;

    /// <summary>
    /// Notification level: "DiscussionsOnly" or "DiscussionsAndPosts".
    /// Only meaningful for Space follows.
    /// </summary>
    public int LevelId { get; set; }
    public virtual Lookups.FollowLevelLookup Level { get; set; } = null!;

    public int? DiscussionId { get; set; }
    public virtual DiscussionDatabaseEntity? Discussion { get; set; }

    public int? SpaceId { get; set; }
    public virtual SpaceDatabaseEntity? Space { get; set; }

    public int? FollowedUserId { get; set; }
    public virtual UserDatabaseEntity? FollowedUser { get; set; }

    public required DateTime CreatedAt { get; set; }
}
