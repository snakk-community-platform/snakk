namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Models;

public class SearchRepository(SnakkDbContext context, IUserGrantsCacheService grantsCache, IFileStorage fileStorage) : ISearchRepository
{
    private readonly SnakkDbContext _context = context;

    private bool IsPostgres => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Escapes LIKE/ILIKE metacharacters so user input is treated as literal text.</summary>
    private static string EscapeLikePattern(string input) => input
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    public async Task<PagedResult<DiscussionSearchResultDto>> SearchDiscussionsAsync(
        string query,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20,
        string? userId = null)
    {
        var baseQuery = _context.Discussions
            .Where(d => !d.IsDeleted);

        baseQuery = await WithAccessFilterAsync(baseQuery, userId);

        // Full-text search: PostgreSQL uses tsvector + websearch_to_tsquery, others fall back to LIKE
        if (!string.IsNullOrWhiteSpace(query))
        {
            if (IsPostgres)
            {
                var tsQuery = EF.Functions.WebSearchToTsQuery("english", query.Trim());
                baseQuery = baseQuery.Where(d => d.SearchVector.Matches(tsQuery));
            }
            else
            {
                var pattern = $"%{EscapeLikePattern(query.Trim())}%";
                baseQuery = baseQuery.Where(d => EF.Functions.Like(d.Title, pattern));
            }
        }

        // Apply filters
        if (!string.IsNullOrEmpty(authorPublicId))
            baseQuery = baseQuery.Where(d => d.CreatedByUser.PublicId == authorPublicId);

        if (!string.IsNullOrEmpty(spacePublicId))
            baseQuery = baseQuery.Where(d => d.Space.PublicId == spacePublicId);

        if (!string.IsNullOrEmpty(hubPublicId))
            baseQuery = baseQuery.Where(d => d.Space.Hub.PublicId == hubPublicId);

        // Order by relevance when searching, by activity when browsing
        IOrderedQueryable<Database.Entities.DiscussionDatabaseEntity> orderedQuery;
        if (!string.IsNullOrWhiteSpace(query) && IsPostgres)
        {
            orderedQuery = baseQuery
                .OrderByDescending(d => d.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", query.Trim())))
                .ThenByDescending(d => d.LastActivityAt ?? d.CreatedAt);
        }
        else
        {
            orderedQuery = baseQuery
                .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt);
        }

        var items = await orderedQuery
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(d => new DiscussionSearchResultDto(
                d.PublicId,
                d.Title,
                d.Slug,
                d.CreatedByUser.PublicId,
                d.CreatedByUser.DisplayName ?? "",
                d.CreatedByUser.AvatarFileName,
                d.Space.PublicId,
                d.Space.Name,
                d.Space.Slug,
                d.Space.Hub.Slug,
                d.Space.Hub.Name,
                d.Space.Hub.Community.Slug,
                d.CreatedAt,
                d.LastActivityAt,
                d.PostCount,
                d.ReactionCount,
                0))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize) : items;

