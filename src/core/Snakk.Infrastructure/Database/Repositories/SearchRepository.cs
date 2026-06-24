namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Extensions;
using Snakk.Shared.Models;

public class SearchRepository(SnakkDbContext context, IDbContextFactory<SnakkDbContext> dbContextFactory, IUserGrantsCacheService grantsCache, IFileStorage fileStorage, HybridCache cache) : ISearchRepository
{
    private readonly SnakkDbContext _context = context;
    private readonly IDbContextFactory<SnakkDbContext> _dbContextFactory = dbContextFactory;

    private bool IsPostgres => _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

    // FTS relevance ordering computes ts_rank() per MATCHING row before LIMIT can
    // apply — Postgres must fetch and detoast every matching tsvector from the
    // heap, so a term hitting ~half the corpus costs 500ms+ regardless of page
    // size (auto_explain: 99% of time in the Bitmap Heap Scan feeding the sort;
    // the GIN probe and top-N heapsort are both sub-10ms). Bounding the ranked
    // set to the newest N matches caps that work: rare terms (< N matches) rank
    // the exact same rows as before, while pathologically-frequent terms rank
    // "most relevant of the N newest" — a better forum default than global rank
    // over tens of thousands of hits, and 6-9x faster (dolor-class term on the
    // 253k-post dev corpus: 499ms -> 57ms; the planner walks a CreatedAt index
    // and filter-matches instead of bitmap-scanning the GIN when the term is
    // frequent enough). N is far above the deepest reachable page
    // (EndlessScroll:MaxPages caps offset at ~200).
    private const int RankCandidateCap = 2000;

    /// <summary>Escapes LIKE/ILIKE metacharacters so user input is treated as literal text.</summary>
    private static string EscapeLikePattern(string input) => input
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    /// <summary>Converts a UI date-range string to a UTC lower-bound cutoff, or null for "all time".</summary>
    private static DateTime? ParseDateCutoff(string? dateRange) => dateRange switch
    {
        "past hour"  => DateTime.UtcNow.AddHours(-1),
        "today"      => DateTime.UtcNow.Date,
        "past week"  => DateTime.UtcNow.AddDays(-7),
        "past month" => DateTime.UtcNow.AddMonths(-1),
        "past year"  => DateTime.UtcNow.AddYears(-1),
        _            => null
    };

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> SearchDiscussionsAsync(
        string query,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20,
        string? userId = null,
        bool viewerAllowsAdult = false,
        string? sortBy = null,
        string? dateRange = null,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Discussions
            .Where(d => !d.IsDeleted);

        baseQuery = await WithAccessFilterAsync(baseQuery, userId, ct);
        baseQuery = await WithAdultFilterAsync(baseQuery, viewerAllowsAdult, ct);

        // Full-text search: PostgreSQL uses tsvector + websearch_to_tsquery, others fall back to LIKE
        if (!string.IsNullOrWhiteSpace(query))
        {
            if (IsPostgres)
            {
                baseQuery = baseQuery.Where(d => d.SearchVector.Matches(
                    EF.Functions.WebSearchToTsQuery("english", query.Trim())));
            }
            else
            {
                var pattern = $"%{EscapeLikePattern(query.Trim())}%";
                baseQuery = baseQuery.Where(d => EF.Functions.Like(d.Title, pattern));
            }
        }

        if (!string.IsNullOrEmpty(authorPublicId))
            baseQuery = baseQuery.Where(d => d.CreatedByUserPublicId == authorPublicId);

        if (!string.IsNullOrEmpty(spacePublicId))
            baseQuery = baseQuery.Where(d => d.Space.PublicId == spacePublicId);

        if (!string.IsNullOrEmpty(hubPublicId))
            baseQuery = baseQuery.Where(d => d.Space.Hub.PublicId == hubPublicId);

        // Date-range filter — applied on CreatedAt regardless of sort order.
        // IX_Discussion_PostCount_IsDeleted / IX_Discussion_ReactionCount_IsDeleted handle the
        // no-FTS path; the GIN index handles the FTS path efficiently.
        var cutoff = ParseDateCutoff(dateRange);
        if (cutoff.HasValue)
            baseQuery = baseQuery.Where(d => d.CreatedAt >= cutoff.Value);

        // Sort order. When an explicit sort is requested it overrides FTS relevance ranking.
        // "newest" with an active FTS query still falls through to relevance (best UX default).
        var trimmedQuery = query.Trim();
        IOrderedQueryable<DiscussionDatabaseEntity> orderedQuery = sortBy switch
        {
            "oldest"    => baseQuery.OrderBy(d => d.CreatedAt).ThenBy(d => d.Id),
            "popular"   => baseQuery.OrderByDescending(d => d.PostCount).ThenByDescending(d => d.Id),
            "reactions" => baseQuery.OrderByDescending(d => d.ReactionCount).ThenByDescending(d => d.Id),
            // "newest" or default: relevance when FTS is active, activity otherwise.
            // Relevance ranks within the RankCandidateCap newest matches — see the
            // constant for why ranking the full match set is a per-row cost bomb.
            _ => !string.IsNullOrWhiteSpace(trimmedQuery) && IsPostgres
                ? baseQuery
                    .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
                    .Take(RankCandidateCap)
                    .OrderByDescending(d => d.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", trimmedQuery)))
                    .ThenByDescending(d => d.LastActivityAt ?? d.CreatedAt)
                : baseQuery
                    .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
        };

        var rawItems = await orderedQuery
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                d.PublicId, d.Title, d.Slug,
                d.Type,
                d.CreatedAt, d.LastActivityAt,
                d.IsPinned, d.IsLocked,
                d.PostCount, d.ReactionCount,
                d.Tags,
                d.IsAdultOnly,
                d.SpaceId, d.SpacePublicId,
                d.HubPublicId,
                d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId,
                d.AuthorDisplayName,
                d.AuthorAvatarFileName,
                d.AuthorAvatarThumbnailFileName,
                d.LastPostAuthorPublicId,
                d.LastPostAuthorDisplayName,
                d.LastPostAuthorAvatarFileName,
                d.LastPostAuthorAvatarThumbnailFileName,
                d.LastPostPlainTextExcerpt
            })
            .ToListAsync(ct);

        var spaceDisplay = await FetchSpaceDisplayAsync(rawItems.Select(x => x.SpaceId), ct);

