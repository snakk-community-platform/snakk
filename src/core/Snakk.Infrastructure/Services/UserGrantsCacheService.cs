namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

public class UserGrantsCacheService(
    SnakkDbContext context,
    IMemoryCache cache,
    IConfiguration configuration) : IUserGrantsCacheService
{
    private readonly TimeSpan _grantsTtl = TimeSpan.FromMinutes(
        configuration.GetValue("AccessCache:GrantsTtlMinutes", 5));

    private readonly TimeSpan _platformCountTtl = TimeSpan.FromSeconds(
        configuration.GetValue("AccessCache:PlatformCountTtlSeconds", 30));

    // ── User grant cache ──────────────────────────────────────────────────────

    // Generation counter: incrementing it orphans all old user-grants keys,
    // effectively evicting the entire user-grants cache without needing to
    // track individual keys. Old entries expire naturally via their TTL.
    private long GetGeneration()
    {
        cache.TryGetValue("user-grants-gen", out long gen);
        return gen;
    }

    private string UserGrantsKey(string userId) =>
        $"user-grants:{userId}:{GetGeneration()}";

    public async Task<UserGrants> GetGrantsAsync(string userId)
    {
        var key = UserGrantsKey(userId);

        if (cache.TryGetValue(key, out UserGrants? cached) && cached is not null)
            return cached;

        var raw = await context.GroupMembers
            .Where(gm => gm.User.PublicId == userId)
            .SelectMany(gm => context.GroupAccess.Where(ga =>
                ga.GroupId == gm.GroupId && ga.AccessLevel >= (int)AccessLevelEnum.Read))
            .Select(ga => new { ga.SpaceId, ga.HubId, ga.CommunityId })
            .ToListAsync();

        var grants = new UserGrants(
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

        cache.Set(key, grants, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _grantsTtl
        });

        return grants;
    }

    public void Invalidate(string userId) =>
        cache.Remove(UserGrantsKey(userId));

    public void InvalidateAll() =>
        cache.Set("user-grants-gen", GetGeneration() + 1);

    // ── Restricted entity set ────────────────────────────────────────────────

    // IsRestricted changes very rarely (admin action), so the TTL here is a
    // safety net only — event-driven invalidation via InvalidateRestrictedCount()
    // handles the normal case immediately.
    private const string RestrictedEntitiesKey = "restricted-entities";

    public async Task<RestrictedEntitySet> GetRestrictedEntitiesAsync()
    {
        if (cache.TryGetValue(RestrictedEntitiesKey, out RestrictedEntitySet? cached) && cached is not null)
            return cached;

        var spaceIds = await context.Spaces
            .Where(s => s.IsRestricted)
            .Select(s => s.Id)
            .ToListAsync();

        var hubIds = await context.Hubs
            .Where(h => h.IsRestricted)
            .Select(h => h.Id)
            .ToListAsync();

        var communityIds = await context.Communities
            .Where(c => c.IsRestricted)
            .Select(c => c.Id)
            .ToListAsync();

        var result = spaceIds.Count == 0 && hubIds.Count == 0 && communityIds.Count == 0
            ? RestrictedEntitySet.Empty
            : new RestrictedEntitySet(
                [.. spaceIds],
                [.. hubIds],
                [.. communityIds]);

        cache.Set(RestrictedEntitiesKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _platformCountTtl
        });

        return result;
    }

    public async Task<bool> AnyRestrictedAsync() =>
        !(await GetRestrictedEntitiesAsync()).IsEmpty;

    public void InvalidateRestrictedCount() =>
        cache.Remove(RestrictedEntitiesKey);

    // ── Adult-hiding space set ────────────────────────────────────────────────

    private const string AdultHidingSpacesKey = "adult-hiding-spaces";

    public async Task<HashSet<int>> GetAdultHidingSpaceIdsAsync()
    {
        if (cache.TryGetValue(AdultHidingSpacesKey, out HashSet<int>? cached) && cached is not null)
            return cached;

        var spaceIds = await context.Spaces
            .Where(s => s.CommunityHideAdultDiscussionsFromLists)
            .Select(s => s.Id)
            .ToListAsync();

        HashSet<int> result = [.. spaceIds];

        cache.Set(AdultHidingSpacesKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _platformCountTtl
        });

        return result;
    }

    public void InvalidateAdultHidingSpaces() =>
        cache.Remove(AdultHidingSpacesKey);
}
