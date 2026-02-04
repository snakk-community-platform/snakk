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
    private readonly MetricsService _metricsService = metricsService;
    private readonly SnakkDbContext _context = context;

    public async Task HandleAsync(DiscussionCreatedEvent @event)
    {
        // Get Space/Hub/Community IDs
        var spaceContext = await _context.Spaces
            .Where(s => s.PublicId == @event.SpaceId.Value)
            .Select(s => new
            {
                SpaceId = s.Id,
                HubId = s.HubId,
                CommunityId = s.Hub.CommunityId
            })
            .FirstOrDefaultAsync();

        if (spaceContext == null)
            return;

        // Increment DISCUSSION_COUNT across all scopes
        await _metricsService.IncrementMetricAsync(
            @event.CreatedByUserId,
            "DISCUSSION_COUNT",
            spaceId: spaceContext.SpaceId,
            hubId: spaceContext.HubId,
            communityId: spaceContext.CommunityId);
    }
}
