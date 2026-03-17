using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class RulesDto
{
    public List<RuleDto> Rules { get; set; } = new();
}

public class RuleDto
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public int Order { get; set; }
}

public class UpdateRulesRequest
{
    public List<RuleDto> Rules { get; set; } = new();
}
