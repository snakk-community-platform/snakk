namespace Snakk.Infrastructure.EventHandlers.Trending;

using Snakk.Application.Events;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class PostCreatedTrendingHandler(SnakkDbContext context, IVolumeWindowService volumeWindowService) : IDomainEventHandler<PostCreatedEvent>
{
    public async Task HandleAsync(PostCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var window = await volumeWindowService.EstimatePostWindowAsync(cancellationToken);
        await TrendScoreCalculator.RecalculateAsync(context, @event.DiscussionId.Value, window, cancellationToken);
    }
}
