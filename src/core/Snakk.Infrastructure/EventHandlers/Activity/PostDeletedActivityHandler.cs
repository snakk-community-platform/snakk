namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class PostDeletedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<PostDeletedEvent>
{
    public async Task HandleAsync(PostDeletedEvent @event)
    {
        var data = await context.Users
            .Where(u => u.PublicId == @event.DeletedByUserId.Value)
            .Select(u => new {
                UserId = u.PublicId,
                Username = u.DisplayName ?? "" })
            .FirstOrDefaultAsync();

        if (data is null)
            return;

        await activityBroadcaster.BroadcastContentDeleted(
            data.UserId,
            data.Username,
            @event.IsHardDelete ? "Post (hard delete)" : "Post",
            @event.PostId.Value,
            null);
    }
}
