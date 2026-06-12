namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserDatabaseEntity>(context), IUserRepository
{
    private static string EscapeLikePattern(string input) => input
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
    public async Task<UserDatabaseEntity?> GetForUpdateAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .AsTracking()
        .FirstOrDefaultAsync(u => u.PublicId == publicId, ct);

    public async Task<UserDetailDto?> GetForDisplayAsync(string publicId, CancellationToken ct = default) => await _dbSet
        .Where(u => u.PublicId == publicId)
        .Select(u => new UserDetailDto(
            u.PublicId,
            u.DisplayName ?? "",
            u.Email,
            u.CreatedAt,
            u.LastSeenAt))
        .FirstOrDefaultAsync(ct);

    public async Task<UserDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(u => u.PublicId == publicId, ct);

    public async Task<IEnumerable<UserDatabaseEntity>> GetByPublicIdsAsync(IEnumerable<string> publicIds, CancellationToken ct = default)
    {
        var publicIdsList = publicIds.ToList();

        return await _dbSet
            .Where(u => publicIdsList.Contains(u.PublicId))
            .ToListAsync(ct);
    }

    public async Task<UserDatabaseEntity?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        // Hash must match the write path in EmailProtector.ComputeHash:
        //   SHA256(UTF8(email.Trim().ToLowerInvariant())) → Convert.ToHexStringLower(bytes)
        // Filtering on EmailHash uses the unique partial index IX_User_EmailHash instead of
        // scanning the encrypted Email column which cannot be indexed for equality.
        var hash = System.Security.Cryptography.SHA256
            .HashData(System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        var emailHash = Convert.ToHexStringLower(hash);
        return await _dbSet.FirstOrDefaultAsync(u => u.EmailHash == emailHash, ct);
    }

    public async Task<UserDatabaseEntity?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default) =>
        _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await _dbSet.FirstOrDefaultAsync(u => EF.Functions.ILike(u.DisplayName!, displayName), ct)
            : await _dbSet.FirstOrDefaultAsync(u => EF.Functions.Like(u.DisplayName!, displayName), ct);

    public async Task<IEnumerable<UserDatabaseEntity>> SearchByDisplayNameAsync(
        string query,
        int limit,
        CancellationToken ct = default) => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
        ? await _dbSet
            .Where(u => EF.Functions.ILike(u.DisplayName!, $"%{EscapeLikePattern(query)}%", "\\"))
            .Take(limit)
            .ToListAsync(ct)
        : await _dbSet
            .Where(u => EF.Functions.Like(u.DisplayName!, $"%{EscapeLikePattern(query)}%", "\\"))
            .Take(limit)
            .ToListAsync(ct);
}
