using System.ComponentModel.DataAnnotations;

namespace Snakk.Application.DTOs.Webhooks;

public class WebhookTestRequest
{
    [Required]
    public string EventType { get; set; } = string.Empty;

    public object? TestPayload { get; set; }
}
