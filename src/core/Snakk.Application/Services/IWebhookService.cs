using Snakk.Application.DTOs.Webhooks;

namespace Snakk.Application.Services;

public interface IWebhookService
{
    /// <summary>
    /// Get all webhooks
    /// </summary>
    Task<List<WebhookResponse>> GetAllWebhooksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific webhook by ID
    /// </summary>
    Task<WebhookResponse?> GetWebhookByIdAsync(Guid webhookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new webhook
    /// </summary>
    Task<WebhookResponse> CreateWebhookAsync(CreateWebhookRequest request, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing webhook
    /// </summary>
    Task<WebhookResponse?> UpdateWebhookAsync(Guid webhookId, UpdateWebhookRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a webhook
    /// </summary>
    Task<bool> DeleteWebhookAsync(Guid webhookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test a webhook by sending a test event
    /// </summary>
    Task<WebhookDeliveryLogResponse> TestWebhookAsync(Guid webhookId, WebhookTestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get delivery logs for a webhook
    /// </summary>
    Task<List<WebhookDeliveryLogResponse>> GetDeliveryLogsAsync(Guid webhookId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available webhook event types
    /// </summary>
    Task<List<WebhookEventInfo>> GetAvailableEventTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger webhooks for a specific event
    /// </summary>
    Task TriggerWebhooksAsync(string eventType, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry failed webhook deliveries
    /// </summary>
    Task RetryFailedDeliveriesAsync(CancellationToken cancellationToken = default);
}
