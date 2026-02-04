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
    private readonly MetricsService _metricsService = metricsService;
    private readonly SnakkDbContext _context = context;

    public async Task HandleAsync(ReactionAddedEvent @event)
    {
        // Get post context and author
        var postContext = await _context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => new
            {
                PostAuthorId = p.CreatedByUser.PublicId,
                SpaceId = p.Discussion.SpaceId,
                HubId = p.Discussion.Space.HubId,
                CommunityId = p.Discussion.Space.Hub.CommunityId
            })
            .FirstOrDefaultAsync();

        if (postContext == null)
            return;

        // Increment REACTION_GIVEN for the person giving the reaction
        await _metricsService.IncrementMetricAsync(
            @event.UserId,
            "REACTION_GIVEN",
            spaceId: postContext.SpaceId,
            hubId: postContext.HubId,
            communityId: postContext.CommunityId);

        // Increment REACTION_RECEIVED for the post author (if not self-reaction)
        if (postContext.PostAuthorId != @event.UserId.Value)
        {
            await _metricsService.IncrementMetricAsync(
                Domain.ValueObjects.UserId.From(postContext.PostAuthorId),
                "REACTION_RECEIVED",
                spaceId: postContext.SpaceId,
                hubId: postContext.HubId,
                communityId: postContext.CommunityId);
        }
    }
}
