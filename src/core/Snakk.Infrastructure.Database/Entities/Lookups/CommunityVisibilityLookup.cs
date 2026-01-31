using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities.Lookups;

[Table("CommunityVisibilityLookup")]
public class CommunityVisibilityLookup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    // Reverse navigation
    public virtual ICollection<CommunityDatabaseEntity> Communities { get; set; } = [];
}
