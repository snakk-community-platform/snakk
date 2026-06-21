namespace Snakk.Infrastructure.EventHandlers.Trending;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class ReactionRemovedTrendingHandler(SnakkDbContext context, IVolumeWindowService volumeWindowService) : IDomainEventHandler<ReactionRemovedEvent>
{
    public async Task HandleAsync(ReactionRemovedEvent @event, CancellationToken cancellationToken = default)
    {
        var discussionPublicId = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => p.Discussion.PublicId)
            .FirstOrDefaultAsync(cancellationToken);

        if (discussionPublicId is null)
            return;

        var window = await volumeWindowService.EstimatePostWindowAsync(cancellationToken);
        await TrendScoreCalculator.RecalculateAsync(context, discussionPublicId, window, cancellationToken);
    }
}
