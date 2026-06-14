namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class CommunityDataService(
    SnakkDbContext dbContext,
    HybridCache cache) : ICommunityDataService
{
    // Keep the cache key byte-identical to the key used in the original GrpcService
    // so that any existing write-path RemoveAsync("community-meta:{publicId}") calls
    // continue to invalidate entries written by this service.
    private static readonly HybridCacheEntryOptions MetaCacheOptions =
        new() { Expiration = TimeSpan.FromMinutes(5) };

    public async Task<IReadOnlyDictionary<string, CommunityCountsDto>> GetCountsByPublicIdsAsync(
        IEnumerable<string> publicIds,
        CancellationToken ct = default)
    {
        var ids = publicIds.ToList();

        var rows = await dbContext.Communities
            .Where(c => ids.Contains(c.PublicId))
            .Select(c => new { c.PublicId, c.DiscussionCount, c.PostCount })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.PublicId,
            r => new CommunityCountsDto(r.DiscussionCount, r.PostCount));
    }

    public async Task<CommunityMetaDto?> GetCommunityMetaAsync(string publicId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<CommunityMetaDto?>(
            $"community-meta:{publicId}",
            async cancel =>
            {
                var raw = await dbContext.Communities
                    .Where(c => c.PublicId == publicId)
                    .Select(c => new { c.HasRules, c.RulesRevision, c.TeamRevision, c.IsRestricted, c.Require2FA })
                    .FirstOrDefaultAsync(cancel);
                return raw is null
                    ? null
                    : new CommunityMetaDto(raw.HasRules, raw.RulesRevision, raw.TeamRevision, raw.IsRestricted, raw.Require2FA);
            },
            MetaCacheOptions,
            cancellationToken: ct);
    }
}
