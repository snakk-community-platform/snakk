namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("PostMedia")]
public class PostMediaDatabaseEntity
{
    public int PostId { get; set; }
    public virtual PostDatabaseEntity Post { get; set; } = null!;

    public int MediaId { get; set; }
    public virtual MediaDatabaseEntity Media { get; set; } = null!;
}
