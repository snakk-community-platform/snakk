using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Webhooks;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Entities.Lookups;

namespace Snakk.Infrastructure.Services;

public class WebhookService(
    SnakkDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookService> logger) : IWebhookService
{
    public async Task<List<WebhookResponse>> GetAllWebhooksAsync(CancellationToken cancellationToken = default)
    {
        var raw = await dbContext.Webhooks
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new {
                w.Id, w.Name, w.Url, w.Description, w.EventTypes,
                w.IsActive, w.MaxRetries, w.TimeoutSeconds,
                w.CreatedAt, w.UpdatedAt, w.CreatedBy,
                CreatedByDisplayName = w.CreatedByUser != null ? w.CreatedByUser.DisplayName : null,
                TotalDeliveries = w.DeliveryLogs.Count(),
                SuccessfulDeliveries = w.DeliveryLogs.Count(l => l.IsSuccess),
                FailedDeliveries = w.DeliveryLogs.Count(l => !l.IsSuccess),
                LastDeliveryAt = (DateTime?)w.DeliveryLogs.Max(l => (DateTime?)l.CreatedAt),
                LastDeliverySuccess = (bool?)w.DeliveryLogs
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => (bool?)l.IsSuccess)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return raw.Select(w => new WebhookResponse
        {
            Id = w.Id,
            Name = w.Name,
            Url = w.Url,
            Description = w.Description,
            EventTypes = JsonSerializer.Deserialize<string[]>(w.EventTypes) ?? [],
            IsActive = w.IsActive,
            MaxRetries = w.MaxRetries,
            TimeoutSeconds = w.TimeoutSeconds,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt,
            CreatedBy = w.CreatedBy,
            CreatedByDisplayName = w.CreatedByDisplayName,
            TotalDeliveries = w.TotalDeliveries,
            SuccessfulDeliveries = w.SuccessfulDeliveries,
            FailedDeliveries = w.FailedDeliveries,
            LastDeliveryAt = w.LastDeliveryAt,
            LastDeliverySuccess = w.LastDeliverySuccess
        }).ToList();
    }

    public async Task<WebhookResponse?> GetWebhookByIdAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default)
    {
        var w = await dbContext.Webhooks
            .AsNoTracking()
            .Where(wh => wh.Id == webhookId)
            .Select(wh => new {
                wh.Id, wh.Name, wh.Url, wh.Description, wh.EventTypes,
                wh.IsActive, wh.MaxRetries, wh.TimeoutSeconds,
                wh.CreatedAt, wh.UpdatedAt, wh.CreatedBy,
                CreatedByDisplayName = wh.CreatedByUser != null ? wh.CreatedByUser.DisplayName : null,
                TotalDeliveries = wh.DeliveryLogs.Count(),
                SuccessfulDeliveries = wh.DeliveryLogs.Count(l => l.IsSuccess),
                FailedDeliveries = wh.DeliveryLogs.Count(l => !l.IsSuccess),
                LastDeliveryAt = (DateTime?)wh.DeliveryLogs.Max(l => (DateTime?)l.CreatedAt),
                LastDeliverySuccess = (bool?)wh.DeliveryLogs
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => (bool?)l.IsSuccess)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (w is null) return null;

        return new WebhookResponse
        {
            Id = w.Id,
            Name = w.Name,
            Url = w.Url,
            Description = w.Description,
            EventTypes = JsonSerializer.Deserialize<string[]>(w.EventTypes) ?? [],
            IsActive = w.IsActive,
            MaxRetries = w.MaxRetries,
            TimeoutSeconds = w.TimeoutSeconds,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt,
            CreatedBy = w.CreatedBy,
            CreatedByDisplayName = w.CreatedByDisplayName,
            TotalDeliveries = w.TotalDeliveries,
            SuccessfulDeliveries = w.SuccessfulDeliveries,
            FailedDeliveries = w.FailedDeliveries,
            LastDeliveryAt = w.LastDeliveryAt,
            LastDeliverySuccess = w.LastDeliverySuccess
        };
    }

    public async Task<WebhookResponse> CreateWebhookAsync(
        CreateWebhookRequest request,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ValidateWebhookUrl(request.Url);

        var webhook = new WebhookDatabaseEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Url = request.Url,
            Description = request.Description,
            EventTypes = JsonSerializer.Serialize(request.EventTypes),
            Secret = request.Secret,
            CustomHeaders = request.CustomHeaders is not null
                ? JsonSerializer.Serialize(request.CustomHeaders)
                : null,
            IsActive = request.IsActive,
            MaxRetries = request.MaxRetries,
            TimeoutSeconds = request.TimeoutSeconds,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        dbContext.Webhooks.Add(webhook);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Webhook created: {WebhookId} - {WebhookName}", webhook.Id, webhook.Name);

        return await GetWebhookByIdAsync(webhook.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created webhook");
    }

    public async Task<WebhookResponse?> UpdateWebhookAsync(
        Guid webhookId,
        UpdateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var webhook = await dbContext.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId, cancellationToken);

        if (webhook is null)
            return null;

        if (request.Name is not null)
            webhook.Name = request.Name;

        if (request.Url is not null)
        {
            ValidateWebhookUrl(request.Url);
            webhook.Url = request.Url;
        }

        if (request.Description is not null)
            webhook.Description = request.Description;

        if (request.EventTypes is not null)
            webhook.EventTypes = JsonSerializer.Serialize(request.EventTypes);

        if (request.Secret is not null)
            webhook.Secret = request.Secret;

        if (request.CustomHeaders is not null)
            webhook.CustomHeaders = JsonSerializer.Serialize(request.CustomHeaders);

        if (request.IsActive.HasValue)
            webhook.IsActive = request.IsActive.Value;

        if (request.MaxRetries.HasValue)
            webhook.MaxRetries = request.MaxRetries.Value;

        if (request.TimeoutSeconds.HasValue)
            webhook.TimeoutSeconds = request.TimeoutSeconds.Value;

        webhook.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Webhook updated: {WebhookId}", webhookId);

        return await GetWebhookByIdAsync(webhookId, cancellationToken);
    }

    public async Task<bool> DeleteWebhookAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default)
    {
        var webhook = await dbContext.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId, cancellationToken);

        if (webhook is null)
            return false;

        dbContext.Webhooks.Remove(webhook);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Webhook deleted: {WebhookId}", webhookId);

        return true;
    }

    public async Task<WebhookDeliveryLogResponse> TestWebhookAsync(
        Guid webhookId,
        WebhookTestRequest request,
        CancellationToken cancellationToken = default)
    {
        var webhook = await dbContext.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId, cancellationToken);

        if (webhook is null)
            throw new InvalidOperationException("Webhook not found");

        var testPayload = request.TestPayload ?? new
        {
            eventType = request.EventType,
            timestamp = DateTime.UtcNow,
            data = new { message = "This is a test webhook delivery" }
        };

        var deliveryLog = await DeliverWebhookAsync(
            webhook,
            request.EventType,
            testPayload,
            isTest: true,
            attemptNumber: 1,
            cancellationToken);

        return MapToDeliveryLogResponse(deliveryLog, webhook.Name);
    }

    public async Task<List<WebhookDeliveryLogResponse>> GetDeliveryLogsAsync(
        Guid webhookId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var webhook = await dbContext.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId, cancellationToken);

        if (webhook is null)
            return [];

        var logs = await dbContext.WebhookDeliveryLogs
            .Where(wdl => wdl.WebhookId == webhookId)
            .OrderByDescending(wdl => wdl.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return logs
            .Select(l => MapToDeliveryLogResponse(l, webhook.Name))
            .ToList();
    }

    public Task<List<WebhookEventInfo>> GetAvailableEventTypesAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<WebhookEventInfo>();

        // User Events
        foreach (var eventType in WebhookEventTypeLookup.Categories.User)
        {
            events.Add(new WebhookEventInfo
            {
                EventType = eventType,
                Category = "User",
                Description = GetEventDescription(eventType)
            });
        }

        // Content Events
        foreach (var eventType in WebhookEventTypeLookup.Categories.Content)
        {
            events.Add(new WebhookEventInfo
            {
                EventType = eventType,
                Category = "Content",
                Description = GetEventDescription(eventType)
            });
        }

        // Moderation Events
        foreach (var eventType in WebhookEventTypeLookup.Categories.Moderation)
        {
            events.Add(new WebhookEventInfo
            {
                EventType = eventType,
                Category = "Moderation",
                Description = GetEventDescription(eventType)
            });
        }

        // System Events
        foreach (var eventType in WebhookEventTypeLookup.Categories.System)
        {
            events.Add(new WebhookEventInfo
            {
                EventType = eventType,
                Category = "System",
                Description = GetEventDescription(eventType)
            });
        }

        return Task.FromResult(events);
    }

    public async Task TriggerWebhooksAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var activeWebhooks = await dbContext.Webhooks
            .Where(w => w.IsActive)
            .ToListAsync(cancellationToken);

        var matchingWebhooks = activeWebhooks
            .Where(w =>
            {
                var eventTypes = JsonSerializer.Deserialize<string[]>(w.EventTypes) ?? [];
                return eventTypes.Contains(eventType);
            })
            .ToList();

        if (matchingWebhooks.Count == 0)
        {
            logger.LogDebug("No active webhooks subscribed to event type: {EventType}", eventType);
            return;
        }

        logger.LogInformation("Triggering {Count} webhooks for event: {EventType}", matchingWebhooks.Count, eventType);

        // Execute HTTP deliveries in parallel (DbContext is NOT thread-safe, so don't save inside parallel tasks)
        var deliveryLogs = await Task.WhenAll(
            matchingWebhooks.Select(webhook =>
                ExecuteDeliveryAsync(webhook, eventType, payload, isTest: false, attemptNumber: 1, cancellationToken)));

        // Batch save all delivery logs in a single DbContext operation
        dbContext.WebhookDeliveryLogs.AddRange(deliveryLogs);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryFailedDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var failedDeliveries = await dbContext.WebhookDeliveryLogs
            .Include(wdl => wdl.Webhook)
            .Where(wdl =>
                !wdl.IsSuccess
                && wdl.NextRetryAt != null
                && wdl.NextRetryAt <= now)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Retrying {Count} failed webhook deliveries", failedDeliveries.Count);

        foreach (var failedDelivery in failedDeliveries)
        {
            if (failedDelivery.Webhook is null || !failedDelivery.Webhook.IsActive)
                continue;

            if (failedDelivery.AttemptNumber >= failedDelivery.Webhook.MaxRetries)
            {
                logger.LogWarning(
                    "Webhook delivery {DeliveryId} exceeded max retries ({MaxRetries})",
                    failedDelivery.Id,
                    failedDelivery.Webhook.MaxRetries);

                // Clear NextRetryAt to prevent future retries
                failedDelivery.NextRetryAt = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<object>(failedDelivery.Payload);

                await DeliverWebhookAsync(
                    failedDelivery.Webhook,
                    failedDelivery.EventType,
                    payload!,
                    isTest: false,
                    failedDelivery.AttemptNumber + 1,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retry webhook delivery {DeliveryId}", failedDelivery.Id);
            }
        }
    }

    /// <summary>
    /// Delivers a webhook and persists the delivery log. Use for single-webhook operations
    /// (test, retry). For parallel delivery, use ExecuteDeliveryAsync + batch save.
    /// </summary>
    private async Task<WebhookDeliveryLogDatabaseEntity> DeliverWebhookAsync(
        WebhookDatabaseEntity webhook,
        string eventType,
        object payload,
        bool isTest,
        int attemptNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var deliveryLog = await ExecuteDeliveryAsync(
            webhook,
            eventType,
            payload,
            isTest,
            attemptNumber,
            cancellationToken);

        dbContext.WebhookDeliveryLogs.Add(deliveryLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return deliveryLog;
    }

    /// <summary>
    /// Executes the HTTP delivery and returns the log entity WITHOUT saving to the database.
    /// Safe to call in parallel since it doesn't touch DbContext.
    /// </summary>
    private async Task<WebhookDeliveryLogDatabaseEntity> ExecuteDeliveryAsync(
        WebhookDatabaseEntity webhook,
        string eventType,
        object payload,
        bool isTest,
        int attemptNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var deliveryLog = new WebhookDeliveryLogDatabaseEntity
        {
            Id = Guid.NewGuid(),
            WebhookId = webhook.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            Url = webhook.Url,
            AttemptNumber = attemptNumber,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            // Re-resolve DNS at delivery time to prevent DNS rebinding attacks.
            // An attacker could register a webhook pointing to a public IP, then
            // flip DNS to 127.0.0.1 after validation. The ConnectCallback fires
            // at actual TCP connect time, re-checking the resolved address.
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (ctx, ct) =>
                {
                    var addresses = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
                    var safeAddress = addresses.FirstOrDefault(a => !IsPrivateOrReservedIp(a))
                        ?? throw new InvalidOperationException(
                            "Webhook URL resolves to a private or reserved IP address.");
                    var socket = new Socket(safeAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                        { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(safeAddress, ctx.DnsEndPoint.Port), ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };
            using var httpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(webhook.TimeoutSeconds)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url);

            // Add custom headers (block dangerous headers that could enable request smuggling or SSRF)
            if (!string.IsNullOrEmpty(webhook.CustomHeaders))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(webhook.CustomHeaders);

                if (headers is not null)
                {
                    foreach (var (key, value) in headers)
                    {
                        if (IsBlockedHeader(key))
                            continue;

                        if (key.Length > 256 || value.Length > 4096)
                            continue;

                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }

            // Add webhook signature if secret is configured
            if (!string.IsNullOrEmpty(webhook.Secret))
            {
                var signature = GenerateSignature(deliveryLog.Payload, webhook.Secret);
                request.Headers.Add("X-Webhook-Signature", signature);
            }

            // Add metadata headers
            request.Headers.Add("X-Webhook-Event", eventType);
            request.Headers.Add("X-Webhook-Delivery", deliveryLog.Id.ToString());
            request.Headers.Add("X-Webhook-Attempt", attemptNumber.ToString());

            if (isTest)
                request.Headers.Add("X-Webhook-Test", "true");

            request.Content = JsonContent.Create(payload);

            var response = await httpClient.SendAsync(request, cancellationToken);

            stopwatch.Stop();

            deliveryLog.HttpStatusCode = (int)response.StatusCode;
            deliveryLog.IsSuccess = response.IsSuccessStatusCode;
            deliveryLog.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                deliveryLog.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                deliveryLog.ErrorMessage = $"HTTP {deliveryLog.HttpStatusCode}: {response.ReasonPhrase}";

                // Schedule retry if within retry limits
                if (attemptNumber < webhook.MaxRetries)
                    deliveryLog.NextRetryAt = CalculateNextRetryTime(attemptNumber);

                logger.LogWarning(
                    "Webhook delivery failed: {WebhookId} - {EventType} - HTTP {StatusCode} (Attempt {Attempt}/{MaxRetries})",
                    webhook.Id,
                    eventType,
                    deliveryLog.HttpStatusCode,
                    attemptNumber,
                    webhook.MaxRetries);
            }
            else
            {
                deliveryLog.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (deliveryLog.ResponseBody.Length > 5000)
                    deliveryLog.ResponseBody = deliveryLog.ResponseBody[..5000];

                logger.LogInformation(
                    "Webhook delivered successfully: {WebhookId} - {EventType} in {Duration}ms",
                    webhook.Id,
                    eventType,
                    deliveryLog.DurationMs);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            deliveryLog.IsSuccess = false;
            deliveryLog.ErrorMessage = ex.Message;
            deliveryLog.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            // Schedule retry if within retry limits
            if (attemptNumber < webhook.MaxRetries)
                deliveryLog.NextRetryAt = CalculateNextRetryTime(attemptNumber);

            logger.LogError(
                ex,
                "Webhook delivery exception: {WebhookId} - {EventType} (Attempt {Attempt}/{MaxRetries})",
                webhook.Id,
                eventType,
                attemptNumber,
                webhook.MaxRetries);
        }

        return deliveryLog;
    }

    private static DateTime CalculateNextRetryTime(int attemptNumber)
    {
        // Exponential backoff: 1min, 5min, 15min, 30min, 1hr, 2hr
        var delayMinutes = attemptNumber switch
        {
            1 => 1,
            2 => 5,
            3 => 15,
            4 => 30,
            5 => 60,
            _ => 120
        };

        return DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    private static string GenerateSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string GetEventDescription(string eventType) =>
        eventType switch
        {
            WebhookEventTypeLookup.UserRegistered => "Triggered when a new user registers",
            WebhookEventTypeLookup.UserBanned => "Triggered when a user is banned",
            WebhookEventTypeLookup.UserUnbanned => "Triggered when a user is unbanned",
            WebhookEventTypeLookup.UserDeleted => "Triggered when a user account is deleted",
            WebhookEventTypeLookup.UserRoleChanged => "Triggered when a user's role is changed",
            WebhookEventTypeLookup.UserProfileUpdated => "Triggered when a user updates their profile",

            WebhookEventTypeLookup.CommunityCreated => "Triggered when a new community is created",
            WebhookEventTypeLookup.CommunityUpdated => "Triggered when a community is updated",
            WebhookEventTypeLookup.CommunityDeleted => "Triggered when a community is deleted",
            WebhookEventTypeLookup.DiscussionCreated => "Triggered when a new discussion is created",
            WebhookEventTypeLookup.DiscussionUpdated => "Triggered when a discussion is updated",
            WebhookEventTypeLookup.DiscussionDeleted => "Triggered when a discussion is deleted",
            WebhookEventTypeLookup.PostCreated => "Triggered when a new post is created",
            WebhookEventTypeLookup.PostUpdated => "Triggered when a post is updated",
            WebhookEventTypeLookup.PostDeleted => "Triggered when a post is deleted",
            WebhookEventTypeLookup.ReactionAdded => "Triggered when a reaction is added to a post",
            WebhookEventTypeLookup.ReactionRemoved => "Triggered when a reaction is removed from a post",

            WebhookEventTypeLookup.ReportCreated => "Triggered when a new report is filed",
            WebhookEventTypeLookup.ReportResolved => "Triggered when a report is resolved",
            WebhookEventTypeLookup.ReportDismissed => "Triggered when a report is dismissed",
            WebhookEventTypeLookup.ContentFlagged => "Triggered when content is flagged by auto-moderation",
            WebhookEventTypeLookup.ModerationActionTaken => "Triggered when a moderation action is taken",
            WebhookEventTypeLookup.AutoModTriggered => "Triggered when auto-moderation rules are triggered",

            WebhookEventTypeLookup.SystemSettingChanged => "Triggered when a system setting is changed",
            WebhookEventTypeLookup.PerformanceAlert => "Triggered when a performance issue is detected",
            WebhookEventTypeLookup.SecurityIncident => "Triggered when a security incident is detected",
            WebhookEventTypeLookup.BackupCompleted => "Triggered when a backup operation completes",

            _ => "Unknown event type"
        };

    /// <summary>
    /// Validates that a webhook URL is safe to call — blocks private/internal networks (SSRF prevention).
    /// </summary>
    private static void ValidateWebhookUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Webhook URL is required.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Webhook URL must be a valid absolute URL.");

        if (uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("Webhook URL must use https or http scheme.");

        var host = uri.Host;

        // Block localhost variants
        if (host is "localhost" || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Webhook URL must not target localhost.");

        // Resolve and check IP addresses
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsPrivateOrReservedIp(ip))
                throw new ArgumentException("Webhook URL must not target private or reserved IP addresses.");
        }
        else
        {
            // DNS resolution — check all resolved addresses
            try
            {
                var addresses = Dns.GetHostAddresses(host);
                if (addresses.Length == 0)
                    throw new ArgumentException("Webhook URL hostname could not be resolved.");

                if (addresses.All(IsPrivateOrReservedIp))
                    throw new ArgumentException("Webhook URL must not target private or reserved IP addresses.");
            }
            catch (SocketException)
            {
                throw new ArgumentException("Webhook URL hostname could not be resolved.");
            }
        }
    }

    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Authorization", "Cookie", "Set-Cookie",
        "Content-Length", "Transfer-Encoding", "Connection", "Upgrade",
        "TE", "Trailer", "Proxy-Authorization", "Proxy-Connection",
        "Keep-Alive", "WWW-Authenticate", "Origin", "Referer",
        "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto"
    };

    private static bool IsBlockedHeader(string headerName) =>
        BlockedHeaders.Contains(headerName);

    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        // Map IPv6-mapped IPv4 to IPv4 for consistent checks
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                10 => true,                                           // 10.0.0.0/8
                127 => true,                                          // 127.0.0.0/8 (loopback)
                169 when bytes[1] == 254 => true,                     // 169.254.0.0/16 (link-local)
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,    // 172.16.0.0/12
                192 when bytes[1] == 168 => true,                     // 192.168.0.0/16
                0 => true,                                            // 0.0.0.0/8
                _ => false
            };
        }

        // IPv6: block loopback (::1) and link-local (fe80::/10)
        return IPAddress.IsLoopback(ip)
            || ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal;
    }

    private static WebhookDeliveryLogResponse MapToDeliveryLogResponse(
        WebhookDeliveryLogDatabaseEntity log,
        string webhookName) => new WebhookDeliveryLogResponse
    {
        Id = log.Id,
        WebhookId = log.WebhookId,
        WebhookName = webhookName,
        EventType = log.EventType,
        Url = log.Url,
        HttpStatusCode = log.HttpStatusCode,
        ResponseBody = log.ResponseBody,
        ErrorMessage = log.ErrorMessage,
        IsSuccess = log.IsSuccess,
        AttemptNumber = log.AttemptNumber,
        DurationMs = log.DurationMs,
        CreatedAt = log.CreatedAt,
        NextRetryAt = log.NextRetryAt
    };
}
