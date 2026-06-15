namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Helpers;

public class SlugService(SnakkDbContext context) : ISlugService
{
    private static readonly System.Text.RegularExpressions.Regex SlugRegex =
        new(@"^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public string? ValidateSlugFormat(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return "Slug cannot be empty.";
        if (slug.Length < 3)
            return "Slug must be at least 3 characters.";
        if (slug.Length > 30)
            return "Slug must be at most 30 characters.";
        if (!SlugRegex.IsMatch(slug))
            return "Slug may only contain lowercase letters, numbers, and hyphens, and must start and end with a letter or number.";
        if (ReservedNames.IsReserved(slug))
            return "This name is reserved and cannot be used as a profile URL.";
        return null;
    }

    public bool IsSlugRateLimited(int changeCount, DateTime? lastChangedAt)
    {
        if (changeCount <= 2) return false;
        if (lastChangedAt is null) return false;
        return (DateTime.UtcNow - lastChangedAt.Value).TotalDays < 182; // ~6 months
    }

    public async Task<string> EnsureUniqueSlugAsync(string candidateSlug, int? excludeInternalUserId = null, CancellationToken ct = default)
    {
        var baseSlug = candidateSlug.Length > 30 ? candidateSlug[..30] : candidateSlug;
        baseSlug = baseSlug.TrimEnd('-');

        var slug = baseSlug;
        for (var i = 1; ; i++)
        {
            if (!await IsSlugTakenAsync(slug, excludeInternalUserId, ct))
                return slug;

            var suffix = i.ToString();
            var trimmedBase = baseSlug.Length + suffix.Length > 30
                ? baseSlug[..(30 - suffix.Length)]
                : baseSlug;
            slug = trimmedBase.TrimEnd('-') + suffix;
        }
    }

    public async Task<(bool Available, string? Suggestion)> CheckSlugAvailabilityAsync(
        string slug, string currentUserPublicId, CancellationToken ct = default)
    {
        var internalId = await context.Users
            .Where(u => u.PublicId == currentUserPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        var takenByOther = await context.Users
            .Where(u => u.PublicId != currentUserPublicId && u.Slug == slug)
            .AnyAsync(ct);

        if (!takenByOther)
        {
            takenByOther = await context.UserSlugHistory
                .Where(h => h.UserId != internalId && h.OldSlug == slug)
                .AnyAsync(ct);
        }

        if (!takenByOther)
            return (true, null);

        var suggestion = await EnsureUniqueSlugAsync(slug, internalId, ct);
        return (false, suggestion);
    }

    public async Task UpdateUserSlugAsync(string publicId, string newSlug, bool isCustomized, CancellationToken ct = default)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == publicId, ct)
            ?? throw new InvalidOperationException($"User {publicId} not found.");

        var oldSlug = user.Slug;

        if (oldSlug is not null)
        {
            // Reclaim: user is setting a slug they previously held — delete the history entry
            var reclaimEntry = await context.UserSlugHistory
                .AsTracking()
                .FirstOrDefaultAsync(h => h.UserId == user.Id && h.OldSlug == newSlug, ct);
            if (reclaimEntry is not null)
                context.UserSlugHistory.Remove(reclaimEntry);

            // Archive the current slug
            context.UserSlugHistory.Add(new UserSlugHistoryDatabaseEntity
            {
                UserId = user.Id,
                OldSlug = oldSlug,
                SupersededAt = DateTime.UtcNow
            });
        }

        user.Slug = newSlug;
        user.IsSlugCustomized = isCustomized;
        user.SlugChangeCount++;
        user.SlugLastChangedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }

    public async Task AssignInitialSlugAsync(string publicId, string displayName, CancellationToken ct = default)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == publicId, ct)
            ?? throw new InvalidOperationException($"User {publicId} not found.");

        if (user.Slug is not null) return; // Already has a slug

        var baseSlug = SlugHelper.ToSlug(displayName);
        if (baseSlug.Length < 3)
            baseSlug = "user" + (baseSlug.Length > 0 ? baseSlug : publicId[..Math.Min(6, publicId.Length)].ToLowerInvariant());
        if (baseSlug.Length > 30)
            baseSlug = baseSlug[..30].TrimEnd('-');

        var slug = await EnsureUniqueSlugAsync(baseSlug, user.Id, ct);
        user.Slug = slug;
        user.IsSlugCustomized = false;
        await context.SaveChangesAsync(ct);
    }

    public async Task AutoUpdateSlugFromDisplayNameAsync(string publicId, string newDisplayName, CancellationToken ct = default)
    {
        var user = await context.Users.AsTracking().FirstOrDefaultAsync(u => u.PublicId == publicId, ct)
            ?? throw new InvalidOperationException($"User {publicId} not found.");

        if (user.IsSlugCustomized) return; // Never auto-update a custom slug

        var baseSlug = SlugHelper.ToSlug(newDisplayName);
        if (baseSlug.Length < 3)
            return; // Display name produced no usable slug — keep current

        if (baseSlug.Length > 30)
            baseSlug = baseSlug[..30].TrimEnd('-');

        var oldSlug = user.Slug;
        var newSlug = await EnsureUniqueSlugAsync(baseSlug, user.Id, ct);

        if (oldSlug == newSlug) return; // No change

        if (oldSlug is not null)
        {
            context.UserSlugHistory.Add(new UserSlugHistoryDatabaseEntity
            {
                UserId = user.Id,
                OldSlug = oldSlug,
                SupersededAt = DateTime.UtcNow
            });
        }

        user.Slug = newSlug;
        await context.SaveChangesAsync(ct);
    }

    private async Task<bool> IsSlugTakenAsync(string slug, int? excludeInternalUserId, CancellationToken ct)
    {
        var query = context.Users.Where(u => u.Slug == slug);
        if (excludeInternalUserId.HasValue)
            query = query.Where(u => u.Id != excludeInternalUserId.Value);

        if (await query.AnyAsync(ct))
            return true;

        var historyQuery = context.UserSlugHistory.Where(h => h.OldSlug == slug);
        if (excludeInternalUserId.HasValue)
            historyQuery = historyQuery.Where(h => h.UserId != excludeInternalUserId.Value);

        return await historyQuery.AnyAsync(ct);
    }
}