        return new PagedResult<DiscussionSearchResultDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<PostSearchResultDto>> SearchPostsAsync(
        string query,
        string? authorPublicId = null,
        string? discussionPublicId = null,
        string? spacePublicId = null,
        int offset = 0,
        int pageSize = 20,
        string? userId = null)
    {
        var baseQuery = _context.Posts
            .Where(p => !p.IsDeleted);

        baseQuery = await WithPostAccessFilterAsync(baseQuery, userId);

        // Full-text search: PostgreSQL uses tsvector + websearch_to_tsquery, others fall back to LIKE
        if (!string.IsNullOrWhiteSpace(query))
        {
            if (IsPostgres)
            {
                var tsQuery = EF.Functions.WebSearchToTsQuery("english", query.Trim());
                baseQuery = baseQuery.Where(p => p.SearchVector.Matches(tsQuery));
            }
            else
            {
                var pattern = $"%{EscapeLikePattern(query.Trim())}%";
                baseQuery = baseQuery.Where(p => EF.Functions.Like(p.Content, pattern));
            }
        }

        // Apply filters
        if (!string.IsNullOrEmpty(authorPublicId))
            baseQuery = baseQuery.Where(p => p.CreatedByUser.PublicId == authorPublicId);

        if (!string.IsNullOrEmpty(discussionPublicId))
            baseQuery = baseQuery.Where(p => p.Discussion.PublicId == discussionPublicId);

        if (!string.IsNullOrEmpty(spacePublicId))
            baseQuery = baseQuery.Where(p => p.Discussion.Space.PublicId == spacePublicId);

        // Order by relevance when searching, by date when browsing
        IOrderedQueryable<Database.Entities.PostDatabaseEntity> orderedQuery;
        if (!string.IsNullOrWhiteSpace(query) && IsPostgres)
        {
            orderedQuery = baseQuery
                .OrderByDescending(p => p.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", query.Trim())))
                .ThenByDescending(p => p.CreatedAt);
        }
        else
        {
            orderedQuery = baseQuery
                .OrderByDescending(p => p.CreatedAt);
        }

        var items = await orderedQuery
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(p => new PostSearchResultDto(
                p.PublicId,
                p.Content.Length > 200 ? p.Content.Substring(0, 200) + "..." : p.Content,
                p.CreatedByUser.PublicId,
                p.CreatedByUser.DisplayName ?? "",
                p.CreatedByUser.AvatarFileName,
                p.Discussion.PublicId,
                p.Discussion.Title,
                p.Discussion.Slug,
                p.Discussion.Space.Slug,
                p.Discussion.Space.Name,
                p.Discussion.Space.Hub.Slug,
                p.Discussion.Space.Hub.Name,
                p.Discussion.Space.Hub.Community.Slug,
                p.CreatedAt))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize) : items;

        return new PagedResult<PostSearchResultDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<int> GetDiscussionCountByAuthorAsync(string authorPublicId) => await _context.Users
        .Where(u => u.PublicId == authorPublicId)
        .Select(u => u.DiscussionCount)
        .FirstOrDefaultAsync();

    public async Task<int> GetPostCountByAuthorAsync(string authorPublicId) =>
        // ReplyCount = non-first posts. Add DiscussionCount to get total posts (each discussion has a first post).
        await _context.Users
            .Where(u => u.PublicId == authorPublicId)
            .Select(u => u.ReplyCount + u.DiscussionCount)
            .FirstOrDefaultAsync();

    public async Task<int> GetDiscussionPostCountAsync(string discussionPublicId) =>
        await _context.Discussions
            .Where(d => d.PublicId == discussionPublicId)
            .Select(d => d.PostCount)
            .FirstOrDefaultAsync();

    public async Task<PagedResult<DiscussionListItemDto>> GetDiscussionsBySpaceAsync(
        string spacePublicId,
        int offset = 0,
        int pageSize = 20,
        int? typeFilter = null,
        string? userId = null,
        string? cursor = null)
    {
        var baseQuery = _context.Discussions
            .Where(d => d.Space.PublicId == spacePublicId && !d.IsDeleted);

        baseQuery = await WithAccessFilterAsync(baseQuery, userId);

        if (typeFilter.HasValue)
            baseQuery = baseQuery.Where(d => d.Type == typeFilter.Value);

        // Apply keyset pagination if cursor provided.
        // The sort order is IsPinned DESC, LastActivityAt DESC.
        // Pinned items only appear on the first page (no cursor), so when a
        // cursor is present we filter them out and paginate on (LastActivityAt, Id).
        var cursorData = Cursor.Decode(cursor);

        if (cursorData.HasValue)
        {
            var (cursorDate, cursorId) = cursorData.Value;

            // Exclude pinned items — they were already returned on the first page
            baseQuery = baseQuery.Where(d => !d.IsPinned);

            // Keyset WHERE clause for ORDER BY LastActivityAt DESC, Id DESC
            baseQuery = baseQuery.Where(d =>
                d.LastActivityAt < cursorDate
                || (d.LastActivityAt == cursorDate && d.Id < cursorId));
        }

        var orderedQuery = baseQuery
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastActivityAt)
            .ThenByDescending(d => d.Id);

        // Only apply offset-based skip when no cursor is provided;
        // the cursor's WHERE clause already positions the query.
        if (!cursorData.HasValue)
            orderedQuery = (IOrderedQueryable<Database.Entities.DiscussionDatabaseEntity>)orderedQuery.Skip(offset);

        var items = await orderedQuery
            .Take(pageSize + 1)
            .Select(d => new
            {
                d.Id,
                Dto = new DiscussionListItemDto(
                    d.PublicId,
                    d.Space.PublicId,
                    d.Title,
                    d.Slug,
                    d.Type,
                    d.CreatedAt,
                    d.LastActivityAt,
                    d.IsPinned,
                    d.IsLocked,
                    d.PostCount,
                    d.ReactionCount,
                    d.CreatedByUser.PublicId,
                    d.CreatedByUser.DisplayName ?? "",
                    d.CreatedByUser.AvatarFileName,
                    d.Tags)
            })
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        // Generate next cursor from last item
        string? nextCursor = null;

        if (hasMoreItems && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = Cursor.Encode(lastItem.Dto.LastActivityAt, lastItem.Id);
        }

        return new PagedResult<DiscussionListItemDto>
        {
            Items = resultItems.Select(x => x.Dto),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = nextCursor
        };
    }

