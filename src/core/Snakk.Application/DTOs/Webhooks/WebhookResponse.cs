namespace Snakk.Application.DTOs.Webhooks;

public class WebhookResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] EventTypes { get; set; } = [];
    public bool IsActive { get; set; }
    public int MaxRetries { get; set; }
    public int TimeoutSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? CreatedByDisplayName { get; set; }
    
    // Statistics
    public int TotalDeliveries { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public bool? LastDeliverySuccess { get; set; }
}
