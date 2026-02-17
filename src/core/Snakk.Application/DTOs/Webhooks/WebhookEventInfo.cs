namespace Snakk.Application.DTOs.Webhooks;

/// <summary>
/// Information about an available webhook event type
/// </summary>
public class WebhookEventInfo
{
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
