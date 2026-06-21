namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

public class DiscussionRepositoryAdapter(
    Infrastructure.Database.Repositories.IDiscussionRepository databaseRepository,
    SnakkDbContext context,
    IUserGrantsCacheService grantsCache,
    HybridCache cache) : Domain.Repositories.IDiscussionRepository
{
    private static readonly HybridCacheEntryOptions _spaceDisplayCacheOptions = new()
    {
        Expiration = TimeSpan.FromDays(365),
        LocalCacheExpiration = TimeSpan.FromDays(365),
    };

    public async Task<Discussion?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var projection = await context.Discussions
            .Where(d => d.Id == id)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.SpacePublicId!, d.CreatedByUserPublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Discussion?> GetByPublicIdAsync(DiscussionId publicId, CancellationToken ct = default)
    {
        var projection = await context.Discussions
            .Where(d => d.PublicId == publicId.Value)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.SpacePublicId!, d.CreatedByUserPublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized, d.PostCount))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Discussion?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var projection = await context.Discussions
            .Where(d => d.Slug == slug)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.SpacePublicId!, d.CreatedByUserPublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<PagedResult<Discussion>> GetBySpaceIdAsync(
        SpaceId spaceId,
        int offset,
        int pageSize,
        CancellationToken ct = default) =>
        await GetPagedBySpaceIdAsync(spaceId, offset, pageSize, ct);

    public async Task<PagedResult<Discussion>> GetPagedBySpaceIdAsync(
        SpaceId spaceId,
        int offset,
        int pageSize,
        CancellationToken ct = default)
    {
        // Get internal ID from PublicId
        var space = await context.Spaces
            .FirstOrDefaultAsync(s => s.PublicId == spaceId.Value, ct);

        if (space is null)
            return new PagedResult<Discussion>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        // Use the database repository's DTO-based method
        var result = await databaseRepository.GetPagedBySpaceIdAsync(space.Id, offset, pageSize, ct: ct);

        return new PagedResult<Discussion>
        {
            Items = result.Items
                .Select(dto => Discussion.RehydrateForList(
                    DiscussionId.From(dto.PublicId),
                    SpaceId.From(spaceId.Value),
                    UserId.FromNullable(dto.CreatedByUserPublicId),
                    dto.Title,
                    dto.Slug,
                    (DiscussionTypeEnum)dto.Type,
                    dto.CreatedAt,
                    dto.LastActivityAt,
                    dto.IsPinned,
                    dto.IsLocked))
                .ToList(),
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };
    }

    public async Task<IEnumerable<Discussion>> GetRecentAsync(int count = 10, CancellationToken ct = default)
    {
        var projections = await context.Discussions
            .OrderByDescending(d => d.LastActivityAt)
            .Take(count)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.SpacePublicId!, d.CreatedByUserPublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .ToListAsync(ct);

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IReadOnlyList<Domain.Repositories.DiscussionSummary>> GetSummariesByPublicIdsAsync(
        IEnumerable<DiscussionId> publicIds,
        CancellationToken ct = default)
    {
        var ids = publicIds.Select(p => p.Value).ToList();
        return await context.Discussions
            .Where(d => ids.Contains(d.PublicId)
                && !d.IsDeleted
                && !d.Space.IsRestricted
                && !d.Space.Hub.IsRestricted
                && !d.Space.Hub.Community.IsRestricted)
            .Select(d => new Domain.Repositories.DiscussionSummary(
                d.PublicId, d.Title, d.Slug,
                d.SpacePublicId!,
                d.CreatedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.Type, d.PostCount))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Discussion discussion, CancellationToken ct = default)
    {
        // Convert to database entity
        var entity = discussion.ToPersistence();

        // Resolve foreign keys from PublicIds
        var space = await context.Spaces
            .Where(s => s.PublicId == discussion.SpaceId.Value)
            .Select(s => new { s.Id, s.PublicId, s.HubId, s.HubPublicId, s.CommunityPublicId, CommunityId = s.Hub.CommunityId })
            .FirstOrDefaultAsync(ct);

        if (space is null)
            throw new InvalidOperationException($"Space with PublicId '{discussion.SpaceId}' not found");

        if (discussion.CreatedByUserId is null)
            throw new InvalidOperationException("Cannot save a discussion with no author");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == discussion.CreatedByUserId.Value, ct);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{discussion.CreatedByUserId}' not found");

        entity.SpaceId = space.Id;
        entity.SpacePublicId = space.PublicId;
        entity.HubId = space.HubId;
        entity.HubPublicId = space.HubPublicId;
        entity.CommunityId = space.CommunityId;
        entity.CommunityPublicId = space.CommunityPublicId;
        entity.CreatedByUserId = user.Id;
        entity.CreatedByUserPublicId = user.PublicId;
        entity.AuthorDisplayName = user.DisplayName;
        entity.AuthorAvatarFileName = user.AvatarFileName;
        entity.AuthorAvatarThumbnailFileName = user.AvatarThumbnailFileName;

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Discussion discussion, CancellationToken ct = default)
    {
        // Fetch existing entity
        var entity = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussion.PublicId.Value, ct);

        if (entity is null)
            throw new InvalidOperationException($"Discussion with PublicId '{discussion.PublicId}' not found");

        // Update properties
        entity.Title = discussion.Title;
        entity.Slug = discussion.Slug;
        entity.Type = (int)discussion.Type;
        entity.LastModifiedAt = discussion.LastModifiedAt;
        entity.LastActivityAt = discussion.LastActivityAt;
        entity.IsPinned = discussion.IsPinned;
        entity.IsLocked = discussion.IsLocked;

        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task<List<Domain.Repositories.TopActiveDiscussion>> GetTopActiveDiscussionsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        string? userId = null,
        bool viewerAllowsAdult = false,
        CancellationToken ct = default)
    {
        var postsQuery = context.Posts
            .Where(p => p.CreatedAt >= since);

        if (!viewerAllowsAdult)
        {
            var adultSpaceIds = await grantsCache.GetAdultHidingSpaceIdsAsync();
            if (adultSpaceIds.Count > 0)
                postsQuery = postsQuery.Where(p =>
                    !(p.Discussion.IsAdultOnly && adultSpaceIds.Contains(p.Discussion.SpaceId)));
        }

        // Resolve public IDs to internal IDs once, outside the query
        if (communityId is not null)
        {
            var dbId = await context.Communities.Where(c => c.PublicId == communityId.Value).Select(c => c.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.CommunityId == dbId);
        }

        if (hubId is not null)
        {
            var dbId = await context.Hubs.Where(h => h.PublicId == hubId.Value).Select(h => h.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.HubId == dbId);
        }

        if (spaceId is not null)
        {
            var dbId = await context.Spaces.Where(s => s.PublicId == spaceId.Value).Select(s => s.Id).FirstOrDefaultAsync(ct);
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.SpaceId == dbId);
        }

        // Group access filter — only show posts in discussions the user can read
        if (!await grantsCache.AnyRestrictedAsync())
        {
            // Fast path: nothing in the platform is restricted, skip filter entirely
        }
        else if (userId == null)
        {
            postsQuery = postsQuery.Where(p =>
                !p.Discussion.Space.IsRestricted &&
                !p.Discussion.Space.Hub.IsRestricted &&
                !p.Discussion.Space.Hub.Community.IsRestricted);
        }
        else
        {
            var grants = await grantsCache.GetGrantsAsync(userId);
            var spaceIds = grants.SpaceIds;
            var hubIds = grants.HubIds;
            var communityIds = grants.CommunityIds;

            postsQuery = postsQuery.Where(p =>
                (!p.Discussion.Space.IsRestricted || spaceIds.Contains(p.Discussion.SpaceId))
                && (!p.Discussion.Space.Hub.IsRestricted || hubIds.Contains(p.Discussion.Space.HubId))
                && (!p.Discussion.Space.Hub.Community.IsRestricted || communityIds.Contains(p.Discussion.Space.Hub.CommunityId)));
        }

        var topDiscussions = await postsQuery
            .GroupBy(p => new {
                p.Discussion.PublicId,
                p.Discussion.Title,
                p.Discussion.Slug,
                AuthorPublicId = p.Discussion.CreatedByUserPublicId ?? "",
                AuthorDisplayName = p.Discussion.AuthorDisplayName ?? "",
                AuthorAvatarFileName = p.Discussion.AuthorAvatarFileName,
                SpaceId = p.Discussion.SpaceId,
                SpacePublicId = p.SpacePublicId,
                HubPublicId = p.HubPublicId })
            .Select(g => new {
                g.Key.PublicId,
                g.Key.Title,
                g.Key.Slug,
                PostCount = g.Count(),
                g.Key.SpaceId,
                g.Key.SpacePublicId,
                g.Key.HubPublicId,
                g.Key.AuthorPublicId,
                g.Key.AuthorDisplayName,
                g.Key.AuthorAvatarFileName })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync(ct);

        var spaceDisplay = await FetchSpaceDisplayAsync(topDiscussions.Select(x => x.SpaceId), ct);

        return topDiscussions
            .Select(d =>
            {
                spaceDisplay.TryGetValue(d.SpaceId, out var space);
                return new Domain.Repositories.TopActiveDiscussion(
                    DiscussionId.From(d.PublicId),
                    d.Title,
                    d.Slug,
                    d.PostCount,
                    d.SpacePublicId!,
                    space?.Slug ?? "",
                    space?.Name ?? "",
                    d.HubPublicId!,
                    space?.HubSlug ?? "",
                    space?.HubName ?? "",
                    d.AuthorPublicId,
                    d.AuthorDisplayName,
                    space?.CommunitySlug ?? "",
                    d.AuthorAvatarFileName);
            })
            .ToList();
    }

    public async Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(
        UserId userId,
        DateTime startDate,
        CancellationToken ct = default)
    {
        // Get the internal user ID
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userDbId == 0)
            return [];

        var activity = await context.Discussions
            .Where(d =>
                d.CreatedByUserId == userDbId
                && d.CreatedAt >= startDate)
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new {
                Date = g.Key,
                Count = g.Count() })
            .ToListAsync(ct);

        return activity.Select(a => (a.Date, a.Count));
    }

    public async Task<List<Domain.Repositories.TopDiscussionByUser>> GetTopDiscussionsByUserAsync(UserId userId, int limit, CancellationToken ct = default)
    {
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userDbId == 0)
            return [];

        return await context.Discussions
            .Where(d => d.CreatedByUserId == userDbId)
            .OrderByDescending(d => d.EngagementScore)
            .ThenByDescending(d => d.CreatedAt)
            .Take(limit)
            .Select(d => new Domain.Repositories.TopDiscussionByUser(
                d.PublicId,
                d.Title,
                d.Slug,
                d.PostCount,
                d.ReactionCount,
                d.CreatedAt,
                d.Space.Slug,
                d.Space.Name,
                d.Space.HubSlug!,
                d.Space.HubName!,
                d.Space.CommunitySlug!,
                d.Space.CommunityName!))
            .ToListAsync(ct);
    }

    public async Task SetLastPostAsync(
        DiscussionId discussionId,
        string? authorPublicId, string? displayName,
        string? avatarFile, string? thumbFile, string? excerpt,
        CancellationToken ct = default)
    {
        await context.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastPostAuthorPublicId, authorPublicId)
                .SetProperty(d => d.LastPostAuthorDisplayName, displayName)
                .SetProperty(d => d.LastPostAuthorAvatarFileName, avatarFile)
                .SetProperty(d => d.LastPostAuthorAvatarThumbnailFileName, thumbFile)
                .SetProperty(d => d.LastPostPlainTextExcerpt, excerpt), ct);
    }

    public async Task RecalculateLastPostAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        var latest = await context.Posts
            .Where(p => p.Discussion.PublicId == discussionId.Value && !p.IsDeleted && !p.IsFirstPost)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.CreatedByUser.PublicId,
                p.CreatedByUser.DisplayName,
                p.CreatedByUser.AvatarFileName,
                p.CreatedByUser.AvatarThumbnailFileName,
                p.PlainTextExcerpt
            })
            .FirstOrDefaultAsync(ct);

        await context.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastPostAuthorPublicId, latest == null ? null : latest.PublicId)
                .SetProperty(d => d.LastPostAuthorDisplayName, latest == null ? null : latest.DisplayName)
                .SetProperty(d => d.LastPostAuthorAvatarFileName, latest == null ? null : latest.AvatarFileName)
                .SetProperty(d => d.LastPostAuthorAvatarThumbnailFileName, latest == null ? null : latest.AvatarThumbnailFileName)
                .SetProperty(d => d.LastPostPlainTextExcerpt, latest == null ? null : latest.PlainTextExcerpt), ct);
    }

    private sealed record SpaceDisplay(
        string Slug, string Name,
        string? Description,
        string? AvatarFileName, string? AvatarThumbnailFileName,
        string? HubSlug, string? HubName,
        string? CommunitySlug, string? CommunityName);

    private async Task<Dictionary<int, SpaceDisplay>> FetchSpaceDisplayAsync(IEnumerable<int> spaceIds, CancellationToken ct = default)
    {
        var ids = spaceIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var result = new Dictionary<int, SpaceDisplay>(ids.Count);
        foreach (var id in ids)
        {
            var display = await cache.GetOrCreateAsync<SpaceDisplay?>(
                $"space-display:{id}",
                ct2 => FetchSingleSpaceDisplayAsync(id, ct2),
                _spaceDisplayCacheOptions, cancellationToken: ct);
            if (display is not null) result[id] = display;
        }
        return result;
    }

    private async ValueTask<SpaceDisplay?> FetchSingleSpaceDisplayAsync(int spaceId, CancellationToken ct) =>
        await context.Spaces
            .Where(s => s.Id == spaceId)
            .Select(s => new SpaceDisplay(s.Slug, s.Name, s.Description, s.AvatarFileName, s.AvatarThumbnailFileName, s.HubSlug, s.HubName, s.CommunitySlug, s.CommunityName))
            .FirstOrDefaultAsync(ct);

    private record DiscussionProjection(
        string PublicId,
        string SpacePublicId,
        string? CreatedByUserPublicId,
        string Title,
        string Slug,
        int Type,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        DateTime? LastActivityAt,
        bool IsPinned,
        bool IsLocked,
        bool IsAdult,
        bool WasNormalized,
        int PostCount = 0)
    {
        public Discussion ToDomain() => Discussion.Rehydrate(
            DiscussionId.From(PublicId),
            SpaceId.From(SpacePublicId),
            UserId.FromNullable(CreatedByUserPublicId),
            Title, Slug, (DiscussionTypeEnum)Type,
            CreatedAt, LastModifiedAt, LastActivityAt,
            IsPinned, IsLocked, posts: [], isAdult: IsAdult, wasNormalized: WasNormalized,
            postCount: PostCount);
    }
}
