namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

public class UserGrantsCacheService(
    SnakkDbContext context,
    HybridCache cache,
    IConfiguration configuration) : IUserGrantsCacheService
{
    private readonly TimeSpan _grantsTtl = TimeSpan.FromMinutes(
        configuration.GetValue("AccessCache:GrantsTtlMinutes", 5));

    private readonly TimeSpan _platformCountTtl = TimeSpan.FromSeconds(
        configuration.GetValue("AccessCache:PlatformCountTtlSeconds", 30));

    private readonly TimeSpan _restrictedEntitiesTtl = TimeSpan.FromMinutes(
        configuration.GetValue("AccessCache:RestrictedEntitiesTtlMinutes", 5));

    // ── User grant cache ──────────────────────────────────────────────────────

    private static string UserGrantsKey(string userId) => $"user-grants:{userId}";

    public Task<UserGrants> GetGrantsAsync(string userId, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            UserGrantsKey(userId),
            async innerCt => await FetchGrantsAsync(userId, innerCt),
            new HybridCacheEntryOptions { Expiration = _grantsTtl },
            tags: ["user-grants"],
            cancellationToken: ct).AsTask();

    private async ValueTask<UserGrants> FetchGrantsAsync(string userId, CancellationToken ct)
    {
        var raw = await context.GroupMembers
            .Where(gm => gm.User.PublicId == userId)
            .SelectMany(gm => context.GroupAccess.Where(ga =>
                ga.GroupId == gm.GroupId && ga.AccessLevel >= (int)AccessLevelEnum.Read))
            .Select(ga => new { ga.SpaceId, ga.HubId, ga.CommunityId })
            .ToListAsync(ct);

        return new UserGrants(
            SpaceIds: raw
                .Where(g => g.SpaceId.HasValue)
                .Select(g => g.SpaceId!.Value)
                .ToHashSet(),
            HubIds: raw
                .Where(g => g.HubId.HasValue)
                .Select(g => g.HubId!.Value)
                .ToHashSet(),
            CommunityIds: raw
                .Where(g => g.CommunityId.HasValue)
                .Select(g => g.CommunityId!.Value)
                .ToHashSet());
    }

    public void Invalidate(string userId)
    {
        _ = cache.RemoveAsync(UserGrantsKey(userId)).AsTask();
        _ = cache.RemoveByTagAsync($"group-access:u:{userId}").AsTask();
    }

    public void InvalidateAll() =>
        _ = cache.RemoveByTagAsync("user-grants").AsTask();

    // ── Restricted entity set ────────────────────────────────────────────────

    private const string RestrictedEntitiesKey = "restricted-entities";

    public Task<RestrictedEntitySet> GetRestrictedEntitiesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            RestrictedEntitiesKey,
            async innerCt => await FetchRestrictedEntitiesAsync(innerCt),
            new HybridCacheEntryOptions { Expiration = _restrictedEntitiesTtl },
            cancellationToken: ct).AsTask();

    private async ValueTask<RestrictedEntitySet> FetchRestrictedEntitiesAsync(CancellationToken ct)
    {
        var spaceIds = await context.Spaces
            .Where(s => s.IsRestricted)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var hubIds = await context.Hubs
            .Where(h => h.IsRestricted)
            .Select(h => h.Id)
            .ToListAsync(ct);

        var communityIds = await context.Communities
            .Where(c => c.IsRestricted)
            .Select(c => c.Id)
            .ToListAsync(ct);

        return spaceIds.Count == 0 && hubIds.Count == 0 && communityIds.Count == 0
            ? RestrictedEntitySet.Empty
            : new RestrictedEntitySet(
                [.. spaceIds],
                [.. hubIds],
                [.. communityIds]);
    }

    public async Task<bool> AnyRestrictedAsync(CancellationToken ct = default) =>
        !(await GetRestrictedEntitiesAsync(ct)).IsEmpty;

    public void InvalidateRestrictedCount() =>
        _ = cache.RemoveAsync(RestrictedEntitiesKey).AsTask();

    // ── Adult-hiding space set ────────────────────────────────────────────────

    private const string AdultHidingSpacesKey = "adult-hiding-spaces";

    public Task<HashSet<int>> GetAdultHidingSpaceIdsAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            AdultHidingSpacesKey,
            async innerCt => await FetchAdultHidingSpaceIdsAsync(innerCt),
            new HybridCacheEntryOptions { Expiration = _platformCountTtl },
            cancellationToken: ct).AsTask();

    private async ValueTask<HashSet<int>> FetchAdultHidingSpaceIdsAsync(CancellationToken ct)
    {
        var spaceIds = await context.Spaces
            .Where(s => s.CommunityHideAdultDiscussionsFromLists)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return [.. spaceIds];
    }

    public void InvalidateAdultHidingSpaces() =>
        _ = cache.RemoveAsync(AdultHidingSpacesKey).AsTask();
}
