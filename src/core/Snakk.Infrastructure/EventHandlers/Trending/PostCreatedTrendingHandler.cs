namespace Snakk.Infrastructure.EventHandlers.Trending;

using Snakk.Application.Events;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class PostCreatedTrendingHandler(SnakkDbContext context) : IDomainEventHandler<PostCreatedEvent>
{
    public async Task HandleAsync(PostCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        await TrendScoreCalculator.RecalculateAsync(context, @event.DiscussionId.Value, cancellationToken);
    }
}
