using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class SpaceRulesDto
{
    public List<SpaceRuleDto> Rules { get; set; } = new();
}

public class SpaceRuleDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public int Order { get; set; }
}

public class UpdateSpaceRulesRequest
{
    public List<SpaceRuleDto> Rules { get; set; } = new();
}
