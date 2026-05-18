namespace Snakk.Infrastructure.EventHandlers.Discord;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class DiscussionCreatedDiscordHandler(
    IDiscordNotificationService discordService,
    SnakkDbContext context) : IDomainEventHandler<DiscussionCreatedEvent>
{
    public async Task HandleAsync(DiscussionCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var data = await context.Discussions
            .Where(d => d.PublicId == @event.DiscussionId.Value)
            .Select(d => new {
                d.Title,
                d.Slug,
                SpacePublicId = d.Space.PublicId,
                SpaceName = d.Space.Name,
                SpaceSlug = d.Space.Slug,
                HubSlug = d.Space.Hub.Slug,
                CommunitySlug = d.Space.Hub.Community.Slug,
                AuthorName = d.CreatedByUser.DisplayName ?? ""
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null) return;

        var url = $"/c/{data.CommunitySlug}/h/{data.HubSlug}/{data.SpaceSlug}/{data.Slug}";
        await discordService.NotifyDiscussionCreatedAsync(
            data.SpacePublicId, data.Title, url, data.AuthorName, data.SpaceName);
    }
}
