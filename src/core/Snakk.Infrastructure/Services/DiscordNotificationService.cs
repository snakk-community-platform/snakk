namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using System.Net.Http.Json;

public class DiscordNotificationService(
    SnakkDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<DiscordNotificationService> logger) : IDiscordNotificationService
{
    public async Task NotifyDiscussionCreatedAsync(string spacePublicId,
        string discussionTitle, string discussionUrl, string authorDisplayName,
        string spaceName, CancellationToken ct = default)
    {
        var webhookUrl = await GetWebhookUrlAsync(spacePublicId, ct);
        if (webhookUrl is null) return;

        var content = $"**New discussion in {spaceName}**\n> **{discussionTitle}**\nby {authorDisplayName}\n{discussionUrl}";
        await SendAsync(webhookUrl, content, ct);
    }

    public async Task NotifyPostCreatedAsync(string spacePublicId,
        string discussionTitle, string discussionUrl, string authorDisplayName,
        string spaceName, CancellationToken ct = default)
    {
        var webhookUrl = await GetWebhookUrlAsync(spacePublicId, ct);
        if (webhookUrl is null) return;

        var content = $"**New reply in {spaceName}**\n> {discussionTitle}\nby {authorDisplayName}\n{discussionUrl}";
        await SendAsync(webhookUrl, content, ct);
    }

    private async Task<string?> GetWebhookUrlAsync(string spacePublicId, CancellationToken ct)
        => await dbContext.Spaces
            .Where(s => s.PublicId == spacePublicId && s.DiscordWebhookUrl != null)
            .Select(s => s.DiscordWebhookUrl)
            .FirstOrDefaultAsync(ct);

    private async Task SendAsync(string webhookUrl, string content, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("DiscordWebhook");
            var response = await client.PostAsJsonAsync(webhookUrl, new { content }, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Discord webhook {Url} returned {Status}",
                    webhookUrl, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord webhook notification");
        }
    }
}
