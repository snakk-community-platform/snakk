namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

public class SpaceDataService(
    SnakkDbContext dbContext,
    HybridCache cache) : ISpaceDataService
{
    // Keep the cache key byte-identical to the key used in the original GrpcService
    // so that any existing write-path RemoveAsync("space-meta:{publicId}") calls
    // continue to invalidate entries written by this service.
    private static readonly HybridCacheEntryOptions MetaCacheOptions =
        new() { Expiration = TimeSpan.FromMinutes(5) };

    public async Task<string?> GetDiscordInviteUrlAsync(string publicId, CancellationToken ct = default)
    {
        return await dbContext.Spaces
            .Where(s => s.PublicId == publicId && s.DiscordInviteUrl != null)
            .Select(s => s.DiscordInviteUrl)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SpaceMetaDto?> GetSpaceMetaAsync(string publicId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<SpaceMetaDto?>(
            $"space-meta:{publicId}",
            async cancel =>
            {
                var raw = await dbContext.Spaces
                    .Where(s => s.PublicId == publicId)
                    .Select(s => new
                    {
                        s.Id,
                        s.HasRules,
                        s.RulesRevision,
                        s.ParentHubHasRules,
                        s.ParentCommunityHasRules,
                        s.TeamRevision,
                        s.IsRestricted,
                        s.Require2FA,
                        s.AllowAnonymousReading,
                        AllowedTypes = s.AllowedDiscussionTypes.Select(a => a.DiscussionType).ToList(),
                        HubSlug = s.Hub.Slug,
                        CommunitySlug = s.Hub.Community.Slug,
                        s.DiscussionCount,
                        ReplyCount = s.PostCount - s.DiscussionCount,
                    })
                    .FirstOrDefaultAsync(cancel);

                if (raw is null) return null;

                // Separate query avoids the ROW_NUMBER() window-function scan EF Core generates
                // when FirstOrDefault() is used on a navigation collection inside a projection.
                var latestRaw = await dbContext.Discussions
                    .Where(d => d.SpaceId == raw.Id && !d.IsDeleted)
                    .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
                    .Select(d => new
                    {
                        d.PublicId,
                        d.Title,
                        d.Slug,
                        LastActivityAt = d.LastActivityAt ?? d.CreatedAt,
                        AuthorPublicId = d.CreatedByUser.PublicId,
                        AuthorDisplayName = d.CreatedByUser.DisplayName,
                        AuthorAvatarFileName = d.CreatedByUser.AvatarFileName,
                        d.PostCount
                    })
                    .FirstOrDefaultAsync(cancel);

                var ld = latestRaw is null
                    ? null
                    : new SpaceLatestDiscussionDto(
                        latestRaw.PublicId,
                        latestRaw.Title,
                        latestRaw.Slug,
                        latestRaw.LastActivityAt,
                        latestRaw.AuthorPublicId,
                        latestRaw.AuthorDisplayName ?? "",
                        latestRaw.AuthorAvatarFileName,
                        latestRaw.PostCount);

                return new SpaceMetaDto(
                    raw.HasRules,
                    raw.RulesRevision,
                    raw.ParentHubHasRules,
                    raw.ParentCommunityHasRules,
                    raw.TeamRevision,
                    raw.IsRestricted,
                    raw.AllowedTypes,
                    raw.HubSlug,
                    raw.CommunitySlug,
                    raw.DiscussionCount,
                    raw.ReplyCount,
                    ld,
                    raw.Require2FA,
                    raw.AllowAnonymousReading);
            },
            MetaCacheOptions,
            cancellationToken: ct);
    }
}
