namespace Snakk.Api.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Snakk.Application.Services;

// Distributed-backed revocation cache (HI-63 fix).
//
// Architecture: two-layer cache
//   L1 — ConcurrentDictionary: short-lived per-replica memo of the L2 answer (revoked OR
//         not revoked), so the hot path does not pay a distributed round-trip per request.
//         A fresh revocation is also written to L1 so the issuing replica enforces
//         immediately; other replicas converge within one L1 cycle (≤30 s).
//   L2 — IDistributedCache (Valkey/Redis in production, in-memory fallback in dev): the
//         authoritative store shared by all replicas.
//
// Key pattern : "jwt:user-revoked:{userId}"
// L1 TTL      : 30 seconds (both positive and negative answers)
// L2 TTL      : 8 h 30 s  (covers the maximum access-token lifetime plus a safety margin)
public class RevocationCache(IDistributedCache distributedCache) : IRevocationCache
{
    private static readonly TimeSpan L2Ttl = TimeSpan.FromHours(8).Add(TimeSpan.FromSeconds(30));
    private static readonly TimeSpan L1Ttl = TimeSpan.FromSeconds(30);

    private static readonly byte[] Sentinel = [1];

    // Keep the memo dictionary from accumulating entries for users that never return.
    private const int SweepThreshold = 50_000;

    // L1: memoized L2 answer per user, valid until Expiry.
    private readonly ConcurrentDictionary<string, (bool Revoked, DateTime Expiry)> _l1 = new();

    private static string CacheKey(string userId) => $"jwt:user-revoked:{userId}";

    public async Task RevokeUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Write to L2 first so other replicas can see it.
        await distributedCache.SetAsync(
            CacheKey(userId),
            Sentinel,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = L2Ttl },
            cancellationToken);

        // Also seed L1 so this replica enforces immediately without an L2 round-trip.
        _l1[userId] = (true, DateTime.UtcNow.Add(L1Ttl));
    }

    public async Task<bool> IsUserRevokedAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Fast path: unexpired L1 memo — positive or negative — answers without a round-trip.
        if (_l1.TryGetValue(userId, out var memo) && DateTime.UtcNow < memo.Expiry)
            return memo.Revoked;

        // L2 check (network call, ~1 ms on same-datacenter Valkey).
        var value = await distributedCache.GetAsync(CacheKey(userId), cancellationToken);
        var revoked = value is not null;

        SweepIfOversized();
        _l1[userId] = (revoked, DateTime.UtcNow.Add(L1Ttl));
        return revoked;
    }

    private void SweepIfOversized()
    {
        if (_l1.Count <= SweepThreshold)
            return;

        var now = DateTime.UtcNow;
        foreach (var entry in _l1)
        {
            if (entry.Value.Expiry < now)
                _l1.TryRemove(entry.Key, out _);
        }
    }
}
