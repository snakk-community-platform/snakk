namespace Snakk.Infrastructure.EventHandlers.Trending;

using StackExchange.Redis;
using Snakk.Application.Events;
using Snakk.Domain.Events;

public class PostCreatedTrendingHandler(IConnectionMultiplexer redis) : IDomainEventHandler<PostCreatedEvent>
{
    public Task HandleAsync(PostCreatedEvent @event, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().SetAddAsync(TrendDirtyKey, @event.DiscussionId.Value);

    public const string TrendDirtyKey = "snakk:counter:trend:dirty";
}
