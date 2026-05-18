namespace Snakk.Infrastructure.EventHandlers.Discord;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class PostCreatedDiscordHandler(
    IDiscordNotificationService discordService,
    SnakkDbContext context) : IDomainEventHandler<PostCreatedEvent>
{
    public async Task HandleAsync(PostCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var data = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => new {
                SpacePublicId = p.Discussion.Space.PublicId,
                SpaceName = p.Discussion.Space.Name,
                SpaceSlug = p.Discussion.Space.Slug,
                HubSlug = p.Discussion.Space.Hub.Slug,
                CommunitySlug = p.Discussion.Space.Hub.Community.Slug,
                DiscussionSlug = p.Discussion.Slug,
                DiscussionTitle = p.Discussion.Title,
                AuthorName = p.CreatedByUser.DisplayName ?? "",
                PostIndex = p.Discussion.Posts.Count(x => x.CreatedAt <= p.CreatedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Skip the opening post — already covered by DiscussionCreatedDiscordHandler
        if (data is null || data.PostIndex <= 1) return;

        var url = $"/c/{data.CommunitySlug}/h/{data.HubSlug}/{data.SpaceSlug}/{data.DiscussionSlug}";
        await discordService.NotifyPostCreatedAsync(
            data.SpacePublicId, data.DiscussionTitle, url, data.AuthorName, data.SpaceName);
    }
}
