namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Models;

public class SearchRepository(SnakkDbContext context) : ISearchRepository
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
        int pageSize = 20)
    {
        var baseQuery = _context.Discussions
            .Where(d => !d.IsDeleted);

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
                d.CreatedByUser.DisplayName,
                d.CreatedByUser.AvatarFileName,
                d.Space.PublicId,
                d.Space.Name,
                d.Space.Slug,
                d.Space.Hub.Slug,
                d.CreatedAt,
                d.LastActivityAt,
                d.PostCount,
                d.ReactionCount))
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
        int pageSize = 20)
    {
        var baseQuery = _context.Posts
            .Where(p => !p.IsDeleted);

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
                p.CreatedByUser.DisplayName,
                p.CreatedByUser.AvatarFileName,
                p.Discussion.PublicId,
                p.Discussion.Title,
                p.Discussion.Slug,
                p.Discussion.Space.Slug,
                p.Discussion.Space.Hub.Slug,
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

    public async Task<PagedResult<DiscussionListItemDto>> GetDiscussionsBySpaceAsync(
        string spacePublicId,
        int offset = 0,
        int pageSize = 20,
        int? typeFilter = null)
    {
        var baseQuery = _context.Discussions
            .Where(d => d.Space.PublicId == spacePublicId && !d.IsDeleted);

        if (typeFilter.HasValue)
            baseQuery = baseQuery.Where(d => d.Type == typeFilter.Value);

        var query = baseQuery
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastActivityAt);

        var items = await query
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(d => new DiscussionListItemDto(
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
                d.CreatedByUser.DisplayName,
                d.CreatedByUser.AvatarFileName,
                d.Tags))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        return new PagedResult<DiscussionListItemDto>
        {
            Items = resultItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems
        };
    }

    public async Task<PagedResult<HubListItemDto>> GetHubsAsync(
        int offset = 0,
        int pageSize = 20)
    {
        // Use denormalized counts + fetch one extra row to check HasMoreItems
        var hubs = await _context.Hubs
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

    public async Task<PagedResult<SpaceListItemDto>> GetSpacesByHubAsync(
        string hubPublicId,
        int offset = 0,
        int pageSize = 20)
    {
        // Use denormalized counts + fetch one extra row to check HasMoreItems (avoids separate COUNT query)
        var spaces = await _context.Spaces
            .Where(s => s.Hub.PublicId == hubPublicId)
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
                        s.LatestDiscussion.AuthorDisplayName,
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

    public async Task<(List<SitemapDiscussionDto> Items, int TotalCount)> GetSitemapDiscussionsAsync(
        int page,
        int pageSize)
    {
        var query = _context.Discussions.Where(d => !d.IsDeleted);

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
        string? cursor = null)
    {
        var query = _context.Discussions.AsQueryable();

        // Filter by community if specified
        if (!string.IsNullOrEmpty(communityId))
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
        var items = await query
            .OrderByDescending(d => d.LastActivityAt)
            .ThenByDescending(d => d.Id)
            .Skip(offset)
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
                    d.CreatedByUser.DisplayName,
                    d.CreatedByUser.AvatarFileName,
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

        // Generate next cursor from last item
        string? nextCursor = null;

        if (hasMoreItems && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = Cursor.Encode(lastItem.Dto.LastActivityAt, lastItem.Id);
        }

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = resultItems.Select(x => x.Dto),
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = nextCursor
        };
    }
}
