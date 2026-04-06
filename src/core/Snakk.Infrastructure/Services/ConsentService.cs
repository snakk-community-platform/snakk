namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class ConsentService(SnakkDbContext db) : IConsentService
{
    public async Task<List<ConsentTypeInfo>> GetActiveRequiredConsentsAsync()
    {
        // For each active+required consent type, get the latest version
        var types = await db.ConsentTypes
            .Where(ct => ct.IsActive && ct.IsRequired)
            .OrderBy(ct => ct.DisplayOrder)
            .Select(ct => new ConsentTypeInfo(
                ct.Slug,
                ct.Name,
                ct.ShortLabel,
                ct.LinkUrl,
                ct.IsRequired,
                ct.DisplayOrder,
                ct.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.Id).FirstOrDefault(),
                ct.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.VersionNumber).FirstOrDefault()))
            .ToListAsync();

        return types.Where(t => t.LatestVersionId > 0).ToList();
    }

    public async Task<ConsentVersionInfo?> GetLatestVersionAsync(string slug) =>
        await db.ConsentTypeVersions
            .Where(v => v.ConsentType.Slug == slug && v.ConsentType.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ConsentVersionInfo(v.Id, v.VersionNumber, v.Text, v.CreatedAt))
            .FirstOrDefaultAsync();

    public async Task<List<PendingConsent>> GetPendingConsentsAsync(string userPublicId)
    {
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == 0) return [];

        // Get latest version ID for each active required type
        var requiredTypes = await db.ConsentTypes
            .Where(ct => ct.IsActive && ct.IsRequired)
            .OrderBy(ct => ct.DisplayOrder)
            .Select(ct => new
            {
                ct.Slug,
                ct.Name,
                ct.ShortLabel,
                ct.LinkUrl,
                LatestVersionId = ct.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.Id)
                    .FirstOrDefault()
            })
            .ToListAsync();

        if (requiredTypes.Count == 0) return [];

        var latestVersionIds = requiredTypes
            .Where(t => t.LatestVersionId > 0)
            .Select(t => t.LatestVersionId)
            .ToList();

        // Get which of these the user has already accepted
        var acceptedVersionIds = await db.UserConsents
            .Where(uc => uc.UserId == userId && latestVersionIds.Contains(uc.ConsentTypeVersionId))
            .Select(uc => uc.ConsentTypeVersionId)
            .ToHashSetAsync();

        return requiredTypes
            .Where(t => t.LatestVersionId > 0 && !acceptedVersionIds.Contains(t.LatestVersionId))
            .Select(t => new PendingConsent(t.Slug, t.Name, t.ShortLabel, t.LinkUrl, t.LatestVersionId))
            .ToList();
    }

    public async Task AcceptConsentAsync(string userPublicId, int consentTypeVersionId, string? ipAddress)
    {
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == 0) return;

        // Check not already accepted
        var exists = await db.UserConsents
            .AnyAsync(uc => uc.UserId == userId && uc.ConsentTypeVersionId == consentTypeVersionId);

        if (exists) return;

        db.UserConsents.Add(new UserConsentDatabaseEntity
        {
            UserId = userId,
            ConsentTypeVersionId = consentTypeVersionId,
            AcceptedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        });

        await db.SaveChangesAsync();
    }

    public async Task AcceptConsentsAsync(string userPublicId, IEnumerable<int> consentTypeVersionIds, string? ipAddress)
    {
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userId == 0) return;

        var versionIds = consentTypeVersionIds.ToList();

        // Get already accepted
        var alreadyAccepted = await db.UserConsents
            .Where(uc => uc.UserId == userId && versionIds.Contains(uc.ConsentTypeVersionId))
            .Select(uc => uc.ConsentTypeVersionId)
            .ToHashSetAsync();

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

        await db.SaveChangesAsync();
    }

    public async Task<bool> HasAllRequiredConsentsAsync(string userPublicId)
    {
        var pending = await GetPendingConsentsAsync(userPublicId);
        return pending.Count == 0;
    }
}
