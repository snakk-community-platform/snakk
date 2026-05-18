namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

/// <summary>
/// Caches immutable parent-chain IDs using IMemoryCache with NeverRemove priority.
/// Since hierarchy is immutable (no reparenting), entries live until process restart
/// or memory pressure eviction — there is no TTL and no explicit invalidation.
/// </summary>
public class EntityHierarchyCacheService(
    SnakkDbContext context,
    IMemoryCache cache) : IEntityHierarchyCacheService
{
    private static readonly MemoryCacheEntryOptions ImmutableOptions = new()
    {
        Priority = CacheItemPriority.NeverRemove
    };

    public async Task<DiscussionHierarchy?> GetDiscussionHierarchyAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:d:{publicId}";

        if (cache.TryGetValue(key, out DiscussionHierarchy? cached))
            return cached;

        var h = await context.Discussions
            .Where(d => d.PublicId == publicId)
            .Select(d => new DiscussionHierarchy(d.SpaceId, d.HubId, d.CommunityId))
            .FirstOrDefaultAsync(ct);

        if (h is not null)
            cache.Set(key, h, ImmutableOptions);

        return h;
    }

    public async Task<SpaceHierarchy?> GetSpaceHierarchyAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:s:{publicId}";

        if (cache.TryGetValue(key, out SpaceHierarchy? cached))
            return cached;

        var h = await context.Spaces
            .Where(s => s.PublicId == publicId)
            .Select(s => new SpaceHierarchy(s.Id, s.HubId, s.Hub.CommunityId))
            .FirstOrDefaultAsync(ct);

        if (h is not null)
            cache.Set(key, h, ImmutableOptions);

        return h;
    }

    public async Task<HubHierarchy?> GetHubHierarchyAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:h:{publicId}";

        if (cache.TryGetValue(key, out HubHierarchy? cached))
            return cached;

        var h = await context.Hubs
            .Where(h => h.PublicId == publicId)
            .Select(h => new HubHierarchy(h.Id, h.CommunityId))
            .FirstOrDefaultAsync(ct);

        if (h is not null)
            cache.Set(key, h, ImmutableOptions);

        return h;
    }

    public async Task<int?> GetCommunityIdAsync(string publicId, CancellationToken ct = default)
    {
        var key = $"hierarchy:c:{publicId}";

        if (cache.TryGetValue(key, out int cached) && cached != 0)
            return cached;

        var id = await context.Communities
            .Where(c => c.PublicId == publicId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (id is not null)
            cache.Set(key, id.Value, ImmutableOptions);

        return id;
    }
}
