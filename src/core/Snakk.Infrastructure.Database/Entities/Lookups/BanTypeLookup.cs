using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Snakk.Infrastructure.Database.Entities.Lookups;

[Table("BanTypeLookup")]
public class BanTypeLookup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }

    public virtual ICollection<UserBanDatabaseEntity> UserBans { get; set; } = [];
}
