namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
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
    IUserGrantsCacheService grantsCache) : Domain.Repositories.IDiscussionRepository
{
    public async Task<Discussion?> GetByIdAsync(int id)
    {
        var projection = await context.Discussions
            .Where(d => d.Id == id)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.Space.PublicId, d.CreatedByUser.PublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Discussion?> GetByPublicIdAsync(DiscussionId publicId)
    {
        var projection = await context.Discussions
            .Where(d => d.PublicId == publicId.Value)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.Space.PublicId, d.CreatedByUser.PublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Discussion?> GetBySlugAsync(string slug)
    {
        var projection = await context.Discussions
            .Where(d => d.Slug == slug)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.Space.PublicId, d.CreatedByUser.PublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<Discussion>> GetBySpaceIdAsync(SpaceId spaceId)
    {
        var spaceDbId = await context.Spaces
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (spaceDbId is null) return [];

        var projections = await context.Discussions
            .Where(d => d.SpaceId == spaceDbId.Value)
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastActivityAt)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.Space.PublicId, d.CreatedByUser.PublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task<PagedResult<Discussion>> GetBySpaceIdAsync(
        SpaceId spaceId,
        int offset,
        int pageSize) =>
        await GetPagedBySpaceIdAsync(spaceId, offset, pageSize);

    public async Task<PagedResult<Discussion>> GetPagedBySpaceIdAsync(
        SpaceId spaceId,
        int offset,
        int pageSize)
    {
        // Get internal ID from PublicId
        var space = await context.Spaces
            .FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);

        if (space is null)
            return new PagedResult<Discussion>
            {
                Items = [],
                Offset = offset,
                PageSize = pageSize,
                HasMoreItems = false
            };

        // Use the database repository's DTO-based method
        var result = await databaseRepository.GetPagedBySpaceIdAsync(space.Id, offset, pageSize);

        return new PagedResult<Discussion>
        {
            Items = result.Items
                .Select(dto => Discussion.RehydrateForList(
                    DiscussionId.From(dto.PublicId),
                    SpaceId.From(spaceId.Value),
                    UserId.From(dto.CreatedByUserPublicId),
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

    public async Task<IEnumerable<Discussion>> GetRecentAsync(int count = 10)
    {
        var projections = await context.Discussions
            .OrderByDescending(d => d.LastActivityAt)
            .Take(count)
            .Select(d => new DiscussionProjection(
                d.PublicId, d.Space.PublicId, d.CreatedByUser.PublicId,
                d.Title, d.Slug, d.Type, d.CreatedAt, d.LastModifiedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.IsAdultOnly, d.WasNormalized))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IReadOnlyList<Domain.Repositories.DiscussionSummary>> GetSummariesByPublicIdsAsync(
        IEnumerable<DiscussionId> publicIds)
    {
        var ids = publicIds.Select(p => p.Value).ToList();
        return await context.Discussions
            .Where(d => ids.Contains(d.PublicId))
            .Select(d => new Domain.Repositories.DiscussionSummary(
                d.PublicId, d.Title, d.Slug,
                d.Space.PublicId,
                d.CreatedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked, d.Type, d.PostCount))
            .ToListAsync();
    }

    public async Task AddAsync(Discussion discussion)
    {
        // Convert to database entity
        var entity = discussion.ToPersistence();

        // Resolve foreign keys from PublicIds
        var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == discussion.SpaceId.Value);

        if (space is null)
            throw new InvalidOperationException($"Space with PublicId '{discussion.SpaceId}' not found");

        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == discussion.CreatedByUserId.Value);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{discussion.CreatedByUserId}' not found");

        entity.SpaceId = space.Id;
        entity.CreatedByUserId = user.Id;

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Discussion discussion)
    {
        // Fetch existing entity
        var entity = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussion.PublicId.Value);

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

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task<List<Domain.Repositories.TopActiveDiscussion>> GetTopActiveDiscussionsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        string? userId = null,
        bool viewerAllowsAdult = false)
    {
        var postsQuery = context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= since);

        if (!viewerAllowsAdult)
        {
            postsQuery = postsQuery.Where(p =>
                !(p.Discussion.IsAdultOnly && p.Discussion.Space.Hub.Community.HideAdultDiscussionsFromLists));
        }

        // Resolve public IDs to internal IDs once, outside the query
        if (communityId is not null)
        {
            var dbId = await context.Communities.Where(c => c.PublicId == communityId.Value).Select(c => c.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.Space.Hub.CommunityId == dbId);
        }

        if (hubId is not null)
        {
            var dbId = await context.Hubs.Where(h => h.PublicId == hubId.Value).Select(h => h.Id).FirstOrDefaultAsync();
            if (dbId == 0) return [];
            postsQuery = postsQuery.Where(p => p.Discussion.Space.HubId == dbId);
        }

        if (spaceId is not null)
        {
            var dbId = await context.Spaces.Where(s => s.PublicId == spaceId.Value).Select(s => s.Id).FirstOrDefaultAsync();
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
                AuthorPublicId = p.Discussion.CreatedByUser != null ? p.Discussion.CreatedByUser.PublicId : "",
                AuthorDisplayName = p.Discussion.CreatedByUser != null ? p.Discussion.CreatedByUser.DisplayName ?? "" : "",
                AuthorAvatarFileName = p.Discussion.CreatedByUser != null ? p.Discussion.CreatedByUser.AvatarFileName : (string?)null,
                p.Discussion.Space.Hub.CommunityId,

                SpacePublicId = p.Discussion.Space.PublicId,
                SpaceSlug = p.Discussion.Space.Slug,
                SpaceName = p.Discussion.Space.Name,
                HubPublicId = p.Discussion.Space.Hub.PublicId,
                HubSlug = p.Discussion.Space.Hub.Slug,
                HubName = p.Discussion.Space.Hub.Name,
                CommunitySlug = p.Discussion.Space.Hub.Community.Slug })
            .Select(g => new {
                g.Key.PublicId,
                g.Key.Title,
                g.Key.Slug,
                PostCount = g.Count(),
                g.Key.SpacePublicId,
                g.Key.SpaceSlug,
                g.Key.SpaceName,
                g.Key.HubPublicId,
                g.Key.HubSlug,
                g.Key.HubName,
                g.Key.AuthorPublicId,
                g.Key.AuthorDisplayName,
                g.Key.AuthorAvatarFileName,
                g.Key.CommunitySlug })
            .OrderByDescending(x => x.PostCount)
            .Take(limit)
            .ToListAsync();

        return topDiscussions
            .Select(d => new Domain.Repositories.TopActiveDiscussion(
                DiscussionId.From(d.PublicId),
                d.Title,
                d.Slug,
                d.PostCount,
                d.SpacePublicId,
                d.SpaceSlug,
                d.SpaceName,
                d.HubPublicId,
                d.HubSlug,
                d.HubName,
                d.AuthorPublicId,
                d.AuthorDisplayName,
                d.CommunitySlug,
                d.AuthorAvatarFileName))
            .ToList();
    }

    public async Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(
        UserId userId,
        DateTime startDate)
    {
        // Get the internal user ID
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

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
            .ToListAsync();

        return activity.Select(a => (a.Date, a.Count));
    }

    public async Task<List<Domain.Repositories.TopDiscussionByUser>> GetTopDiscussionsByUserAsync(UserId userId, int limit)
    {
        var userDbId = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (userDbId == 0)
            return [];

        return await context.Discussions
            .Where(d => d.CreatedByUserId == userDbId && !d.IsDeleted)
            .OrderByDescending(d => d.ReactionCount + d.PostCount)
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
                d.Space.Hub.Slug,
                d.Space.Hub.Name,
                d.Space.Hub.Community.Slug,
                d.Space.Hub.Community.Name))
            .ToListAsync();
    }

    private record DiscussionProjection(
        string PublicId,
        string SpacePublicId,
        string CreatedByUserPublicId,
        string Title,
        string Slug,
        int Type,
        DateTime CreatedAt,
        DateTime? LastModifiedAt,
        DateTime? LastActivityAt,
        bool IsPinned,
        bool IsLocked,
        bool IsAdult,
        bool WasNormalized)
    {
        public Discussion ToDomain() => Discussion.Rehydrate(
            DiscussionId.From(PublicId),
            SpaceId.From(SpacePublicId),
            UserId.From(CreatedByUserPublicId),
            Title, Slug, (DiscussionTypeEnum)Type,
            CreatedAt, LastModifiedAt, LastActivityAt,
            IsPinned, IsLocked, posts: [], isAdult: IsAdult, wasNormalized: WasNormalized);
    }
}