    public async Task<PagedResult<HubListItemDto>> GetHubsAsync(
        int offset = 0,
        int pageSize = 20,
        string? userId = null)
    {
        // Use denormalized counts + fetch one extra row to check HasMoreItems
        var query = _context.Hubs.AsQueryable();
        query = await WithHubAccessFilterAsync(query, userId);
        var hubs = await query
            .OrderBy(h => h.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(h => new HubListItemDto(
                h.PublicId,
                h.Community.PublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.CreatedAt,
                h.SpaceCount,
                h.DiscussionCount,
                h.PostCount - h.DiscussionCount))
            .ToListAsync();

        var hasMore = hubs.Count > pageSize;

        return new PagedResult<HubListItemDto>
        {
            Items = hasMore
                ? hubs
                    .Take(pageSize)
                    .ToList()
                : hubs,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }

    public async Task<List<SpaceSearchItemDto>> SearchSpacesAsync(
        string? query = null,
        string? hubPublicId = null,
        string? communityPublicId = null,
        int limit = 10,
        string? userId = null)
    {
        var baseQuery = _context.Spaces.AsQueryable();
        baseQuery = await WithSpaceAccessFilterAsync(baseQuery, userId);

        if (!string.IsNullOrEmpty(hubPublicId))
            baseQuery = baseQuery.Where(s => s.Hub.PublicId == hubPublicId);
        else if (!string.IsNullOrEmpty(communityPublicId))
            baseQuery = baseQuery.Where(s => s.Hub.Community.PublicId == communityPublicId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{EscapeLikePattern(query.Trim())}%";
            baseQuery = baseQuery.Where(s => EF.Functions.ILike(s.Name, pattern));
        }

        var raw = await baseQuery
            .OrderByDescending(s => s.DiscussionCount)
            .ThenBy(s => s.Name)
            .Take(limit)
            .Select(s => new
            {
                s.PublicId,
                s.Name,
                s.Slug,
                HubSlug = s.Hub.Slug,
                HubName = s.Hub.Name,
                CommunitySlug = s.Hub.Community.Slug,
                s.DiscussionCount,
                CommunityName = s.Hub.Community.Name
            })
            .ToListAsync();

        return raw.Select(s => new SpaceSearchItemDto(
            s.PublicId, s.Name, s.Slug, s.HubSlug, s.HubName,
            s.CommunitySlug, s.DiscussionCount, s.CommunityName,
            Snakk.Shared.Helpers.AvatarHelper.GetAvatarUrl(s.PublicId, Snakk.Shared.Helpers.AvatarEntityType.Space, 0)))
            .ToList();
    }

    public async Task<PagedResult<SpaceListItemDto>> GetSpacesByHubAsync(
        string hubPublicId,
        int offset = 0,
        int pageSize = 20,
        string? userId = null)
    {
        // Use denormalized counts + fetch one extra row to check HasMoreItems (avoids separate COUNT query)
        var baseSpaceQuery = _context.Spaces
            .Where(s => s.Hub.PublicId == hubPublicId);
        baseSpaceQuery = await WithSpaceAccessFilterAsync(baseSpaceQuery, userId);
        var spaces = await baseSpaceQuery
            .OrderBy(s => s.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(s => new {
                s.PublicId,
                HubPublicId = s.Hub.PublicId,
                s.Name,
                s.Slug,
                s.Description,
                s.CreatedAt,
                s.DiscussionCount,
                ReplyCount = s.PostCount - s.DiscussionCount,
                LatestDiscussion = s.Discussions
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
                    .Select(d => new {
                        d.PublicId,
                        d.Title,
                        d.Slug,
                        LastActivityAt = d.LastActivityAt ?? d.CreatedAt,
                        AuthorPublicId = d.CreatedByUser.PublicId,
                        AuthorDisplayName = d.CreatedByUser.DisplayName,
                        AuthorAvatarFileName = d.CreatedByUser.AvatarFileName,
                        d.PostCount })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var hasMore = spaces.Count > pageSize;

        var items = spaces
            .Take(pageSize)
            .Select(s => new SpaceListItemDto(
                s.PublicId,
                s.HubPublicId,
                s.Name,
                s.Slug,
                s.Description,
                s.CreatedAt,
                s.DiscussionCount,
                s.ReplyCount,
                s.LatestDiscussion is not null
                    ? new LatestDiscussionDto(
                        s.LatestDiscussion.PublicId,
                        s.LatestDiscussion.Title,
                        s.LatestDiscussion.Slug,
                        s.LatestDiscussion.LastActivityAt,
                        s.LatestDiscussion.AuthorPublicId,
                        s.LatestDiscussion.AuthorDisplayName ?? "",
                        s.LatestDiscussion.AuthorAvatarFileName,
                        s.LatestDiscussion.PostCount)
                    : null))
            .ToList();

        return new PagedResult<SpaceListItemDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }

    private async Task<IQueryable<Database.Entities.HubDatabaseEntity>> WithHubAccessFilterAsync(
        IQueryable<Database.Entities.HubDatabaseEntity> query, string? userId)
    {
        if (!await grantsCache.AnyRestrictedAsync())
            return query;

        if (userId == null)
            return query.Where(h =>
                !h.IsRestricted &&
                !h.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId);
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(h =>
            (!h.IsRestricted || hubIds.Contains(h.Id))
            && (!h.Community.IsRestricted || communityIds.Contains(h.CommunityId)));
    }

    private async Task<IQueryable<Database.Entities.SpaceDatabaseEntity>> WithSpaceAccessFilterAsync(
        IQueryable<Database.Entities.SpaceDatabaseEntity> query, string? userId)
    {
        if (!await grantsCache.AnyRestrictedAsync())
            return query;

        if (userId == null)
            return query.Where(s =>
                !s.IsRestricted &&
                !s.Hub.IsRestricted &&
                !s.Hub.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId);
        var spaceIds = grants.SpaceIds;
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(s =>
            (!s.IsRestricted || spaceIds.Contains(s.Id))
            && (!s.Hub.IsRestricted || hubIds.Contains(s.HubId))
            && (!s.Hub.Community.IsRestricted || communityIds.Contains(s.Hub.CommunityId)));
    }

    /// <summary>
    /// Filters discussions to only those accessible by the given user.
    /// Anonymous (null userId): only unrestricted discussions are returned.
    /// Authenticated: unrestricted discussions plus those where the user holds a CanRead
    /// grant at each restricted level (intersection-gate model).
    /// Grant lookups are resolved via <see cref="IUserGrantsCacheService"/> (5-minute TTL).
    /// </summary>
    private async Task<IQueryable<Database.Entities.PostDatabaseEntity>> WithPostAccessFilterAsync(
        IQueryable<Database.Entities.PostDatabaseEntity> query, string? userId)
    {
        if (!await grantsCache.AnyRestrictedAsync())
            return query;

        if (userId == null)
            return query.Where(p =>
                !p.Discussion.Space.IsRestricted
                && !p.Discussion.Space.Hub.IsRestricted
                && !p.Discussion.Space.Hub.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId);
        var spaceIds = grants.SpaceIds;
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(p =>
            (!p.Discussion.Space.IsRestricted || spaceIds.Contains(p.Discussion.SpaceId))
            && (!p.Discussion.Space.Hub.IsRestricted || hubIds.Contains(p.Discussion.Space.HubId))
            && (!p.Discussion.Space.Hub.Community.IsRestricted || communityIds.Contains(p.Discussion.Space.Hub.CommunityId)));
    }

    private async Task<IQueryable<Database.Entities.DiscussionDatabaseEntity>> WithAccessFilterAsync(
        IQueryable<Database.Entities.DiscussionDatabaseEntity> query, string? userId)
    {
        if (!await grantsCache.AnyRestrictedAsync())
            return query;

        if (userId == null)
            return query.Where(d =>
                !d.Space.IsRestricted &&
                !d.Space.Hub.IsRestricted &&
                !d.Space.Hub.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId);
        var spaceIds = grants.SpaceIds;
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(d =>
            (!d.Space.IsRestricted || spaceIds.Contains(d.SpaceId))
            && (!d.Space.Hub.IsRestricted || hubIds.Contains(d.Space.HubId))
            && (!d.Space.Hub.Community.IsRestricted || communityIds.Contains(d.Space.Hub.CommunityId)));
    }

    public async Task<(List<SitemapDiscussionDto> Items, int TotalCount)> GetSitemapDiscussionsAsync(
        int page,
        int pageSize)
    {
        var query = _context.Discussions.Where(d => !d.IsDeleted);
        query = await WithAccessFilterAsync(query, null);

        var totalCount = await query.CountAsync();

        var discussions = await query
            .OrderByDescending(d => d.LastModifiedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new SitemapDiscussionDto(
                d.PublicId,
                d.Slug,
                d.Space.Hub.Slug,
                d.Space.Slug,
                d.Space.Hub.Community.Slug,
                d.LastModifiedAt ?? d.CreatedAt,
                d.IsPinned))
            .ToListAsync();

        return (discussions, totalCount);
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetRecentDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        string? cursor = null,
        string? userId = null,
        string? authorId = null)
    {
        var query = _context.Discussions.AsQueryable();

        query = await WithAccessFilterAsync(query, userId);

        // Filter by author if specified
        if (!string.IsNullOrEmpty(authorId))
        {
            query = query.Where(d => d.CreatedByUser.PublicId == authorId);
        }

        // Filter by space if specified (most specific)
        if (!string.IsNullOrEmpty(spaceId))
        {
            query = query.Where(d => d.Space.PublicId == spaceId);
        }
        // Filter by hub if specified (more specific than community)
        else if (!string.IsNullOrEmpty(hubId))
        {
            query = query.Where(d => d.Space.Hub.PublicId == hubId);
        }
        // Filter by community if specified
        else if (!string.IsNullOrEmpty(communityId))
        {
            query = query.Where(d => d.Space.Hub.Community.PublicId == communityId);
        }

        // Apply keyset pagination if cursor provided
        var cursorData = Cursor.Decode(cursor);

        if (cursorData.HasValue)
        {
            var (cursorDate, cursorId) = cursorData.Value;
            // ORDER BY LastActivityAt DESC, Id DESC
            // Keyset: WHERE (LastActivityAt < cursorDate) OR (LastActivityAt = cursorDate AND Id < cursorId)
            query = query.Where(d =>
                d.LastActivityAt < cursorDate
                || (d.LastActivityAt == cursorDate && d.Id < cursorId));
        }
        var orderedQuery = query
            .OrderByDescending(d => d.LastActivityAt)
            .ThenByDescending(d => d.Id);

        // Only apply offset-based skip when no cursor is provided;
        // the cursor's WHERE clause already positions the query.
        if (!cursorData.HasValue)
            orderedQuery = (IOrderedQueryable<Database.Entities.DiscussionDatabaseEntity>)orderedQuery.Skip(offset);

        var items = await orderedQuery
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                Dto = new Application.Repositories.RecentDiscussionDto(
                    d.PublicId,
                    d.Title,
                    d.Slug,
                    d.Type,
                    d.CreatedAt,
                    d.LastActivityAt,
                    d.IsPinned,
                    d.IsLocked,
                    d.Space.PublicId,
                    d.Space.Slug,
                    d.Space.Name,
                    d.Space.Hub.PublicId,
                    d.Space.Hub.Slug,
                    d.Space.Hub.Name,
                    d.Space.Hub.Community.PublicId,
                    d.Space.Hub.Community.Slug,
                    d.Space.Hub.Community.Name,
                    d.CreatedByUser.PublicId,
                    d.CreatedByUser.DisplayName ?? "",
                    d.CreatedByUser.AvatarFileName,
                    d.CreatedByUser.AvatarThumbnailFileName,
                    d.PostCount,
                    d.ReactionCount,
                    string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
            })
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        // Batch-fetch preview data for typed discussions (max 4 queries, not N+1)
        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList());

        var finalItems = resultItems.Select(x =>
            previewMap.TryGetValue(x.Dto.PublicId, out var preview)
                ? x.Dto with { Preview = preview }
                : x.Dto).ToList();

        // Generate next cursor from last item
        string? nextCursor = null;

        if (hasMoreItems && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = Cursor.Encode(lastItem.Dto.LastActivityAt, lastItem.Id);
        }

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = finalItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = nextCursor
        };
    }

    private async Task<Dictionary<string, Application.Repositories.DiscussionPreviewDto>> BatchFetchPreviewsAsync(
        List<(int Id, string PublicId, int Type)> discussions)
    {
        var result = new Dictionary<string, Application.Repositories.DiscussionPreviewDto>();

        // Group by type
        var pollIds = discussions.Where(d => d.Type == 2).ToList();
        var linkIds = discussions.Where(d => d.Type == 4).ToList();
        var imagesIds = discussions.Where(d => d.Type == 5).ToList();
        var debateIds = discussions.Where(d => d.Type == 7).ToList();
        var iamaIds = discussions.Where(d => d.Type == 9).ToList();

        // Polls: fetch options with vote counts
        if (pollIds.Count > 0)
        {
            var ids = pollIds.Select(d => d.Id).ToList();
            var pollIdMap = pollIds.ToDictionary(d => d.Id, d => d.PublicId);
            var polls = await _context.DiscussionPolls
                .Where(p => ids.Contains(p.DiscussionId))
                .Select(p => new
                {
                    p.DiscussionId,
                    p.VotesVisible,
                    p.ClosesAt,
                    Options = p.Options
                        .OrderBy(o => o.DisplayOrder)
                        .Select(o => new { o.Text, o.VoteCount })
                        .ToList()
                })
                .ToListAsync();

            foreach (var poll in polls)
            {
                var publicId = pollIdMap[poll.DiscussionId];
                var isSecret = !poll.VotesVisible;
                var isClosed = poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow;
                var hideVotes = isSecret && !isClosed;
                var options = poll.Options
                    .Select(o => new Application.Repositories.PollOptionPreviewDto(o.Text, hideVotes ? 0 : o.VoteCount))
                    .ToList();
                var totalVotes = hideVotes ? 0 : options.Sum(o => o.VoteCount);
                result[publicId] = new(Poll: new(options, totalVotes, IsSecret: isSecret, ClosesAt: poll.ClosesAt));
            }
        }

        // Debates: fetch positions with post counts (batched to avoid N+1)
        if (debateIds.Count > 0)
        {
            var ids = debateIds.Select(d => d.Id).ToList();
            var debateIdMap = debateIds.ToDictionary(d => d.Id, d => d.PublicId);

            var debates = await _context.DiscussionDebates
                .Where(db => ids.Contains(db.DiscussionId))
                .Select(db => new
                {
                    db.DiscussionId,
                    Positions = db.Positions
                        .OrderBy(p => p.Index)
                        .Select(p => new { p.Id, p.Label, p.Index })
                        .ToList()
                })
                .ToListAsync();

            // Batch load all position post counts in a single query
            var allPositionIds = debates.SelectMany(d => d.Positions.Select(p => p.Id)).ToList();
            var positionCounts = allPositionIds.Count > 0
                ? await _context.DiscussionDebatePostPositions
                    .Where(pdp => allPositionIds.Contains(pdp.PositionId))
                    .GroupBy(pdp => pdp.PositionId)
                    .Select(g => new { PositionId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.PositionId, x => x.Count)
                : new Dictionary<int, int>();

            foreach (var debate in debates)
            {
                var publicId = debateIdMap[debate.DiscussionId];
                var positions = debate.Positions
                    .Select(p => new Application.Repositories.DebatePositionPreviewDto(
                        p.Label, p.Index, positionCounts.GetValueOrDefault(p.Id, 0)))
                    .ToList();
                result[publicId] = new(Debate: new(positions));
            }
        }

        // Links: fetch metadata
        if (linkIds.Count > 0)
        {
            var ids = linkIds.Select(d => d.Id).ToList();
            var linkIdMap = linkIds.ToDictionary(d => d.Id, d => d.PublicId);
            var links = await _context.DiscussionLinks
                .Where(l => ids.Contains(l.DiscussionId))
                .Select(l => new
                {
                    l.DiscussionId,
                    l.Url, l.Title, l.Description, l.Domain,
                    l.ImageUrl, l.ImagePath, l.ImageThumbnailPath, l.OEmbedHtml, l.IsInternal
                })
                .ToListAsync();

            foreach (var link in links)
            {
                var publicId = linkIdMap[link.DiscussionId];
                result[publicId] = new(Link: new(
                    link.Url, link.Title, link.Description, link.Domain,
                    link.ImageUrl, link.ImagePath, link.ImageThumbnailPath, link.OEmbedHtml, link.IsInternal));
            }
        }

        // Images: fetch image URLs via Media join
        if (imagesIds.Count > 0)
        {
            var ids = imagesIds.Select(d => d.Id).ToList();
            var imagesIdMap = imagesIds.ToDictionary(d => d.Id, d => d.PublicId);
            var images = await _context.DiscussionImages
                .Where(g => ids.Contains(g.DiscussionId))
                .Select(g => new
                {
                    g.DiscussionId,
                    g.IsSpoiler,
                    g.Layout,
                    Items = g.Images
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => new
                        {
                            Url = i.Image.StoragePath,
                            ThumbnailUrl = i.Image.ThumbnailPath,
                            MediumThumbnailUrl = i.Image.MediumThumbnailPath,
                            i.Image.BlurDataUri
                        })
                        .ToList()
                })
                .ToListAsync();

            foreach (var img in images)
            {
                var publicId = imagesIdMap[img.DiscussionId];
                var items = img.Items
                    .Select(i => new Application.Repositories.ImagePreviewItemDto(
                        fileStorage.GetPublicUrl(i.Url),
                        i.ThumbnailUrl is not null ? fileStorage.GetPublicUrl(i.ThumbnailUrl) : null,
                        i.MediumThumbnailUrl is not null ? fileStorage.GetPublicUrl(i.MediumThumbnailUrl) : null,
                        i.BlurDataUri))
                    .ToList();
                result[publicId] = new(Images: new(items.Count, items, img.IsSpoiler, img.Layout));
            }
        }

        // IAMAs: fetch phase, schedule, and activity counts
        if (iamaIds.Count > 0)
        {
            var ids = iamaIds.Select(d => d.Id).ToList();
            var iamaIdMap = iamaIds.ToDictionary(d => d.Id, d => d.PublicId);
            var iamas = await _context.DiscussionIamas
                .Where(i => ids.Contains(i.DiscussionId))
                .Select(i => new
                {
                    i.DiscussionId,
                    i.Phase,
                    i.ScheduledStartUtc,
                    i.ScheduledEndUtc,
                    OfficialAnswerCount = i.OfficialAnswers.Count,
                    BestQuestionCount = i.BestQuestions.Count,
                    IsVerified = i.VerificationNote != null && i.VerificationNote != ""
                })
                .ToListAsync();

            foreach (var iama in iamas)
            {
                var publicId = iamaIdMap[iama.DiscussionId];
                result[publicId] = new(Iama: new(
                    iama.Phase,
                    iama.ScheduledStartUtc,
                    iama.ScheduledEndUtc,
                    iama.OfficialAnswerCount,
                    iama.BestQuestionCount,
                    iama.IsVerified));
            }
        }

        return result;
    }
}
