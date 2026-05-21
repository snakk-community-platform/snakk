namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class EntityHierarchyCacheService(
    SnakkDbContext context,
    HybridCache cache) : IEntityHierarchyCacheService
{
    private static readonly HybridCacheEntryOptions ImmutableOptions = new()
    {
        Expiration = TimeSpan.FromHours(24)
    };

    public async Task<DiscussionHierarchy?> GetDiscussionHierarchyAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:d:{publicId}";
        var result = await cache.GetOrCreateAsync<DiscussionHierarchy?>(
            key,
            cancel => new ValueTask<DiscussionHierarchy?>(context.Discussions
                .Where(d => d.PublicId == publicId)
                .Select(d => new DiscussionHierarchy(d.SpaceId, d.HubId, d.CommunityId))
                .FirstOrDefaultAsync(cancel)),
            ImmutableOptions, cancellationToken: ct);
        if (result is null) await cache.RemoveAsync(key, ct);
        return result;
    }

    public async Task<SpaceHierarchy?> GetSpaceHierarchyAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:s:{publicId}";
        var result = await cache.GetOrCreateAsync<SpaceHierarchy?>(
            key,
            cancel => new ValueTask<SpaceHierarchy?>(context.Spaces
                .Where(s => s.PublicId == publicId)
                .Select(s => new SpaceHierarchy(s.Id, s.HubId, s.Hub.CommunityId))
                .FirstOrDefaultAsync(cancel)),
            ImmutableOptions, cancellationToken: ct);
        if (result is null) await cache.RemoveAsync(key, ct);
        return result;
    }

    public async Task<HubHierarchy?> GetHubHierarchyAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:h:{publicId}";
        var result = await cache.GetOrCreateAsync<HubHierarchy?>(
            key,
            cancel => new ValueTask<HubHierarchy?>(context.Hubs
                .Where(h => h.PublicId == publicId)
                .Select(h => new HubHierarchy(h.Id, h.CommunityId))
                .FirstOrDefaultAsync(cancel)),
            ImmutableOptions, cancellationToken: ct);
        if (result is null) await cache.RemoveAsync(key, ct);
        return result;
    }

    public async Task<int?> GetCommunityIdAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:c:{publicId}";
        var result = await cache.GetOrCreateAsync<int?>(
            key,
            cancel => new ValueTask<int?>(context.Communities
                .Where(c => c.PublicId == publicId)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(cancel)),
            ImmutableOptions, cancellationToken: ct);
        if (result is null) await cache.RemoveAsync(key, ct);
        return result;
    }
}
