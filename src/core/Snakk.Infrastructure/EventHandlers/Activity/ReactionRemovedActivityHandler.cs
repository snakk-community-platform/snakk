namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class ReactionRemovedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<ReactionRemovedEvent>
{
    public async Task HandleAsync(ReactionRemovedEvent @event)
    {
        // Reaction is already deleted by the time the handler runs,
        // so we look up user and post separately from event fields.
        var user = await context.Users
            .Where(u => u.PublicId == @event.UserId.Value)
            .Select(u => new { u.DisplayName })
            .FirstOrDefaultAsync();

        var post = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => new {
                PostId = p.PublicId,
                DiscussionTitle = p.Discussion.Title })
            .FirstOrDefaultAsync();

        if (user is null || post is null)
            return;

        await activityBroadcaster.BroadcastReactionAdded(
            @event.UserId.Value,
            user.DisplayName ?? "",
            $"{@event.Type} (removed)",
            "post",
            post.PostId,
            post.DiscussionTitle);
    }
}
