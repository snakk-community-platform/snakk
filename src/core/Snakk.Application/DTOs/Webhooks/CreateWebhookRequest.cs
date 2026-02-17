using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Webhooks;

public class CreateWebhookRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Url]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one event type must be selected")]
    public string[] EventTypes { get; set; } = Array.Empty<string>();

    public string? Secret { get; set; }

    public Dictionary<string, string>? CustomHeaders { get; set; }

    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsActive { get; set; } = true;
}
