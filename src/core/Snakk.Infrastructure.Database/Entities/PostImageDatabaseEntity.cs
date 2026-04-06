namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("PostImage")]
public class PostImageDatabaseEntity
{
    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int ImageId { get; set; }
    public virtual ImageDatabaseEntity Image { get; set; } = null!;
}
