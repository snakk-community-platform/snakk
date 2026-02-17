using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Snakk.Infrastructure.Database.Entities;

public class WebhookDatabaseEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// JSON array of event types this webhook subscribes to
    /// e.g., ["UserRegistered", "ReportCreated", "CommunityCreated"]
    /// </summary>
    [Required]
    public string EventTypes { get; set; } = "[]";

    /// <summary>
    /// Optional secret for webhook signature verification
    /// </summary>
    [MaxLength(500)]
    public string? Secret { get; set; }

    /// <summary>
    /// Optional custom headers as JSON
    /// e.g., {"Authorization": "Bearer token", "X-Custom-Header": "value"}
    /// </summary>
    public string? CustomHeaders { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed deliveries
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout in seconds for webhook delivery
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [Required]
    [MaxLength(50)]
    public string CreatedBy { get; set; } = string.Empty;
    public UserDatabaseEntity? CreatedByUser { get; set; }

    // Navigation properties
    public ICollection<WebhookDeliveryLogDatabaseEntity> DeliveryLogs { get; set; } = new List<WebhookDeliveryLogDatabaseEntity>();
}
