using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByValueAsync(string tokenValue, CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task UpdateAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllForUserAsync(UserId userId, CancellationToken ct = default);
    Task DeleteExpiredTokensAsync(CancellationToken ct = default);
}
