namespace Snakk.Infrastructure.EventHandlers.Activity;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;
using Snakk.Application.Services;

public class DiscussionCreatedActivityHandler(
    IActivityBroadcaster activityBroadcaster,
    SnakkDbContext context) : IDomainEventHandler<DiscussionCreatedEvent>
{
    public async Task HandleAsync(DiscussionCreatedEvent @event)
    {
        var data = await context.Discussions
            .Where(d => d.PublicId == @event.DiscussionId.Value)
            .Select(d => new {
                Username = d.CreatedByUser.DisplayName,
                d.Title,
                CommunityName = d.Space.Hub.Community.Name,
                HubName = d.Space.Hub.Name,
                SpaceName = d.Space.Name })
            .FirstOrDefaultAsync();

        if (data is null)
            return;

        await activityBroadcaster.BroadcastDiscussionCreated(
            @event.CreatedByUserId.Value,
            data.Username,
            @event.DiscussionId.Value,
            data.Title,
            data.CommunityName,
            data.HubName,
            data.SpaceName);
    }
}
