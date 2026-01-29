namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("DiscussionReadState")]
[PrimaryKey(nameof(UserId), nameof(DiscussionId))]
public class DiscussionReadStateDatabaseEntity
{
    public required string UserId { get; set; }
    public required string DiscussionId { get; set; }
    public string? LastReadPostId { get; set; }
    public required DateTime LastReadAt { get; set; }
}
