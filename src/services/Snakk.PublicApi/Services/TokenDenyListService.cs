namespace Snakk.PublicApi.Services;

using Microsoft.Extensions.Caching.Memory;

public interface ITokenDenyListService
{
    bool IsRevoked(string jti);
    void Revoke(string jti, TimeSpan ttl);
}

public class TokenDenyListService(IMemoryCache cache) : ITokenDenyListService
{
    private static string Key(string jti) => $"deny:{jti}";

    public bool IsRevoked(string jti) => cache.TryGetValue(Key(jti), out _);

    public void Revoke(string jti, TimeSpan ttl) =>
        cache.Set(Key(jti), true, ttl);
}
