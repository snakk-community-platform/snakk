using System;
using System.ComponentModel.DataAnnotations;

namespace Snakk.Infrastructure.Database.Entities;

public class WebhookDeliveryLogDatabaseEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WebhookId { get; set; }
    public WebhookDatabaseEntity? Webhook { get; set; }

    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The JSON payload that was sent
    /// </summary>
    [Required]
    public string Payload { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    public int HttpStatusCode { get; set; }

    /// <summary>
    /// Response body from the webhook endpoint
    /// </summary>
    [MaxLength(5000)]
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Error message if delivery failed
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public bool IsSuccess { get; set; }

    /// <summary>
    /// Number of delivery attempts made
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Time taken for the request in milliseconds
    /// </summary>
    public int? DurationMs { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Next retry time if this attempt failed
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
}
