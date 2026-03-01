using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Management;

public class CommunityRulesDto
{
    public List<CommunityRuleDto> Rules { get; set; } = new();
}

public class CommunityRuleDto
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

public class UpdateCommunityRulesRequest
{
    public List<CommunityRuleDto> Rules { get; set; } = new();
}
