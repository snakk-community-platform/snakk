namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

public class MeDataService(SnakkDbContext context) : IMeDataService
{
    public async Task<UserCredentialData?> GetUserCredentialDataAsync(
        string userPublicId, CancellationToken ct = default)
    {
        var row = await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => new { u.PasswordHash, u.TwoFactorEnabled })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new UserCredentialData(row.PasswordHash, row.TwoFactorEnabled);
    }

    public async Task<string?> GetPasswordHashAsync(string userPublicId, CancellationToken ct = default) =>
        await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => u.PasswordHash)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetEncryptedEmailAsync(string userPublicId, CancellationToken ct = default) =>
        await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetEmailAsync(string userPublicId, CancellationToken ct = default) =>
        await context.Users
            .Where(u => u.PublicId == userPublicId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

    public async Task<List<string>> GetUserRolesAsync(string userPublicId, CancellationToken ct = default) =>
        await context.Users
            .Where(u => u.PublicId == userPublicId)
            .SelectMany(u => u.Roles.Where(r => r.RevokedAt == null))
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToListAsync(ct);
}
