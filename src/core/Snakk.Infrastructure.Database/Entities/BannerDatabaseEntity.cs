namespace Snakk.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

[Table("Banner")]
public class BannerDatabaseEntity
{
    public int Id { get; set; }
    public required string PublicId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string RenderedContent { get; set; } = "";
    public int TypeId { get; set; } // Maps to BannerTypeEnum
    public int ScopeId { get; set; } // Maps to BannerScopeEnum
    public required string ScopeEntityId { get; set; } // PublicId of community/hub/space
    public DateTime? VisibleFrom { get; set; }
    public DateTime? VisibleUntil { get; set; }
    public bool IsDismissible { get; set; } = true;
    public int SortOrder { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    // Many-to-one relationships
    public int CreatedByUserId { get; set; }
    public string? CreatedByUserPublicId { get; set; }
    public virtual UserDatabaseEntity CreatedByUser { get; set; } = null!;
}
