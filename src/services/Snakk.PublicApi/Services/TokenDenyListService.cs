namespace Snakk.PublicApi.Services;

using Microsoft.Extensions.Caching.Distributed;

public interface ITokenDenyListService
{
    bool IsRevoked(string jti);
    void Revoke(string jti, TimeSpan ttl);
}

public class TokenDenyListService(IDistributedCache cache) : ITokenDenyListService
{
    private static readonly byte[] Sentinel = [1];
    private static string Key(string jti) => $"deny:{jti}";

    public bool IsRevoked(string jti) => cache.Get(Key(jti)) is not null;

    public void Revoke(string jti, TimeSpan ttl) =>
        cache.Set(Key(jti), Sentinel,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
}
