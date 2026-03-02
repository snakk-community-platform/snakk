using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Snakk.Application.DTOs.Webhooks;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class WebhookServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<WebhookService>> _mockLogger;
    private readonly WebhookService _service;

    public WebhookServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"WebhookTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<WebhookService>>();

        _service = new WebhookService(_context, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Create the admin user referenced by webhook CreatedBy FK
        _context.Users.Add(new UserDatabaseEntity
        {
            PublicId = "admin",
            DisplayName = "Admin",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<WebhookDatabaseEntity> CreateWebhook(
        string name = "Test Webhook",
        string url = "https://example.com/webhook",
        string[]? eventTypes = null,
        bool isActive = true,
        string? secret = null)
    {
        var webhook = new WebhookDatabaseEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Url = url,
            EventTypes = JsonSerializer.Serialize(eventTypes ?? new[] { "UserRegistered" }),
            IsActive = isActive,
            MaxRetries = 3,
            TimeoutSeconds = 30,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin",
            Secret = secret
        };
        _context.Webhooks.Add(webhook);
        await _context.SaveChangesAsync();

        return webhook;
    }

    #region CreateWebhookAsync Tests

    [Test]
    public async Task CreateWebhookAsync_CreatesWebhookInDatabase()
    {
        var request = new CreateWebhookRequest
        {
            Name = "New Webhook",
            Url = "https://hooks.example.com/notify",
            EventTypes = ["UserRegistered", "PostCreated"],
            IsActive = true,
            MaxRetries = 5,
            TimeoutSeconds = 15
        };

        var result = await _service.CreateWebhookAsync(request, "admin");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo("New Webhook");
        await Assert.That(result.Url).IsEqualTo("https://hooks.example.com/notify");
        await Assert.That(result.EventTypes).Contains("UserRegistered");
        await Assert.That(result.IsActive).IsTrue();

        var saved = await _context.Webhooks.FirstOrDefaultAsync();
        await Assert.That(saved).IsNotNull();
    }

    [Test]
    public async Task CreateWebhookAsync_WithCustomHeaders_StoresThem()
    {
        var request = new CreateWebhookRequest
        {
            Name = "Header Webhook",
            Url = "https://hooks.example.com",
            EventTypes = ["UserRegistered"],
            CustomHeaders = new Dictionary<string, string> { { "Authorization", "Bearer token123" } },
            IsActive = true
        };

        var result = await _service.CreateWebhookAsync(request, "admin");

        var saved = await _context.Webhooks.FirstOrDefaultAsync(w => w.Id == result.Id);
        await Assert.That(saved!.CustomHeaders).IsNotNull();
        await Assert.That(saved.CustomHeaders!).Contains("Authorization");
    }

    #endregion

    #region GetWebhookByIdAsync Tests

    [Test]
    public async Task GetWebhookByIdAsync_ReturnsWebhook_WhenExists()
    {
        var webhook = await CreateWebhook();

        var result = await _service.GetWebhookByIdAsync(webhook.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(webhook.Id);
    }

    [Test]
    public async Task GetWebhookByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetWebhookByIdAsync(Guid.NewGuid());

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllWebhooksAsync Tests

    [Test]
    public async Task GetAllWebhooksAsync_ReturnsAllWebhooks()
    {
        await CreateWebhook("Webhook 1");
        await CreateWebhook("Webhook 2");

        var result = await _service.GetAllWebhooksAsync();

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetAllWebhooksAsync_OrdersByCreatedAtDescending()
    {
        var older = await CreateWebhook("Older");
        // Simulate time gap
        var newer = new WebhookDatabaseEntity
        {
            Id = Guid.NewGuid(),
            Name = "Newer",
            Url = "https://example.com",
            EventTypes = "[]",
            CreatedAt = DateTime.UtcNow.AddMinutes(5),
            CreatedBy = "admin"
        };
        _context.Webhooks.Add(newer);
        await _context.SaveChangesAsync();

        var result = await _service.GetAllWebhooksAsync();

        await Assert.That(result.First().Name).IsEqualTo("Newer");
    }

    #endregion

    #region UpdateWebhookAsync Tests

    [Test]
    public async Task UpdateWebhookAsync_UpdatesFields()
    {
        var webhook = await CreateWebhook();

        var request = new UpdateWebhookRequest
        {
            Name = "Updated Name",
            Url = "https://updated.example.com",
            IsActive = false
        };

        var result = await _service.UpdateWebhookAsync(webhook.Id, request);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Updated Name");
        await Assert.That(result.Url).IsEqualTo("https://updated.example.com");
        await Assert.That(result.IsActive).IsFalse();
    }

    [Test]
    public async Task UpdateWebhookAsync_NonexistentWebhook_ReturnsNull()
    {
        var result = await _service.UpdateWebhookAsync(Guid.NewGuid(), new UpdateWebhookRequest { Name = "x" });

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task UpdateWebhookAsync_NullFields_DoNotOverwrite()
    {
        var webhook = await CreateWebhook("Original Name", "https://original.com");

        var request = new UpdateWebhookRequest
        {
            Name = "Updated Name"
            // Url is null, so it should not be overwritten
        };

        var result = await _service.UpdateWebhookAsync(webhook.Id, request);

        await Assert.That(result!.Name).IsEqualTo("Updated Name");
        await Assert.That(result.Url).IsEqualTo("https://original.com");
    }

    #endregion

    #region DeleteWebhookAsync Tests

    [Test]
    public async Task DeleteWebhookAsync_DeletesWebhook_ReturnsTrue()
    {
        var webhook = await CreateWebhook();

        var result = await _service.DeleteWebhookAsync(webhook.Id);

        await Assert.That(result).IsTrue();

        var found = await _context.Webhooks.FirstOrDefaultAsync(w => w.Id == webhook.Id);
        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task DeleteWebhookAsync_NonexistentWebhook_ReturnsFalse()
    {
        var result = await _service.DeleteWebhookAsync(Guid.NewGuid());

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetAvailableEventTypesAsync Tests

    [Test]
    public async Task GetAvailableEventTypesAsync_ReturnsEvents()
    {
        var events = await _service.GetAvailableEventTypesAsync();

        await Assert.That(events.Count).IsGreaterThan(0);

        // Should have events from all categories
        var categories = events
            .Select(e => e.Category)
            .Distinct()
            .ToList();
        await Assert.That(categories).Contains("User");
        await Assert.That(categories).Contains("Content");
        await Assert.That(categories).Contains("Moderation");
        await Assert.That(categories).Contains("System");
    }

    #endregion

    #region GetDeliveryLogsAsync Tests

    [Test]
    public async Task GetDeliveryLogsAsync_ReturnsLogs_ForWebhook()
    {
        var webhook = await CreateWebhook();
        _context.WebhookDeliveryLogs.Add(new WebhookDeliveryLogDatabaseEntity
        {
            Id = Guid.NewGuid(),
            WebhookId = webhook.Id,
            EventType = "UserRegistered",
            Payload = "{}",
            Url = webhook.Url,
            IsSuccess = true,
            AttemptNumber = 1,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var logs = await _service.GetDeliveryLogsAsync(webhook.Id);

        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs.First().EventType).IsEqualTo("UserRegistered");
    }

    [Test]
    public async Task GetDeliveryLogsAsync_NonexistentWebhook_ReturnsEmpty()
    {
        var logs = await _service.GetDeliveryLogsAsync(Guid.NewGuid());

        await Assert.That(logs.Count).IsEqualTo(0);
    }

    #endregion

    #region TriggerWebhooksAsync Tests

    [Test]
    public async Task TriggerWebhooksAsync_NoMatchingWebhooks_CompletesWithoutError()
    {
        await CreateWebhook(eventTypes: new[] { "UserRegistered" });

        var act = async () => await _service.TriggerWebhooksAsync("PostCreated", new { data = "test" });

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task TriggerWebhooksAsync_InactiveWebhook_NotTriggered()
    {
        await CreateWebhook(eventTypes: new[] { "UserRegistered" }, isActive: false);

        // This should not attempt delivery since webhook is inactive
        var act = async () => await _service.TriggerWebhooksAsync("UserRegistered", new { data = "test" });

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
