namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class UserCreatedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<UserCreatedEvent>
{
    public async Task HandleAsync(UserCreatedEvent @event)
    {
        var user = await context.Users
            .Where(u => u.PublicId == @event.UserId.Value)
            .Select(u => new {
                u.DisplayName,
                u.Email })
            .FirstOrDefaultAsync();

        if (user is null)
            return;

        await activityBroadcaster.BroadcastUserRegistered(
            @event.UserId.Value,
            user.DisplayName,
            user.Email ?? "");
    }
}
