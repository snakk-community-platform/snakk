using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class HubRulesDto
{
    public List<HubRuleDto> Rules { get; set; } = new();
}

public class HubRuleDto
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

public class UpdateHubRulesRequest
{
    public List<HubRuleDto> Rules { get; set; } = new();
}
