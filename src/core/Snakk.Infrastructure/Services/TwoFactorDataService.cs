namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

/// <summary>
/// Encapsulates the AsTracking query + backup-code consumption + lockout
/// mutation + SaveChanges sequence that previously lived inside Snakk.Api's
/// VerifyTwoFactorAsync (REST) and VerifyTwoFactorLogin (gRPC).
///
/// The EF tracking context is per-scoped lifetime (matching SnakkDbContext),
/// so mutations written in <see cref="ConsumeBackupCodeAsync"/> and
/// <see cref="IncrementFailedLoginAttemptsAsync"/> share the same context as
/// the initial load — important for the AsTracking semantics to apply.
/// </summary>
public class TwoFactorDataService(SnakkDbContext context) : ITwoFactorDataService
{
    // ── REST path: look up by PublicId ────────────────────────────────────────

    public async Task<TwoFactorUserData?> GetUserForTwoFactorVerifyAsync(
        string userPublicId, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, ct);

        if (user is null || !user.TwoFactorEnabled)
            return null;

        return Map(user);
    }

    // ── gRPC path: look up by email hash ─────────────────────────────────────

    public async Task<TwoFactorUserData?> GetUserForTwoFactorLoginByEmailHashAsync(
        string emailHash, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .Include(u => u.TwoFactorBackupCodes)
            .Include(u => u.Roles.Where(r => r.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.EmailHash == emailHash, ct);

        if (user is null || !user.TwoFactorEnabled)
            return null;

        return Map(user);
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task ConsumeBackupCodeAsync(int backupCodeId, string? usedIp, CancellationToken ct = default)
    {
        // The backup code entity is already change-tracked because the same
        // context instance loaded it via the Include above. We find it again
        // by primary key so this method works without the caller passing the
        // entity reference.
        var code = await context.TwoFactorBackupCodes.FindAsync([backupCodeId], ct)
            ?? throw new InvalidOperationException($"BackupCode {backupCodeId} not found");

        code.IsUsed = true;
        code.UsedAt = DateTime.UtcNow;
        code.UsedIp = usedIp;
        await context.SaveChangesAsync(ct);
    }

    public async Task IncrementFailedLoginAttemptsAsync(string userPublicId, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, ct)
            ?? throw new InvalidOperationException($"User {userPublicId} not found");

        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= 5)
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);

        await context.SaveChangesAsync(ct);
    }

    public async Task RecordSuccessfulLoginAsync(string userPublicId, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, ct)
            ?? throw new InvalidOperationException($"User {userPublicId} not found");

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TwoFactorUserData Map(Database.Entities.UserDatabaseEntity u) => new()
    {
        PublicId = u.PublicId,
        DisplayName = u.DisplayName,
        Email = u.Email,
        EmailVerified = u.EmailVerified,
        AvatarFileName = u.AvatarFileName,
        AuthVersion = u.AuthVersion,
        TwoFactorEnabled = u.TwoFactorEnabled,
        TwoFactorSecret = u.TwoFactorSecret,
        LockoutEnd = u.LockoutEnd,
        BackupCodes = u.TwoFactorBackupCodes
            .Select(bc => new TwoFactorBackupCodeData
            {
                Id = bc.Id,
                IsUsed = bc.IsUsed,
                CodeHash = bc.CodeHash
            })
            .ToList(),
        Roles = u.Roles
            .Select(r => ((UserRoleTypeEnum)r.RoleId).ToString())
            .ToList()
    };
}
