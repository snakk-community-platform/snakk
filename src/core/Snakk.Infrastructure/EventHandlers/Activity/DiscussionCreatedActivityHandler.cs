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
    private readonly IActivityBroadcaster _activityBroadcaster = activityBroadcaster;
    private readonly SnakkDbContext _context = context;

    public async Task HandleAsync(DiscussionCreatedEvent @event)
    {
        var data = await _context.Discussions
            .Where(d => d.PublicId == @event.DiscussionId.Value)
            .Select(d => new
            {
                Username = d.CreatedByUser.DisplayName,
                Title = d.Title,
                CommunityName = d.Space.Hub.Community.Name,
                HubName = d.Space.Hub.Name,
                SpaceName = d.Space.Name
            })
            .FirstOrDefaultAsync();

        if (data == null)
            return;

        await _activityBroadcaster.BroadcastDiscussionCreated(
            @event.CreatedByUserId.Value,
            data.Username,
            @event.DiscussionId.Value,
            data.Title,
            data.CommunityName,
            data.HubName,
            data.SpaceName);
    }
}
