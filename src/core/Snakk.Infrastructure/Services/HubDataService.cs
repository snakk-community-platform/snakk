namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class HubDataService(
    SnakkDbContext dbContext,
    HybridCache cache) : IHubDataService
{
    // Keep the cache key byte-identical to the key used in the original GrpcService
    // so that any existing write-path RemoveAsync("hub-meta:{publicId}") calls
    // continue to invalidate entries written by this service.
    private static readonly HybridCacheEntryOptions MetaCacheOptions =
        new() { Expiration = TimeSpan.FromMinutes(5) };

    public async Task<IReadOnlyDictionary<string, HubCountsDto>> GetCountsByPublicIdsAsync(
        IEnumerable<string> publicIds,
        CancellationToken ct = default)
    {
        var ids = publicIds.ToList();

        var rows = await dbContext.Hubs
            .Where(h => ids.Contains(h.PublicId))
            .Select(h => new { h.PublicId, h.SpaceCount, h.DiscussionCount, h.PostCount })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.PublicId,
            r => new HubCountsDto(r.SpaceCount, r.DiscussionCount, r.PostCount));
    }

    public async Task<HubMetaDto?> GetHubMetaAsync(string publicId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<HubMetaDto?>(
            $"hub-meta:{publicId}",
            async cancel =>
            {
                var raw = await dbContext.Hubs
                    .Where(h => h.PublicId == publicId)
                    .Select(h => new
                    {
                        h.HasRules,
                        h.RulesRevision,
                        h.ParentCommunityHasRules,
                        h.TeamRevision,
                        h.IsRestricted,
                        h.Require2FA,
                        CommunitySlug = h.Community.Slug
                    })
                    .FirstOrDefaultAsync(cancel);
                return raw is null
                    ? null
                    : new HubMetaDto(
                        raw.HasRules,
                        raw.RulesRevision,
                        raw.ParentCommunityHasRules,
                        raw.TeamRevision,
                        raw.IsRestricted,
                        raw.CommunitySlug,
                        raw.Require2FA);
            },
            MetaCacheOptions,
            cancellationToken: ct);
    }
}
