namespace Snakk.Infrastructure.EventHandlers.Achievements;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;

public class PostCreatedAchievementHandler(
    MetricsService metricsService,
    SnakkDbContext context) : IDomainEventHandler<PostCreatedEvent>
{
    public async Task HandleAsync(PostCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Get Space/Hub/Community IDs by querying the discussion
        var discussionContext = await context.Discussions
            .Where(d => d.PublicId == @event.DiscussionId.Value)
            .Select(d => new {
                d.SpaceId,
                d.HubId,
                d.CommunityId })
            .FirstOrDefaultAsync(cancellationToken);

        if (discussionContext is null)
            return; // Discussion not found, skip metrics

        // Increment POST_COUNT across all scopes (Global, Community, Hub, Space)
        await metricsService.IncrementMetricAsync(
            @event.CreatedByUserId,
            "POST_COUNT",
            spaceId: discussionContext.SpaceId,
            hubId: discussionContext.HubId,
            communityId: discussionContext.CommunityId);

        // Note: Achievement checking will be handled by background worker
        // For now, we just update metrics synchronously (fast, 5-10ms)
    }
}
