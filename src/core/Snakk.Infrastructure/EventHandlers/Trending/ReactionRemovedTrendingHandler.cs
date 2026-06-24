namespace Snakk.Infrastructure.EventHandlers.Trending;

using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Snakk.Application.Events;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class ReactionRemovedTrendingHandler(SnakkDbContext context, IConnectionMultiplexer redis) : IDomainEventHandler<ReactionRemovedEvent>
{
    public async Task HandleAsync(ReactionRemovedEvent @event, CancellationToken cancellationToken = default)
    {
        var discussionPublicId = await context.Posts
            .Where(p => p.PublicId == @event.PostId.Value)
            .Select(p => p.Discussion.PublicId)
            .FirstOrDefaultAsync(cancellationToken);

        if (discussionPublicId is null) return;

        await redis.GetDatabase().SetAddAsync(PostCreatedTrendingHandler.TrendDirtyKey, discussionPublicId);
    }
}
