using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Webhooks;

public class UpdateWebhookRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [Url]
    [MaxLength(2000)]
    public string? Url { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public string[]? EventTypes { get; set; }

    public string? Secret { get; set; }

    public Dictionary<string, string>? CustomHeaders { get; set; }

    [Range(0, 10)]
    public int? MaxRetries { get; set; }

    [Range(5, 300)]
    public int? TimeoutSeconds { get; set; }

    public bool? IsActive { get; set; }
}
