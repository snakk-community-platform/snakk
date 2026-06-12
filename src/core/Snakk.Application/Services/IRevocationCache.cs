namespace Snakk.Application.Services;

public interface IRevocationCache
{
    Task RevokeUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsUserRevokedAsync(string userId, CancellationToken cancellationToken = default);
}
