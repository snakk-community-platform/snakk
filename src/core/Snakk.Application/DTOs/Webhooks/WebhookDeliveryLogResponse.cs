namespace Snakk.Application.DTOs.Webhooks;

public class WebhookDeliveryLogResponse
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public string WebhookName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int HttpStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; }
    public int AttemptNumber { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}
