namespace Snakk.Infrastructure.EventHandlers.Achievements;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;
using Snakk.Application.Events;

public class ReactionAddedAchievementHandler(
    MetricsService metricsService,
    SnakkDbContext context) : IDomainEventHandler<ReactionAddedEvent>
{
    public async Task HandleAsync(ReactionAddedEvent @event)
    {
        // Get post context and author
        var postContext = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => new {
                p.CreatedByUser.PublicId,
                p.Discussion.SpaceId,
                p.Discussion.Space.HubId,
                p.Discussion.Space.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (postContext is null)
            return;

        // Increment REACTION_GIVEN for the person giving the reaction
        await metricsService.IncrementMetricAsync(
            @event.UserId,
            "REACTION_GIVEN",
            spaceId: postContext.SpaceId,
            hubId: postContext.HubId,
            communityId: postContext.CommunityId);

        // Increment REACTION_RECEIVED for the post author (if not self-reaction)
        if (postContext.PublicId != @event.UserId.Value)
        {
            await metricsService.IncrementMetricAsync(
                Domain.ValueObjects.UserId.From(postContext.PublicId),
                "REACTION_RECEIVED",
                spaceId: postContext.SpaceId,
                hubId: postContext.HubId,
                communityId: postContext.CommunityId);
        }
    }
}
