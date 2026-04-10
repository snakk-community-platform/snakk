namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class PostCreatedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<PostCreatedEvent>
{
    public async Task HandleAsync(PostCreatedEvent @event)
    {
        // Get user and discussion details
        var data = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => new {
                Username = p.CreatedByUser.DisplayName ?? "",
                DiscussionId = p.Discussion.PublicId,
                DiscussionTitle = p.Discussion.Title,
                CommunityName = p.Discussion.Space.Hub.Community.Name,
                HubName = p.Discussion.Space.Hub.Name,
                SpaceName = p.Discussion.Space.Name })
            .FirstOrDefaultAsync();

        if (data is null)
            return;

        await activityBroadcaster.BroadcastPostCreated(
            @event.CreatedByUserId.Value,
            data.Username,
            @event.PostId.Value,
            data.DiscussionId,
            data.DiscussionTitle,
            data.CommunityName,
            data.HubName,
            data.SpaceName);
    }
}
