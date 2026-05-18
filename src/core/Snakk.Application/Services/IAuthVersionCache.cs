namespace Snakk.Application.Services;

public interface IAuthVersionCache
{
    // Returns null when the user does not exist in the DB (deleted/unknown) — caller should treat as USER_REVOKED
    Task<long?> GetVersionAsync(string userId, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string userId, CancellationToken cancellationToken = default);
    Task SetAsync(string userId, long version, CancellationToken cancellationToken = default);
}
