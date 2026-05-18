namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class FollowCreatedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<FollowCreatedEvent>
{
    public async Task HandleAsync(FollowCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var data = await context.UserFollows
            .Where(f => f.PublicId == @event.FollowId.Value)
            .Select(f => new {
                Username = f.User.DisplayName ?? "",
                TargetType =
                    f.FollowedUser != null ? "user"
                    : f.Space != null ? "space"
                    : f.Discussion != null ? "discussion" : "unknown",
                TargetId =
                    f.FollowedUser != null ? f.FollowedUser.PublicId
                    : f.Space != null ? f.Space.PublicId
                    : f.Discussion != null ? f.Discussion.PublicId : "",
                TargetName =
                    f.FollowedUser != null ? f.FollowedUser.DisplayName
                    : f.Space != null ? f.Space.Name
                    : f.Discussion != null ? f.Discussion.Title : null })
            .FirstOrDefaultAsync(cancellationToken);

        if (data == null)
            return;

        await activityBroadcaster.BroadcastFollowAdded(
            @event.UserId.Value,
            data.Username,
            data.TargetType,
            data.TargetId,
            data.TargetName);
    }
}
