namespace Snakk.Infrastructure.EventHandlers.Achievements;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;

public class DiscussionCreatedAchievementHandler(
    MetricsService metricsService,
    SnakkDbContext context) : IDomainEventHandler<DiscussionCreatedEvent>
{
    public async Task HandleAsync(DiscussionCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Get Space/Hub/Community IDs
        var spaceContext = await context.Spaces
            .Where(s => s.PublicId == @event.SpaceId.Value)
            .Select(s => new {
                s.Id,
                s.HubId,
                s.Hub.CommunityId })
            .FirstOrDefaultAsync(cancellationToken);

        if (spaceContext is null)
            return;

        // Increment DISCUSSION_COUNT across all scopes
        await metricsService.IncrementMetricAsync(
            @event.CreatedByUserId,
            "DISCUSSION_COUNT",
            spaceId: spaceContext.Id,
            hubId: spaceContext.HubId,
            communityId: spaceContext.CommunityId);
    }
}
