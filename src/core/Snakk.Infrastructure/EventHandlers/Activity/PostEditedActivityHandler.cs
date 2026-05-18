namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class PostEditedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<PostEditedEvent>
{
    public async Task HandleAsync(PostEditedEvent @event, CancellationToken cancellationToken = default)
    {
        var data = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => new {
                UserId = p.CreatedByUser.PublicId,
                Username = p.CreatedByUser.DisplayName ?? "",
                DiscussionTitle = p.Discussion.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
            return;

        await activityBroadcaster.BroadcastModerationAction(
            data.UserId,
            data.Username,
            "PostEdited",
            "Post",
            @event.PostId.Value,
            data.DiscussionTitle,
            null);
    }
}
