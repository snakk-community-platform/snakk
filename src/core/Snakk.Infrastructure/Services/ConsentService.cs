namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ConsentService(SnakkDbContext db) : IConsentService
{
    public async Task<List<ConsentTypeInfo>> GetActiveRequiredConsentsAsync(CancellationToken ct = default)
    {
        // For each active+required consent type, get the latest version
        var types = await db.ConsentTypes
            .Where(ct2 => ct2.IsActive && ct2.IsRequired)
            .OrderBy(ct2 => ct2.DisplayOrder)
            .Select(ct2 => new ConsentTypeInfo(
                ct2.Slug,
                ct2.Name,
                ct2.ShortLabel,
                ct2.LinkUrl,
                ct2.IsRequired,
                ct2.DisplayOrder,
                ct2.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.Id).FirstOrDefault(),
                ct2.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.VersionNumber).FirstOrDefault()))
            .ToListAsync(ct);

        return types.Where(t => t.LatestVersionId > 0).ToList();
    }

    public async Task<ConsentVersionInfo?> GetLatestVersionAsync(string slug, CancellationToken ct = default) =>
        await db.ConsentTypeVersions
            .Where(v => v.ConsentType.Slug == slug && v.ConsentType.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ConsentVersionInfo(v.Id, v.VersionNumber, v.Text, v.CreatedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<List<PendingConsent>> GetPendingConsentsAsync(string userPublicId, CancellationToken ct = default)
    {
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == 0) return [];

        // Get latest version ID for each active required type
        var requiredTypes = await db.ConsentTypes
            .Where(ct2 => ct2.IsActive && ct2.IsRequired)
            .OrderBy(ct2 => ct2.DisplayOrder)
            .Select(ct2 => new
            {
                ct2.Slug,
                ct2.Name,
                ct2.ShortLabel,
                ct2.LinkUrl,
                LatestVersionId = ct2.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        if (requiredTypes.Count == 0) return [];

        var latestVersionIds = requiredTypes
            .Where(t => t.LatestVersionId > 0)
            .Select(t => t.LatestVersionId)
            .ToList();

        // Get which of these the user has already accepted
        var acceptedVersionIds = await db.UserConsents
            .Where(uc => uc.UserId == userId && latestVersionIds.Contains(uc.ConsentTypeVersionId))
            .Select(uc => uc.ConsentTypeVersionId)
            .ToHashSetAsync(ct);

        return requiredTypes
            .Where(t => t.LatestVersionId > 0 && !acceptedVersionIds.Contains(t.LatestVersionId))
            .Select(t => new PendingConsent(t.Slug, t.Name, t.ShortLabel, t.LinkUrl, t.LatestVersionId))
            .ToList();
    }

    public async Task AcceptConsentAsync(string userPublicId, int consentTypeVersionId, string? ipAddress, CancellationToken ct = default)
    {
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == 0) return;

        // Check not already accepted
        var exists = await db.UserConsents
            .AnyAsync(uc => uc.UserId == userId && uc.ConsentTypeVersionId == consentTypeVersionId, ct);

        if (exists) return;

        db.UserConsents.Add(new UserConsentDatabaseEntity
        {
            UserId = userId,
            ConsentTypeVersionId = consentTypeVersionId,
            AcceptedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task AcceptConsentsAsync(string userPublicId, IEnumerable<int> consentTypeVersionIds, string? ipAddress, CancellationToken ct = default)
    {
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == 0) return;

        var versionIds = consentTypeVersionIds.ToList();

        // Get already accepted
        var alreadyAccepted = await db.UserConsents
            .Where(uc => uc.UserId == userId && versionIds.Contains(uc.ConsentTypeVersionId))
            .Select(uc => uc.ConsentTypeVersionId)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var versionId in versionIds.Where(id => !alreadyAccepted.Contains(id)))
        {
            db.UserConsents.Add(new UserConsentDatabaseEntity
            {
                UserId = userId,
                ConsentTypeVersionId = versionId,
                AcceptedAt = now,
                IpAddress = ipAddress
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasAllRequiredConsentsAsync(string userPublicId, CancellationToken ct = default)
    {
        var pending = await GetPendingConsentsAsync(userPublicId, ct);
        return pending.Count == 0;
    }
}
