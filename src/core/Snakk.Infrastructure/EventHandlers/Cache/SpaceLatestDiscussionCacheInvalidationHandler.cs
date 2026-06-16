namespace Snakk.Infrastructure.EventHandlers.Cache;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Snakk.Application.Events;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

/// <summary>
/// Invalidates the per-space latest-discussion cache entry
/// (<c>space-latest-discussion:{internalSpaceId}</c>) when a discussion is created or a post is
/// added. Either event changes which discussion is "latest" for the space.
/// Failures are caught and logged so that cache misses never fail the write path.
/// </summary>
public class DiscussionCreatedSpaceLatestCacheHandler(
    SnakkDbContext context,
    HybridCache cache,
    ILogger<DiscussionCreatedSpaceLatestCacheHandler> logger) : IDomainEventHandler<DiscussionCreatedEvent>
{
    public async Task HandleAsync(DiscussionCreatedEvent @event, CancellationToken ct = default)
    {
        try
        {
            var internalSpaceId = await context.Spaces
                .Where(s => s.PublicId == @event.SpaceId.Value)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (internalSpaceId is null) return;

            await cache.RemoveAsync($"space-latest-discussion:{internalSpaceId}", ct);
            await cache.RemoveAsync($"space-meta:{@event.SpaceId.Value}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to invalidate space-latest-discussion cache for space {SpacePublicId} on DiscussionCreated",
                @event.SpaceId.Value);
        }
    }
}

/// <summary>
/// Invalidates the per-space latest-discussion cache entry when a post is added to a discussion
/// (a new post changes the LastActivityAt / latest-discussion ordering for the space).
/// </summary>
public class PostCreatedSpaceLatestCacheHandler(
    SnakkDbContext context,
    HybridCache cache,
    ILogger<PostCreatedSpaceLatestCacheHandler> logger) : IDomainEventHandler<PostCreatedEvent>
{
    public async Task HandleAsync(PostCreatedEvent @event, CancellationToken ct = default)
    {
        try
        {
            var data = await context.Discussions
                .Where(d => d.PublicId == @event.DiscussionId.Value)
                .Select(d => new { InternalSpaceId = (int?)d.SpaceId, d.SpacePublicId })
                .FirstOrDefaultAsync(ct);

            if (data is null) return;

            await cache.RemoveAsync($"space-latest-discussion:{data.InternalSpaceId}", ct);
            await cache.RemoveAsync($"space-meta:{data.SpacePublicId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to invalidate space-latest-discussion cache for discussion {DiscussionPublicId} on PostCreated",
                @event.DiscussionId.Value);
        }
    }
}
