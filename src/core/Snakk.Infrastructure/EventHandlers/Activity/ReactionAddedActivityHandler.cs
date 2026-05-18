namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Shared.Enums;

public class ReactionAddedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<ReactionAddedEvent>
{
    public async Task HandleAsync(ReactionAddedEvent @event, CancellationToken cancellationToken = default)
    {
        var data = await context.PostReactions
            .Where(r => r.PublicId == @event.ReactionId.Value)
            .Select(r => new {
                Username = r.User.DisplayName ?? "",
                ReactionType = ((ReactionTypeEnum)r.TypeId).ToString(),
                TargetId = r.Post.PublicId,
                DiscussionTitle = r.Post.Discussion.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
            return;

        await activityBroadcaster.BroadcastReactionAdded(
            @event.UserId.Value,
            data.Username,
            data.ReactionType,
            "post",
            data.TargetId,
            data.DiscussionTitle);
    }
}
