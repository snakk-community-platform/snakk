namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Models;

public class SearchRepository(SnakkDbContext context) : ISearchRepository
{
    private readonly SnakkDbContext _context = context;

    public async Task<PagedResult<DiscussionSearchResultDto>> SearchDiscussionsAsync(
        string query,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20)
    {
        var baseQuery = _context.Discussions.AsNoTracking().AsQueryable();

        // Apply case-insensitive ILIKE search on Title (PostgreSQL)
        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in searchTerms)
            {
                var likeTerm = term; // Capture for closure
                baseQuery = baseQuery.Where(d => EF.Functions.ILike(d.Title, $"%{likeTerm}%"));
            }
        }

        // Apply filters
        if (!string.IsNullOrEmpty(authorPublicId))
            baseQuery = baseQuery.Where(d => d.CreatedByUser.PublicId == authorPublicId);
        if (!string.IsNullOrEmpty(spacePublicId))
            baseQuery = baseQuery.Where(d => d.Space.PublicId == spacePublicId);
        if (!string.IsNullOrEmpty(hubPublicId))
            baseQuery = baseQuery.Where(d => d.Space.Hub.PublicId == hubPublicId);

        var items = await baseQuery
            .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
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
                d.Posts.Count,
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
        var baseQuery = _context.Posts.AsNoTracking().AsQueryable();

        // Apply case-insensitive ILIKE search on Content (PostgreSQL)
        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in searchTerms)
            {
                var likeTerm = term; // Capture for closure
                baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.Content, $"%{likeTerm}%"));
            }
        }

        // Apply filters
        if (!string.IsNullOrEmpty(authorPublicId))
            baseQuery = baseQuery.Where(p => p.CreatedByUser.PublicId == authorPublicId);
        if (!string.IsNullOrEmpty(discussionPublicId))
            baseQuery = baseQuery.Where(p => p.Discussion.PublicId == discussionPublicId);
        if (!string.IsNullOrEmpty(spacePublicId))
            baseQuery = baseQuery.Where(p => p.Discussion.Space.PublicId == spacePublicId);

        var items = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
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

    public async Task<int> GetDiscussionCountByAuthorAsync(string authorPublicId)
    {
        return await _context.Discussions
            .AsNoTracking()
            .Where(d => d.CreatedByUser.PublicId == authorPublicId)
            .CountAsync();
    }

    public async Task<int> GetPostCountByAuthorAsync(string authorPublicId)
    {
        return await _context.Posts
            .AsNoTracking()
            .Where(p => p.CreatedByUser.PublicId == authorPublicId)
            .CountAsync();
    }

    public async Task<PagedResult<DiscussionListItemDto>> GetDiscussionsBySpaceAsync(
        string spacePublicId,
        int offset = 0,
        int pageSize = 20)
    {
        // Single query using navigation properties
        var query = _context.Discussions.AsNoTracking()
            .Where(d => d.Space.PublicId == spacePublicId && !d.IsDeleted)
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
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.Posts.Count(p => !p.IsDeleted),
                d.ReactionCount,
                d.CreatedByUser.PublicId,
                d.CreatedByUser.DisplayName,
                d.CreatedByUser.AvatarFileName,
                d.Tags
            ))
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

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
        // Single query that gets hubs with their stats using navigation properties
        var query = _context.Hubs.AsNoTracking()
            .OrderBy(h => h.Name);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(offset)
            .Take(pageSize)
            .Select(h => new HubListItemDto(
                h.PublicId,
                h.Community.PublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.CreatedAt,
                h.Spaces.Count(),
                h.Spaces.SelectMany(s => s.Discussions).Count(d => !d.IsDeleted),
                h.Spaces
                    .SelectMany(s => s.Discussions.Where(d => !d.IsDeleted))
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost && !p.IsDeleted)
            ))
            .ToListAsync();

        return new PagedResult<HubListItemDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }

    public async Task<PagedResult<SpaceListItemDto>> GetSpacesByHubAsync(
        string hubPublicId,
        int offset = 0,
        int pageSize = 20)
    {
        // Single query that gets spaces with their stats using navigation properties
        var query = _context.Spaces.AsNoTracking()
            .Where(s => s.Hub.PublicId == hubPublicId)
            .OrderBy(s => s.Name);

        var totalCount = await query.CountAsync();

        var spaces = await query
            .Skip(offset)
            .Take(pageSize)
            .Select(s => new
            {
                PublicId = s.PublicId,
                HubPublicId = s.Hub.PublicId,
                Name = s.Name,
                Slug = s.Slug,
                Description = s.Description,
                CreatedAt = s.CreatedAt,
                DiscussionCount = s.Discussions.Count(d => !d.IsDeleted),
                ReplyCount = s.Discussions
                    .Where(d => !d.IsDeleted)
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost && !p.IsDeleted),
                LatestDiscussion = s.Discussions
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
                    .Select(d => new
                    {
                        PublicId = d.PublicId,
                        Title = d.Title,
                        Slug = d.Slug,
                        LastActivityAt = d.LastActivityAt ?? d.CreatedAt,
                        AuthorPublicId = d.CreatedByUser.PublicId,
                        AuthorDisplayName = d.CreatedByUser.DisplayName,
                        AuthorAvatarFileName = d.CreatedByUser.AvatarFileName,
                        PostCount = d.Posts.Count(p => !d.IsDeleted)
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var items = spaces.Select(s => new SpaceListItemDto(
            s.PublicId,
            s.HubPublicId,
            s.Name,
            s.Slug,
            s.Description,
            s.CreatedAt,
            s.DiscussionCount,
            s.ReplyCount,
            s.LatestDiscussion != null
                ? new LatestDiscussionDto(
                    s.LatestDiscussion.PublicId,
                    s.LatestDiscussion.Title,
                    s.LatestDiscussion.Slug,
                    s.LatestDiscussion.LastActivityAt,
                    s.LatestDiscussion.AuthorPublicId,
                    s.LatestDiscussion.AuthorDisplayName,
                    s.LatestDiscussion.AuthorAvatarFileName,
                    s.LatestDiscussion.PostCount)
                : null
        )).ToList();

        return new PagedResult<SpaceListItemDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = offset + items.Count < totalCount
        };
    }

    public async Task<List<SitemapDiscussionDto>> GetSitemapDiscussionsAsync()
    {
        var discussions = await _context.Discussions
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.LastModifiedAt ?? d.CreatedAt)
            .Select(d => new SitemapDiscussionDto(
                d.PublicId,
                d.Slug,
                d.Space.Hub.Slug,
                d.Space.Slug,
                d.Space.Hub.Community.Slug,
                d.LastModifiedAt ?? d.CreatedAt,
                d.IsPinned))
            .ToListAsync();

        return discussions;
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetRecentDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? cursor = null)
    {
        var query = _context.Discussions.AsNoTracking();

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
                d.LastActivityAt < cursorDate ||
                (d.LastActivityAt == cursorDate && d.Id < cursorId));
        }
        else if (offset > 0)
        {
            // Fall back to offset for backwards compatibility
            query = query.Skip(offset);
        }

        var items = await query
            .OrderByDescending(d => d.LastActivityAt)
            .ThenByDescending(d => d.Id)
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                Dto = new Application.Repositories.RecentDiscussionDto(
                    d.PublicId,
                    d.Title,
                    d.Slug,
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
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

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
