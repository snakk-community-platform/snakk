using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByValueAsync(string tokenValue);
    Task<IEnumerable<RefreshToken>> GetByUserIdAsync(UserId userId);
    Task AddAsync(RefreshToken token);
    Task UpdateAsync(RefreshToken token);
    Task RevokeAllForUserAsync(UserId userId);
    Task DeleteExpiredTokensAsync();
}