        var items = rawItems.Select(d =>
        {
            var space = spaceDisplay.GetValueOrDefault(d.SpaceId);
            return new {
                d.Id,
                Dto = new Application.Repositories.RecentDiscussionDto(
                    d.PublicId, d.Title, d.Slug,
                    d.Type,
                    d.CreatedAt, d.LastActivityAt,
                    d.IsPinned, d.IsLocked,
                    d.SpacePublicId,
                    space?.Slug ?? "",
                    space?.Name ?? "",
                    d.HubPublicId,
                    space?.HubSlug,
                    space?.HubName,
                    d.CommunityPublicId,
                    space?.CommunitySlug,
                    space?.CommunityName,
                    d.AuthorPublicId,
                    d.AuthorDisplayName ?? "",
                    d.AuthorAvatarFileName,
                    d.AuthorAvatarThumbnailFileName,
                    d.PostCount, d.ReactionCount,
                    string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                    LastReplierPublicId: d.LastPostAuthorPublicId,
                    LastReplierDisplayName: d.LastPostAuthorDisplayName,
                    LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                    LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                    LastPostExcerpt: d.LastPostPlainTextExcerpt,
                    IsAdult: d.IsAdultOnly)
            };
        }).ToList();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList(), ct);

        var finalItems = resultItems.Select(x =>
            previewMap.TryGetValue(x.Dto.PublicId, out var preview)
                ? x.Dto with { Preview = preview }
                : x.Dto).ToList();

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = finalItems,
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
        string? userId = null,
        string? sortBy = null,
        string? dateRange = null,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Posts
            .Where(p => !p.IsDeleted);

        baseQuery = await WithPostAccessFilterAsync(baseQuery, userId, ct);

        // Full-text search: PostgreSQL uses tsvector + websearch_to_tsquery, others fall back to LIKE
        if (!string.IsNullOrWhiteSpace(query))
        {
            if (IsPostgres)
            {
                baseQuery = baseQuery.Where(p => p.SearchVector.Matches(
                    EF.Functions.WebSearchToTsQuery("english", query.Trim())));
            }
            else
            {
                var pattern = $"%{EscapeLikePattern(query.Trim())}%";
                baseQuery = baseQuery.Where(p => EF.Functions.Like(p.Content, pattern));
            }
        }

        // Apply filters
        if (!string.IsNullOrEmpty(authorPublicId))
            baseQuery = baseQuery.Where(p => p.CreatedByUserPublicId == authorPublicId);

        if (!string.IsNullOrEmpty(discussionPublicId))
            baseQuery = baseQuery.Where(p => p.DiscussionPublicId == discussionPublicId);

        if (!string.IsNullOrEmpty(spacePublicId))
            baseQuery = baseQuery.Where(p => p.Discussion.Space.PublicId == spacePublicId);

        // Date-range filter on CreatedAt. IX_Post_ReactionCount_IsDeleted handles the
        // no-FTS popular/reactions path; GIN handles the FTS path.
        var cutoff = ParseDateCutoff(dateRange);
        if (cutoff.HasValue)
            baseQuery = baseQuery.Where(p => p.CreatedAt >= cutoff.Value);

        // Posts use ReactionCount for both "popular" and "reactions" — posts don't have a reply count.
        var trimmedQuery = query.Trim();
        IOrderedQueryable<PostDatabaseEntity> orderedQuery = sortBy switch
        {
            "oldest"               => baseQuery.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            "popular" or "reactions" => baseQuery.OrderByDescending(p => p.ReactionCount).ThenByDescending(p => p.Id),
            // Relevance ranks within the RankCandidateCap newest matches — see the
            // constant for why ranking the full match set is a per-row cost bomb.
            _ => !string.IsNullOrWhiteSpace(trimmedQuery) && IsPostgres
                ? baseQuery
                    .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                    .Take(RankCandidateCap)
                    .OrderByDescending(p => p.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", trimmedQuery)))
                    .ThenByDescending(p => p.CreatedAt)
                : baseQuery
                    .OrderByDescending(p => p.CreatedAt)
        };

        return await orderedQuery
            .Select(p => new PostSearchResultDto(
                p.PublicId,
                p.PlainTextExcerpt ?? "",
                p.CreatedByUserPublicId,
                p.CreatedByUser.DisplayName ?? "",
                p.CreatedByUser.AvatarFileName,
                p.DiscussionPublicId,
                p.Discussion.Title,
                p.Discussion.Slug,
                p.Discussion.Space.Slug,
                p.Discussion.Space.Name,
                p.Discussion.Space.HubSlug!,
                p.Discussion.Space.HubName!,
                p.Discussion.Space.CommunitySlug!,
                p.CreatedAt))
            .ToPagedResultAsync(offset, pageSize, ct);
    }

    public async Task<int> GetDiscussionCountByAuthorAsync(string authorPublicId, CancellationToken ct = default) => await _context.Users
        .Where(u => u.PublicId == authorPublicId)
        .Select(u => u.DiscussionCount)
        .FirstOrDefaultAsync(ct);

    public async Task<int> GetPostCountByAuthorAsync(string authorPublicId, CancellationToken ct = default) =>
        // ReplyCount = non-first posts. Add DiscussionCount to get total posts (each discussion has a first post).
        await _context.Users
            .Where(u => u.PublicId == authorPublicId)
            .Select(u => u.ReplyCount + u.DiscussionCount)
            .FirstOrDefaultAsync(ct);

    public async Task<int> GetDiscussionPostCountAsync(string discussionPublicId, CancellationToken ct = default) =>
        await _context.Discussions
            .Where(d => d.PublicId == discussionPublicId)
            .Select(d => d.PostCount)
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<DiscussionListItemDto>> GetDiscussionsBySpaceAsync(
        string spacePublicId,
        int offset = 0,
        int pageSize = 20,
        int? typeFilter = null,
        string? userId = null,
        string? cursor = null,
        bool viewerAllowsAdult = false,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        // When mods request deleted content, bypass the global soft-delete filter.
        // The Space navigation still has its own filter, so only non-deleted spaces match.
        var baseQuery = includeDeleted
            ? _context.Discussions.IgnoreQueryFilters()
                .Where(d => d.Space.PublicId == spacePublicId)
            : _context.Discussions
                .Where(d => d.Space.PublicId == spacePublicId && !d.IsDeleted);

        baseQuery = await WithAccessFilterAsync(baseQuery, userId, ct);
        baseQuery = await WithAdultFilterAsync(baseQuery, viewerAllowsAdult, ct);

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
            orderedQuery = (IOrderedQueryable<DiscussionDatabaseEntity>)orderedQuery.Skip(offset);

        var items = await orderedQuery
            .Take(pageSize + 1)
            .Select(d => new
            {
                d.Id,
                Dto = new DiscussionListItemDto(
                    d.PublicId,
                    d.SpacePublicId!,
                    d.Title,
                    d.Slug,
                    d.Type,
                    d.CreatedAt,
                    d.LastActivityAt,
                    d.IsPinned,
                    d.IsLocked,
                    d.PostCount,
                    d.ReactionCount,
                    d.CreatedByUserPublicId,
                    d.AuthorDisplayName ?? "",
                    d.AuthorAvatarFileName,
                    d.Tags,
                    null,
                    d.IsDeleted)
            })
            .ToListAsync(ct);

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
        string? userId = null,
        CancellationToken ct = default)
    {
        // Use denormalized counts + fetch one extra row to check HasMoreItems
        var query = _context.Hubs.AsQueryable();
        query = await WithHubAccessFilterAsync(query, userId, ct);
        return await query
            .OrderBy(h => h.Name)
            .Select(h => new HubListItemDto(
                h.PublicId,
                h.CommunityPublicId,
                h.Name,
                h.Slug,
                h.Description,
                h.CreatedAt,
                h.SpaceCount,
                h.DiscussionCount,
                h.PostCount - h.DiscussionCount))
            .ToPagedResultAsync(offset, pageSize, ct);
    }

    public async Task<List<SpaceSearchItemDto>> SearchSpacesAsync(
        string? query = null,
        string? hubPublicId = null,
        string? communityPublicId = null,
        int limit = 10,
        string? userId = null,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Spaces.AsQueryable();
        baseQuery = await WithSpaceAccessFilterAsync(baseQuery, userId, ct);

        if (!string.IsNullOrEmpty(hubPublicId))
            baseQuery = baseQuery.Where(s => s.Hub.PublicId == hubPublicId);
        else if (!string.IsNullOrEmpty(communityPublicId))
            baseQuery = baseQuery.Where(s => s.CommunityPublicId == communityPublicId);

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
                HubSlug = s.HubSlug,
                HubName = s.HubName,
                CommunitySlug = s.CommunitySlug,
                s.DiscussionCount,
                CommunityName = s.CommunityName
            })
            .ToListAsync(ct);

        return raw.Select(s => new SpaceSearchItemDto(
            s.PublicId, s.Name, s.Slug, s.HubSlug!, s.HubName!,
            s.CommunitySlug!, s.DiscussionCount, s.CommunityName!,
            Snakk.Shared.Helpers.AvatarHelper.GetAvatarUrl(s.PublicId, Snakk.Shared.Helpers.AvatarEntityType.Space, 0)))
            .ToList();
    }

    public async Task<PagedResult<SpaceListItemDto>> GetSpacesByHubAsync(
        string hubPublicId,
        int offset = 0,
        int pageSize = 20,
        string? userId = null,
        CancellationToken ct = default)
    {
        // Use denormalized counts + fetch one extra row to check HasMoreItems (avoids separate COUNT query)
        var baseSpaceQuery = _context.Spaces
            .Where(s => s.Hub.PublicId == hubPublicId);
        baseSpaceQuery = await WithSpaceAccessFilterAsync(baseSpaceQuery, userId, ct);
        var spaces = await baseSpaceQuery
            .OrderBy(s => s.Name)
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(s => new {
                s.Id,
                s.PublicId,
                HubPublicId = s.HubPublicId,
                s.Name,
                s.Slug,
                s.Description,
                s.CreatedAt,
                s.DiscussionCount,
                ReplyCount = s.PostCount - s.DiscussionCount,
                s.AvatarFileName,
            })
            .ToListAsync(ct);

        var hasMore = spaces.Count > pageSize;
        var page = spaces.Take(pageSize).ToList();

        var latestBySpace = await GetLatestDiscussionPerSpaceAsync(
            page.Select(s => s.Id).ToArray(), ct);

        var items = page
            .Select(s => {
                latestBySpace.TryGetValue(s.Id, out var ld);
                return new SpaceListItemDto(
                    s.PublicId,
                    s.HubPublicId!,
                    s.Name,
                    s.Slug,
                    s.Description,
                    s.CreatedAt,
                    s.DiscussionCount,
                    s.ReplyCount,
                    ld is not null
                        ? new LatestDiscussionDto(
                            ld.PublicId,
                            ld.Title,
                            ld.Slug,
                            ld.LastActivityAt,
                            ld.AuthorPublicId,
                            ld.AuthorDisplayName ?? "",
                            ld.AuthorAvatarFileName,
                            ld.PostCount)
                        : null,
                    s.AvatarFileName);
            })
            .ToList();

        return new PagedResult<SpaceListItemDto>
        {
            Items = items,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMore
        };
    }

    private async Task<IQueryable<HubDatabaseEntity>> WithHubAccessFilterAsync(
        IQueryable<HubDatabaseEntity> query, string? userId, CancellationToken ct = default)
    {
        if (!await grantsCache.AnyRestrictedAsync(ct))
            return query;

        if (userId == null)
            return query.Where(h =>
                !h.IsRestricted &&
                !h.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(h =>
            (!h.IsRestricted || hubIds.Contains(h.Id))
            && (!h.Community.IsRestricted || communityIds.Contains(h.CommunityId)));
    }

    private async Task<IQueryable<SpaceDatabaseEntity>> WithSpaceAccessFilterAsync(
        IQueryable<SpaceDatabaseEntity> query, string? userId, CancellationToken ct = default)
    {
        if (!await grantsCache.AnyRestrictedAsync(ct))
            return query;

        if (userId == null)
            return query.Where(s =>
                !s.IsRestricted &&
                !s.Hub.IsRestricted &&
                !s.Hub.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
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
    private async Task<IQueryable<PostDatabaseEntity>> WithPostAccessFilterAsync(
        IQueryable<PostDatabaseEntity> query, string? userId, CancellationToken ct = default)
    {
        if (!await grantsCache.AnyRestrictedAsync(ct))
            return query;

        if (userId == null)
            return query.Where(p =>
                !p.Discussion.Space.IsRestricted
                && !p.Discussion.Space.Hub.IsRestricted
                && !p.Discussion.Space.Hub.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        var spaceIds = grants.SpaceIds;
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(p =>
            (!p.Discussion.Space.IsRestricted || spaceIds.Contains(p.Discussion.SpaceId))
            && (!p.Discussion.Space.Hub.IsRestricted || hubIds.Contains(p.Discussion.Space.HubId))
            && (!p.Discussion.Space.Hub.Community.IsRestricted || communityIds.Contains(p.Discussion.Space.Hub.CommunityId)));
    }

    /// <summary>
    /// When the viewer hasn't opted into adult content, hide adult-tagged discussions from
    /// communities that have HideAdultDiscussionsFromLists enabled. Viewers who allow adult
    /// content (authenticated with AllowAdultContent==true, or anonymous with the confirm cookie)
    /// see all discussions regardless of community setting.
    /// </summary>
    private async Task<IQueryable<DiscussionDatabaseEntity>> WithAdultFilterAsync(
        IQueryable<DiscussionDatabaseEntity> query, bool viewerAllowsAdult, CancellationToken ct = default)
    {
        if (viewerAllowsAdult) return query;
        var adultSpaceIds = await grantsCache.GetAdultHidingSpaceIdsAsync(ct);
        if (adultSpaceIds.Count == 0) return query;
        return query.Where(d => !(d.IsAdultOnly && adultSpaceIds.Contains(d.SpaceId)));
    }

    private async Task<IQueryable<DiscussionDatabaseEntity>> WithAccessFilterAsync(
        IQueryable<DiscussionDatabaseEntity> query, string? userId, CancellationToken ct = default)
    {
        if (!await grantsCache.AnyRestrictedAsync(ct))
            return query;

        if (userId == null)
            return query.Where(d =>
                !d.Space.IsRestricted &&
                !d.Space.Hub.IsRestricted &&
                !d.Space.Hub.Community.IsRestricted);

        var grants = await grantsCache.GetGrantsAsync(userId, ct);
        var spaceIds = grants.SpaceIds;
        var hubIds = grants.HubIds;
        var communityIds = grants.CommunityIds;

        return query.Where(d =>
            (!d.Space.IsRestricted || spaceIds.Contains(d.SpaceId))
            && (!d.Space.Hub.IsRestricted || hubIds.Contains(d.Space.HubId))
            && (!d.Space.Hub.Community.IsRestricted || communityIds.Contains(d.Space.Hub.CommunityId)));
    }

    // Latest-discussion-per-space drives the hub/community space listings and was ~85% of DB
    // time under load (the DISTINCT-ON re-ran on every render across hubs/pages/users). It's
    // viewer-agnostic — space-level access is already filtered upstream — so cache it per space,
    // single-flight. Invalidated by SpaceLatestDiscussionCacheInvalidationHandler on
    // DiscussionCreatedEvent and PostCreatedEvent; 24 h TTL is a safety net only.
    private static readonly HybridCacheEntryOptions LatestPerSpaceCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromHours(24),
    };

    // Preview data is immutable for links/images and only changes on explicit user actions for
    // polls/debates/IAMAs (votes, position assignments, official answers, phase transitions).
    // Invalidation is event-driven — see PollService and DiscussionTypeQueryService.
    private static readonly HybridCacheEntryOptions _previewCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromHours(24),
    };

    private static readonly HybridCacheEntryOptions _spaceDisplayCacheOptions = new()
    {
        Expiration = TimeSpan.FromDays(365),
        LocalCacheExpiration = TimeSpan.FromDays(365),
    };

    private async Task<Dictionary<int, SpaceLatestDiscussion>> GetLatestDiscussionPerSpaceAsync(
        int[] spaceIds, CancellationToken ct)
    {
        if (spaceIds.Length == 0) return [];

        var tasks = spaceIds.Select(spaceId => cache.GetOrCreateAsync<SpaceLatestDiscussion?>(
            $"space-latest-discussion:{spaceId}",
            async c =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(c);
                return await QueryLatestDiscussionForSpaceAsync(spaceId, db, c);
            },
            LatestPerSpaceCacheOptions,
            cancellationToken: ct).AsTask());

        var results = await Task.WhenAll(tasks);

        var result = new Dictionary<int, SpaceLatestDiscussion>(spaceIds.Length);
        for (var i = 0; i < spaceIds.Length; i++)
        {
            if (results[i] is not null)
                result[spaceIds[i]] = results[i]!;
        }
        return result;
    }

    private static async Task<SpaceLatestDiscussion?> QueryLatestDiscussionForSpaceAsync(int spaceId, SnakkDbContext db, CancellationToken ct) =>
        await db.Database
            .SqlQuery<SpaceLatestDiscussion>($"""
                SELECT
                    d."SpaceId",
                    d."PublicId",
                    d."Title",
                    d."Slug",
                    COALESCE(d."LastActivityAt", d."CreatedAt") AS "LastActivityAt",
                    d."CreatedByUserPublicId" AS "AuthorPublicId",
                    d."AuthorDisplayName",
                    d."AuthorAvatarFileName",
                    d."PostCount"
                FROM "Discussion" d
                WHERE d."SpaceId" = {spaceId} AND NOT d."IsDeleted"
                ORDER BY COALESCE(d."LastActivityAt", d."CreatedAt") DESC
                LIMIT 1
                """)
            .FirstOrDefaultAsync(ct);

    private sealed class SpaceLatestDiscussion
    {
        public int SpaceId { get; set; }
        public string PublicId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public DateTime LastActivityAt { get; set; }
        public string? AuthorPublicId { get; set; }
        public string? AuthorDisplayName { get; set; }
        public string? AuthorAvatarFileName { get; set; }
        public int PostCount { get; set; }
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

        var tasks = ids.Select(id => cache.GetOrCreateAsync<SpaceDisplay?>(
            $"space-display:{id}",
            async ct2 =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct2);
                return await FetchSingleSpaceDisplayAsync(id, db, ct2);
            },
            _spaceDisplayCacheOptions, cancellationToken: ct).AsTask());

        var results = await Task.WhenAll(tasks);

        var result = new Dictionary<int, SpaceDisplay>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            if (results[i] is not null) result[ids[i]] = results[i]!;
        }
        return result;
    }

    private static async ValueTask<SpaceDisplay?> FetchSingleSpaceDisplayAsync(int spaceId, SnakkDbContext db, CancellationToken ct) =>
        await db.Spaces
            .Where(s => s.Id == spaceId)
            .Select(s => new SpaceDisplay(s.Slug, s.Name, s.Description, s.AvatarFileName, s.AvatarThumbnailFileName, s.HubSlug, s.HubName, s.CommunitySlug, s.CommunityName))
            .FirstOrDefaultAsync(ct);

    public async Task<(List<SitemapDiscussionDto> Items, int TotalCount)> GetSitemapDiscussionsAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Discussions.Where(d => !d.IsDeleted);
        query = await WithAccessFilterAsync(query, null, ct);

        var totalCount = await query.CountAsync(ct);

        var discussions = await query
            .OrderByDescending(d => d.LastModifiedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new SitemapDiscussionDto(
                d.PublicId,
                d.Slug,
                d.Space.HubSlug!,
                d.Space.Slug,
                d.Space.CommunitySlug!,
                d.LastModifiedAt ?? d.CreatedAt,
                d.IsPinned))
            .ToListAsync(ct);

        return (discussions, totalCount);
    }

    public async Task<int> GetUnreadDiscussionCountAsync(
        DateTime since,
        string? userId = null,
        bool viewerAllowsAdult = false,
        CancellationToken ct = default)
    {
        var query = _context.Discussions.AsQueryable();
        query = await WithAccessFilterAsync(query, userId, ct);
        query = await WithAdultFilterAsync(query, viewerAllowsAdult, ct);
        query = query.Where(d => d.LastActivityAt > since);
        return await query.CountAsync(ct);
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetRecentDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        string? cursor = null,
        string? userId = null,
        string? authorId = null,
        IReadOnlyList<string>? spaceIds = null,
        bool viewerAllowsAdult = false,
        bool sinceLastVisit = false,
        DateTime? lastVisitAt = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = includeDeleted
            ? _context.Discussions.IgnoreQueryFilters()
            : _context.Discussions.AsQueryable();

        query = await WithAccessFilterAsync(query, userId, ct);
        query = await WithAdultFilterAsync(query, viewerAllowsAdult, ct);

        if (sinceLastVisit && lastVisitAt.HasValue)
            query = query.Where(d => d.LastActivityAt > lastVisitAt.Value);

        // Filter by author if specified
        if (!string.IsNullOrEmpty(authorId))
        {
            query = query.Where(d => d.CreatedByUserPublicId == authorId);
        }

        // Scope filters go through the Space navigation rather than the denormalized
        // public-id columns: the parents' PublicId columns are uniquely indexed and the
        // join lets the planner use Discussion's int FK indexes (the denormalized string
        // columns are unindexed). It also preserves the parents' soft-delete query
        // filters, matching the old resolve-then-filter behavior.
        // Filter by multiple spaces (e.g. My Feed)
        if (spaceIds is { Count: > 0 })
            query = query.Where(d => spaceIds.Contains(d.Space.PublicId));
        // Filter by single space (most specific)
        else if (!string.IsNullOrEmpty(spaceId))
            query = query.Where(d => d.Space.PublicId == spaceId);
        // Filter by hub if specified (more specific than community)
        else if (!string.IsNullOrEmpty(hubId))
            query = query.Where(d => d.Space.Hub.PublicId == hubId);
        // Filter by community if specified
        else if (!string.IsNullOrEmpty(communityId))
            query = query.Where(d => d.Space.Hub.Community.PublicId == communityId);

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
            orderedQuery = (IOrderedQueryable<DiscussionDatabaseEntity>)orderedQuery.Skip(offset);

        var rawItems = await orderedQuery
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                d.PublicId,
                d.Title,
                d.Slug,
                d.Type,
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.PostCount,
                d.ReactionCount,
                d.Tags,
                d.IsAdultOnly,
                d.IsDeleted,
                SpaceId = d.SpaceId,
                SpacePublicId = d.SpacePublicId,
                HubPublicId = d.HubPublicId,
                CommunityPublicId = d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId,
                AuthorDisplayName = d.AuthorDisplayName,
                AuthorAvatarFileName = d.AuthorAvatarFileName,
                AuthorAvatarThumbnailFileName = d.AuthorAvatarThumbnailFileName,
                LastPostAuthorPublicId = d.LastPostAuthorPublicId,
                LastPostAuthorDisplayName = d.LastPostAuthorDisplayName,
                LastPostAuthorAvatarFileName = d.LastPostAuthorAvatarFileName,
                LastPostAuthorAvatarThumbnailFileName = d.LastPostAuthorAvatarThumbnailFileName,
                LastPostPlainTextExcerpt = d.LastPostPlainTextExcerpt
            })
            .ToListAsync(ct);

        var spaceDisplay = await FetchSpaceDisplayAsync(rawItems.Select(x => x.SpaceId), ct);

        var items = rawItems.Select(d =>
        {
            var space = spaceDisplay.GetValueOrDefault(d.SpaceId);
            return new
            {
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
                d.SpacePublicId!,
                space?.Slug ?? "",
                space?.Name ?? "",
                d.HubPublicId!,
                space?.HubSlug ?? "",
                space?.HubName ?? "",
                d.CommunityPublicId,
                space?.CommunitySlug,
                space?.CommunityName,
                d.AuthorPublicId,
                d.AuthorDisplayName ?? "",
                d.AuthorAvatarFileName,
                d.AuthorAvatarThumbnailFileName,
                d.PostCount,
                d.ReactionCount,
                string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                LastReplierPublicId: d.LastPostAuthorPublicId,
                LastReplierDisplayName: d.LastPostAuthorDisplayName,
                LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                LastPostExcerpt: d.LastPostPlainTextExcerpt,
                IsAdult: d.IsAdultOnly,
                IsDeleted: d.IsDeleted)
            };
        }).ToList();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems
            ? items
                .Take(pageSize)
                .ToList()
            : items;

        // Batch-fetch preview data for typed discussions (max 4 queries, not N+1)
        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList(), ct);

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

    public async Task<Dictionary<string, Application.Repositories.DiscussionPreviewDto>> FetchPreviewsByPublicIdsAsync(
        IEnumerable<string> publicIds,
        CancellationToken ct = default)
    {
        var ids = publicIds.ToList();
        if (ids.Count == 0) return new();
        var discussions = await _context.Discussions
            .Where(d => ids.Contains(d.PublicId))
            .Select(d => new { d.Id, d.PublicId, d.Type })
            .ToListAsync(ct);
        return await BatchFetchPreviewsAsync(discussions.Select(d => (d.Id, d.PublicId, d.Type)).ToList(), ct);
    }

    public async Task<List<Application.Repositories.RecentDiscussionDto>> GetRecentDiscussionsByPublicIdsAsync(
        IEnumerable<string> publicIds,
        CancellationToken ct = default)
    {
        var ids = publicIds.ToList();
        if (ids.Count == 0) return [];
        var rawItems = await _context.Discussions
            .Where(d => ids.Contains(d.PublicId)
                && !d.IsDeleted
                && !d.Space.IsRestricted
                && !d.Space.Hub.IsRestricted
                && !d.Space.Hub.Community.IsRestricted)
            .Select(d => new {
                d.Id,
                d.PublicId,
                d.Title,
                d.Slug,
                d.Type,
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.PostCount,
                d.ReactionCount,
                d.Tags,
                d.IsAdultOnly,
                SpaceId = d.SpaceId,
                SpacePublicId = d.SpacePublicId,
                HubPublicId = d.HubPublicId,
                CommunityPublicId = d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId,
                AuthorDisplayName = d.AuthorDisplayName,
                AuthorAvatarFileName = d.AuthorAvatarFileName,
                AuthorAvatarThumbnailFileName = d.AuthorAvatarThumbnailFileName,
                LastPostAuthorPublicId = d.LastPostAuthorPublicId,
                LastPostAuthorDisplayName = d.LastPostAuthorDisplayName,
                LastPostAuthorAvatarFileName = d.LastPostAuthorAvatarFileName,
                LastPostAuthorAvatarThumbnailFileName = d.LastPostAuthorAvatarThumbnailFileName,
                LastPostPlainTextExcerpt = d.LastPostPlainTextExcerpt
            })
            .ToListAsync(ct);
        var spaceDisplay = await FetchSpaceDisplayAsync(rawItems.Select(x => x.SpaceId), ct);
        var resultItems = rawItems.Select(d =>
        {
            spaceDisplay.TryGetValue(d.SpaceId, out var space);
            return new {
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
                d.SpacePublicId!,
                space?.Slug ?? "",
                space?.Name ?? "",
                d.HubPublicId!,
                space?.HubSlug ?? "",
                space?.HubName ?? "",
                d.CommunityPublicId,
                space?.CommunitySlug,
                space?.CommunityName,
                d.AuthorPublicId,
                d.AuthorDisplayName ?? "",
                d.AuthorAvatarFileName,
                d.AuthorAvatarThumbnailFileName,
                d.PostCount,
                d.ReactionCount,
                string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                LastReplierPublicId: d.LastPostAuthorPublicId,
                LastReplierDisplayName: d.LastPostAuthorDisplayName,
                LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                LastPostExcerpt: d.LastPostPlainTextExcerpt,
                IsAdult: d.IsAdultOnly)
            };
        }).ToList();
        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList(), ct);
        return resultItems.Select(x =>
            previewMap.TryGetValue(x.Dto.PublicId, out var preview) ? x.Dto with { Preview = preview } : x.Dto
        ).ToList();
    }

    private async Task<Dictionary<string, Application.Repositories.DiscussionPreviewDto>> BatchFetchPreviewsAsync(
        List<(int Id, string PublicId, int Type)> discussions,
        CancellationToken ct = default)
    {
        (string key, Func<SnakkDbContext, CancellationToken, ValueTask<Application.Repositories.DiscussionPreviewDto?>>) GetFetcher(
            (int Id, string PublicId, int Type) d) => d.Type switch
        {
            2 => ($"preview:poll:{d.PublicId}",    (db, c) => FetchPollPreviewAsync(d.Id, db, c)),
            7 => ($"preview:debate:{d.PublicId}",  (db, c) => FetchDebatePreviewAsync(d.Id, db, c)),
            4 => ($"preview:link:{d.PublicId}",    (db, c) => FetchLinkPreviewAsync(d.Id, db, c)),
            5 => ($"preview:images:{d.PublicId}",  (db, c) => FetchImagesPreviewAsync(d.Id, db, c)),
            9 => ($"preview:iama:{d.PublicId}",    (db, c) => FetchIamaPreviewAsync(d.Id, db, c)),
            _ => default
        };

        var eligible = discussions
            .Where(d => d.Type is 2 or 7 or 4 or 5 or 9)
            .ToList();

        var tasks = eligible.Select(d =>
        {
            var (key, fetcher) = GetFetcher(d);
            return (d.PublicId, Task: cache.GetOrCreateAsync<Application.Repositories.DiscussionPreviewDto?>(
                key,
                async ct2 =>
                {
                    await using var db = await _dbContextFactory.CreateDbContextAsync(ct2);
                    return await fetcher(db, ct2);
                },
                _previewCacheOptions, cancellationToken: ct).AsTask());
        }).ToList();

        await Task.WhenAll(tasks.Select(t => t.Task));

        var result = new Dictionary<string, Application.Repositories.DiscussionPreviewDto>();
        foreach (var (publicId, task) in tasks)
        {
            var preview = await task;
            if (preview is not null) result[publicId] = preview;
        }
        return result;
    }

    private static async ValueTask<Application.Repositories.DiscussionPreviewDto?> FetchPollPreviewAsync(int discussionId, SnakkDbContext db, CancellationToken ct)
    {
        var poll = await db.DiscussionPolls
            .Where(p => p.DiscussionId == discussionId)
            .Select(p => new
            {
                p.VotesVisible,
                p.ClosesAt,
                Options = p.Options.OrderBy(o => o.DisplayOrder).Select(o => new { o.Text, o.VoteCount }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (poll is null) return null;

        var isSecret = !poll.VotesVisible;
        var isClosed = poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow;
        var hideVotes = isSecret && !isClosed;
        var options = poll.Options
            .Select(o => new Application.Repositories.PollOptionPreviewDto(o.Text, hideVotes ? 0 : o.VoteCount))
            .ToList();
        var totalVotes = hideVotes ? 0 : options.Sum(o => o.VoteCount);
        return new(Poll: new(options, totalVotes, IsSecret: isSecret, ClosesAt: poll.ClosesAt));
    }

    private static async ValueTask<Application.Repositories.DiscussionPreviewDto?> FetchDebatePreviewAsync(int discussionId, SnakkDbContext db, CancellationToken ct)
    {
        var debate = await db.DiscussionDebates
            .Where(d => d.DiscussionId == discussionId)
            .Select(d => new
            {
                Positions = d.Positions.OrderBy(p => p.Index).Select(p => new { p.Id, p.Label, p.Index }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (debate is null) return null;

        var positionIds = debate.Positions.Select(p => p.Id).ToList();
        var positionCounts = new Dictionary<int, int>();
        if (positionIds.Count > 0)
        {
            var postPositions = await db.DiscussionDebatePostPositions
                .Where(pdp => positionIds.Contains(pdp.PositionId))
                .Select(pdp => new { pdp.PositionId, pdp.Post.CreatedByUserId, pdp.Post.CreatedAt })
                .ToListAsync(ct);

            // Only the latest-position post per user counts toward position vote tallies.
            positionCounts = postPositions
                .GroupBy(x => x.CreatedByUserId)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .GroupBy(x => x.PositionId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var positions = debate.Positions
            .Select(p => new Application.Repositories.DebatePositionPreviewDto(
                p.Label, p.Index, positionCounts.GetValueOrDefault(p.Id, 0)))
            .ToList();
        return new(Debate: new(positions));
    }

    private static async ValueTask<Application.Repositories.DiscussionPreviewDto?> FetchLinkPreviewAsync(int discussionId, SnakkDbContext db, CancellationToken ct)
    {
        var link = await db.DiscussionLinks
            .Where(l => l.DiscussionId == discussionId)
            .Select(l => new
            {
                l.Url, l.Title, l.Description, l.Domain,
                l.ImageUrl, l.ImagePath, l.ImageThumbnailPath, l.OEmbedHtml, l.IsInternal,
                l.ImageBlurDataUri, l.ImageWidth, l.ImageHeight
            })
            .FirstOrDefaultAsync(ct);

        if (link is null) return null;

        return new(Link: new(
            link.Url, link.Title, link.Description, link.Domain,
            link.ImageUrl, link.ImagePath, link.ImageThumbnailPath, link.OEmbedHtml, link.IsInternal,
            link.ImageBlurDataUri, link.ImageWidth, link.ImageHeight));
    }

    private async ValueTask<Application.Repositories.DiscussionPreviewDto?> FetchImagesPreviewAsync(int discussionId, SnakkDbContext db, CancellationToken ct)
    {
        var img = await db.DiscussionImages
            .Where(g => g.DiscussionId == discussionId)
            .Select(g => new
            {
                g.IsSpoiler,
                g.Layout,
                Items = g.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new
                    {
                        Url = i.Image.StoragePath,
                        ThumbnailUrl = i.Image.ThumbnailPath,
                        i.Image.ThumbnailWidth,
                        i.Image.ThumbnailHeight,
                        MediumThumbnailUrl = i.Image.MediumThumbnailPath,
                        i.Image.MediumThumbnailWidth,
                        i.Image.MediumThumbnailHeight,
                        i.Image.BlurDataUri,
                        i.Image.Width,
                        i.Image.Height
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (img is null) return null;

        var items = img.Items
            .Select(i => new Application.Repositories.ImagePreviewItemDto(
                fileStorage.GetPublicUrl(i.Url),
                i.ThumbnailUrl is not null ? fileStorage.GetPublicUrl(i.ThumbnailUrl) : null,
                i.MediumThumbnailUrl is not null ? fileStorage.GetPublicUrl(i.MediumThumbnailUrl) : null,
                i.BlurDataUri,
                i.Width, i.Height,
                i.ThumbnailWidth, i.ThumbnailHeight,
                i.MediumThumbnailWidth, i.MediumThumbnailHeight))
            .ToList();
        return new(Images: new(items.Count, items, img.IsSpoiler, img.Layout));
    }

    private static async ValueTask<Application.Repositories.DiscussionPreviewDto?> FetchIamaPreviewAsync(int discussionId, SnakkDbContext db, CancellationToken ct)
    {
        var iama = await db.DiscussionIamas
            .Where(i => i.DiscussionId == discussionId)
            .Select(i => new
            {
                i.Phase,
                i.ScheduledStartUtc,
                i.ScheduledEndUtc,
                i.OfficialAnswerCount,
                i.BestQuestionCount,
                IsVerified = i.VerificationNote != null && i.VerificationNote != ""
            })
            .FirstOrDefaultAsync(ct);

        if (iama is null) return null;

        return new(Iama: new(
            iama.Phase,
            iama.ScheduledStartUtc,
            iama.ScheduledEndUtc,
            iama.OfficialAnswerCount,
            iama.BestQuestionCount,
            iama.IsVerified));
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetTrendingDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? cursor = null,
        string? userId = null,
        bool viewerAllowsAdult = false,
        CancellationToken ct = default)
    {
        var query = _context.Discussions.Where(d => !d.IsDeleted);

        query = await WithAccessFilterAsync(query, userId, ct);
        query = await WithAdultFilterAsync(query, viewerAllowsAdult, ct);

        if (!string.IsNullOrEmpty(communityId))
            query = query.Where(d => d.Space.Hub.Community.PublicId == communityId);

        // Only show discussions with recent activity (TrendScore > 0)
        query = query.Where(d => d.TrendScore > 0);

        var orderedQuery = query
            .OrderByDescending(d => d.TrendScore)
            .ThenByDescending(d => d.Id);

        var rawItems = await orderedQuery
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                d.PublicId,
                d.Title,
                d.Slug,
                d.Type,
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.PostCount,
                d.ReactionCount,
                d.Tags,
                d.IsAdultOnly,
                SpaceId = d.SpaceId,
                SpacePublicId = d.SpacePublicId,
                HubPublicId = d.HubPublicId,
                CommunityPublicId = d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId,
                AuthorDisplayName = d.AuthorDisplayName,
                AuthorAvatarFileName = d.AuthorAvatarFileName,
                AuthorAvatarThumbnailFileName = d.AuthorAvatarThumbnailFileName,
                LastPostAuthorPublicId = d.LastPostAuthorPublicId,
                LastPostAuthorDisplayName = d.LastPostAuthorDisplayName,
                LastPostAuthorAvatarFileName = d.LastPostAuthorAvatarFileName,
                LastPostAuthorAvatarThumbnailFileName = d.LastPostAuthorAvatarThumbnailFileName,
                LastPostPlainTextExcerpt = d.LastPostPlainTextExcerpt
            })
            .ToListAsync(ct);

        var spaceDisplay = await FetchSpaceDisplayAsync(rawItems.Select(x => x.SpaceId), ct);

        var items = rawItems.Select(d =>
        {
            var space = spaceDisplay.GetValueOrDefault(d.SpaceId);
            return new
            {
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
                d.SpacePublicId!,
                space?.Slug ?? "",
                space?.Name ?? "",
                d.HubPublicId!,
                space?.HubSlug ?? "",
                space?.HubName ?? "",
                d.CommunityPublicId,
                space?.CommunitySlug,
                space?.CommunityName,
                d.AuthorPublicId,
                d.AuthorDisplayName ?? "",
                d.AuthorAvatarFileName,
                d.AuthorAvatarThumbnailFileName,
                d.PostCount,
                d.ReactionCount,
                string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                LastReplierPublicId: d.LastPostAuthorPublicId,
                LastReplierDisplayName: d.LastPostAuthorDisplayName,
                LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                LastPostExcerpt: d.LastPostPlainTextExcerpt,
                IsAdult: d.IsAdultOnly)
            };
        }).ToList();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList(), ct);

        var finalItems = resultItems.Select(x =>
            previewMap.TryGetValue(x.Dto.PublicId, out var preview)
                ? x.Dto with { Preview = preview }
                : x.Dto).ToList();

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = finalItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = hasMoreItems ? null : null  // offset-based: no cursor
        };
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetTopDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? timePeriod = null,
        string? userId = null,
        bool viewerAllowsAdult = false,
        CancellationToken ct = default)
    {
        var query = _context.Discussions.AsQueryable();

        query = await WithAccessFilterAsync(query, userId, ct);
        query = await WithAdultFilterAsync(query, viewerAllowsAdult, ct);

        if (!string.IsNullOrEmpty(communityId))
            query = query.Where(d => d.Space.Hub.Community.PublicId == communityId);

        // Apply time window filter on CreatedAt
        DateTime? cutoff = timePeriod switch
        {
            "day"   => DateTime.UtcNow.AddDays(-1),
            "week"  => DateTime.UtcNow.AddDays(-7),
            "month" => DateTime.UtcNow.AddDays(-30),
            "year"  => DateTime.UtcNow.AddDays(-365),
            _       => null // "all_time" or unrecognised — no filter
        };

        if (cutoff.HasValue)
            query = query.Where(d => d.CreatedAt >= cutoff.Value);

        var orderedQuery = query
            .OrderByDescending(d => d.EngagementScore)
            .ThenByDescending(d => d.Id);

        var rawItems = await orderedQuery
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                d.PublicId,
                d.Title,
                d.Slug,
                d.Type,
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.PostCount,
                d.ReactionCount,
                d.Tags,
                d.IsAdultOnly,
                SpaceId = d.SpaceId,
                SpacePublicId = d.SpacePublicId,
                HubPublicId = d.HubPublicId,
                CommunityPublicId = d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId,
                AuthorDisplayName = d.AuthorDisplayName,
                AuthorAvatarFileName = d.AuthorAvatarFileName,
                AuthorAvatarThumbnailFileName = d.AuthorAvatarThumbnailFileName,
                LastPostAuthorPublicId = d.LastPostAuthorPublicId,
                LastPostAuthorDisplayName = d.LastPostAuthorDisplayName,
                LastPostAuthorAvatarFileName = d.LastPostAuthorAvatarFileName,
                LastPostAuthorAvatarThumbnailFileName = d.LastPostAuthorAvatarThumbnailFileName,
                LastPostPlainTextExcerpt = d.LastPostPlainTextExcerpt
            })
            .ToListAsync(ct);

        var spaceDisplay = await FetchSpaceDisplayAsync(rawItems.Select(x => x.SpaceId), ct);

        var items = rawItems.Select(d =>
        {
            var space = spaceDisplay.GetValueOrDefault(d.SpaceId);
            return new
            {
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
                d.SpacePublicId!,
                space?.Slug ?? "",
                space?.Name ?? "",
                d.HubPublicId!,
                space?.HubSlug ?? "",
                space?.HubName ?? "",
                d.CommunityPublicId,
                space?.CommunitySlug,
                space?.CommunityName,
                d.AuthorPublicId,
                d.AuthorDisplayName ?? "",
                d.AuthorAvatarFileName,
                d.AuthorAvatarThumbnailFileName,
                d.PostCount,
                d.ReactionCount,
                string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                LastReplierPublicId: d.LastPostAuthorPublicId,
                LastReplierDisplayName: d.LastPostAuthorDisplayName,
                LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                LastPostExcerpt: d.LastPostPlainTextExcerpt,
                IsAdult: d.IsAdultOnly)
            };
        }).ToList();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList(), ct);

        var finalItems = resultItems.Select(x =>
            previewMap.TryGetValue(x.Dto.PublicId, out var preview)
                ? x.Dto with { Preview = preview }
                : x.Dto).ToList();

        return new PagedResult<Application.Repositories.RecentDiscussionDto>
        {
            Items = finalItems,
            Offset = offset,
            PageSize = pageSize,
            HasMoreItems = hasMoreItems,
            NextCursor = null
        };
    }

    public async Task<PagedResult<Application.Repositories.RecentDiscussionDto>> GetNewDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? cursor = null,
        string? userId = null,
        bool viewerAllowsAdult = false,
        CancellationToken ct = default)
    {
        var query = _context.Discussions.AsQueryable();

        query = await WithAccessFilterAsync(query, userId, ct);
        query = await WithAdultFilterAsync(query, viewerAllowsAdult, ct);

        if (!string.IsNullOrEmpty(communityId))
            query = query.Where(d => d.Space.Hub.Community.PublicId == communityId);

        var cursorData = Cursor.Decode(cursor);

        if (cursorData.HasValue)
        {
            var (cursorDate, cursorId) = cursorData.Value;
            // ORDER BY CreatedAt DESC, Id DESC
            // Keyset: WHERE (CreatedAt < cursorDate) OR (CreatedAt = cursorDate AND Id < cursorId)
            query = query.Where(d =>
                d.CreatedAt < cursorDate
                || (d.CreatedAt == cursorDate && d.Id < cursorId));
        }

        var orderedQuery = query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id);

        if (!cursorData.HasValue)
            orderedQuery = (IOrderedQueryable<DiscussionDatabaseEntity>)orderedQuery.Skip(offset);

        var rawItems = await orderedQuery
            .Take(pageSize + 1)
            .Select(d => new {
                d.Id,
                d.PublicId,
                d.Title,
                d.Slug,
                d.Type,
                d.CreatedAt,
                d.LastActivityAt,
                d.IsPinned,
                d.IsLocked,
                d.PostCount,
                d.ReactionCount,
                d.Tags,
                d.IsAdultOnly,
                SpaceId = d.SpaceId,
                SpacePublicId = d.SpacePublicId,
                HubPublicId = d.HubPublicId,
                CommunityPublicId = d.CommunityPublicId,
                AuthorPublicId = d.CreatedByUserPublicId,
                AuthorDisplayName = d.AuthorDisplayName,
                AuthorAvatarFileName = d.AuthorAvatarFileName,
                AuthorAvatarThumbnailFileName = d.AuthorAvatarThumbnailFileName,
                LastPostAuthorPublicId = d.LastPostAuthorPublicId,
                LastPostAuthorDisplayName = d.LastPostAuthorDisplayName,
                LastPostAuthorAvatarFileName = d.LastPostAuthorAvatarFileName,
                LastPostAuthorAvatarThumbnailFileName = d.LastPostAuthorAvatarThumbnailFileName,
                LastPostPlainTextExcerpt = d.LastPostPlainTextExcerpt
            })
            .ToListAsync(ct);

        var spaceDisplay = await FetchSpaceDisplayAsync(rawItems.Select(x => x.SpaceId), ct);

        var items = rawItems.Select(d =>
        {
            var space = spaceDisplay.GetValueOrDefault(d.SpaceId);
            return new
            {
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
                d.SpacePublicId!,
                space?.Slug ?? "",
                space?.Name ?? "",
                d.HubPublicId!,
                space?.HubSlug ?? "",
                space?.HubName ?? "",
                d.CommunityPublicId,
                space?.CommunitySlug,
                space?.CommunityName,
                d.AuthorPublicId,
                d.AuthorDisplayName ?? "",
                d.AuthorAvatarFileName,
                d.AuthorAvatarThumbnailFileName,
                d.PostCount,
                d.ReactionCount,
                string.IsNullOrEmpty(d.Tags) ? Array.Empty<string>() : d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                LastReplierPublicId: d.LastPostAuthorPublicId,
                LastReplierDisplayName: d.LastPostAuthorDisplayName,
                LastReplierAvatarFileName: d.LastPostAuthorAvatarFileName,
                LastReplierAvatarThumbnailFileName: d.LastPostAuthorAvatarThumbnailFileName,
                LastPostExcerpt: d.LastPostPlainTextExcerpt,
                IsAdult: d.IsAdultOnly)
            };
        }).ToList();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize).ToList() : items;

        var previewMap = await BatchFetchPreviewsAsync(
            resultItems.Select(x => (x.Id, x.Dto.PublicId, x.Dto.Type)).ToList(), ct);

        var finalItems = resultItems.Select(x =>
            previewMap.TryGetValue(x.Dto.PublicId, out var preview)
                ? x.Dto with { Preview = preview }
                : x.Dto).ToList();

        string? nextCursor = null;
        if (hasMoreItems && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = Cursor.Encode(lastItem.Dto.CreatedAt, lastItem.Id);
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
}
