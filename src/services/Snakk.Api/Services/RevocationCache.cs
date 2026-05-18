namespace Snakk.Api.Services;

using System.Collections.Concurrent;
using Snakk.Application.Services;

public class RevocationCache : IRevocationCache
{
    // TTL chosen to cover the maximum cookie lifetime (8h access + 30s buffer).
    // After expiry the entry is removed lazily on the next read or via Sweep().
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(8).Add(TimeSpan.FromSeconds(30));

    private readonly ConcurrentDictionary<string, DateTime> _entries = new();

    public void RevokeUser(string userId) =>
        _entries[userId] = DateTime.UtcNow.Add(Ttl);

    public bool IsUserRevoked(string userId)
    {
        if (!_entries.TryGetValue(userId, out var expiry)) return false;

        if (DateTime.UtcNow < expiry) return true;

        _entries.TryRemove(userId, out _);
        return false;
    }

    public void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _entries.Keys)
        {
            if (_entries.TryGetValue(key, out var expiry) && now >= expiry)
                _entries.TryRemove(key, out _);
        }
    }
}
