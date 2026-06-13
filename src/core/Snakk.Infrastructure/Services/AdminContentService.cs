using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class AdminContentService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory,
    HybridCache cache,
    ISecurityService securityService,
    ILogger<AdminContentService> logger) : IAdminContentService
{
    private static readonly HybridCacheEntryOptions CacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    public async Task<ContentOverviewDto> GetContentOverviewAsync(CancellationToken ct = default)
    {
        // Aggregate: TTL-only, no write-path invalidation. These site-wide counts change
        // on every organic post/discussion/space creation — covering all mutation paths is
        // impractical, and a 5-minute-stale dashboard count is acceptable.
        var cacheKey = "admin_content_overview";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var commTask  = ReadAsync(db => db.Communities.CountAsync(cancel));
                var hubTask   = ReadAsync(db => db.Hubs.CountAsync(cancel));
                var spaceTask = ReadAsync(db => db.Spaces.CountAsync(cancel));
                var discTask  = ReadAsync(db => db.Discussions.CountAsync(cancel));
                var postTask  = ReadAsync(db => db.Posts.CountAsync(cancel));
                await Task.WhenAll(commTask, hubTask, spaceTask, discTask, postTask);
                return new ContentOverviewDto
                {
                    TotalCommunities = commTask.Result,
                    TotalHubs = hubTask.Result,
                    TotalSpaces = spaceTask.Result,
                    TotalDiscussions = discTask.Result,
                    TotalPosts = postTask.Result
                };
            },
            CacheOptions,
            cancellationToken: ct);
    }

    public async Task<ContentOverviewExtendedDto> GetContentOverviewExtendedAsync(CancellationToken ct = default)
    {
        // Aggregate: TTL-only, no write-path invalidation — same rationale as GetContentOverviewAsync.
        var cacheKey = "admin_content_overview_extended";

        return await cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var commTask    = ReadAsync(db => db.Communities.CountAsync(x => !x.IsDeleted, cancel));
                var hubTask     = ReadAsync(db => db.Hubs.CountAsync(x => !x.IsDeleted, cancel));
                var spaceTask   = ReadAsync(db => db.Spaces.CountAsync(x => !x.IsDeleted, cancel));
                var discTask    = ReadAsync(db => db.Discussions.CountAsync(x => !x.IsDeleted, cancel));
                var postTask    = ReadAsync(db => db.Posts.CountAsync(x => !x.IsDeleted, cancel));
                var pinnedTask  = ReadAsync(db => db.Discussions.CountAsync(x => !x.IsDeleted && x.IsPinned, cancel));
                var lockedTask  = ReadAsync(db => db.Discussions.CountAsync(x => !x.IsDeleted && x.IsLocked, cancel));
                await Task.WhenAll(commTask, hubTask, spaceTask, discTask, postTask, pinnedTask, lockedTask);
                return new ContentOverviewExtendedDto
                {
                    TotalCommunities = commTask.Result,
                    TotalHubs = hubTask.Result,
                    TotalSpaces = spaceTask.Result,
                    TotalDiscussions = discTask.Result,
                    TotalPosts = postTask.Result,
                    PinnedDiscussions = pinnedTask.Result,
                    LockedDiscussions = lockedTask.Result
                };
            },
            CacheOptions,
            cancellationToken: ct);
    }

    public async Task<PaginatedResponse<AdminCommunityDto>> GetCommunitiesAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Communities.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Communities.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));
            return q.OrderByDescending(c => c.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(c => new AdminCommunityDto
                {
                    Slug = c.Slug,
                    Name = c.Name,
                    Description = c.Description,
                    MemberCount = 0, // TODO: Add member tracking
                    HubCount = c.Hubs.Count,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);

        return new PaginatedResponse<AdminCommunityDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminCommunityDetailDto?> GetCommunityAsync(string publicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Communities
            .Where(c => c.PublicId == publicId && !c.IsDeleted)
            .Select(c => new AdminCommunityDetailDto
            {
                PublicId = c.PublicId,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                Visibility = ((CommunityVisibilityEnum)c.VisibilityId).ToString(),
                CreatedAt = c.CreatedAt,
                HubCount = c.HubCount,
                MemberCount = 0,
                Hubs = db.Hubs
                    .Where(h => h.CommunityId == c.Id && !h.IsDeleted)
                    .Select(h => new AdminHubSummaryDto
                    {
                        PublicId = h.PublicId,
                        Name = h.Name,
                        Slug = h.Slug,
                        SpaceCount = h.SpaceCount
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResponse<AdminHubDto>> GetHubsAsync(
        int page,
        int pageSize,
        string? search,
        string? communityId,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Hubs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(communityId))
                q = q.Where(h => h.Community.Slug == communityId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(h => h.Name.Contains(search) || h.Slug.Contains(search));
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Hubs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(communityId))
                q = q.Where(h => h.Community.Slug == communityId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(h => h.Name.Contains(search) || h.Slug.Contains(search));
            return q.OrderByDescending(h => h.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(h => new AdminHubDto
                {
                    Slug = h.Slug,
                    Name = h.Name,
                    Description = h.Description,
                    CommunitySlug = h.Community.Slug,
                    CommunityName = h.Community.Name,
                    SpaceCount = h.Spaces.Count,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);

        return new PaginatedResponse<AdminHubDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminSpaceDto>> GetSpacesAsync(
        int page,
        int pageSize,
        string? search,
        string? hubId,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Spaces.AsQueryable();
            if (!string.IsNullOrWhiteSpace(hubId))
                q = q.Where(s => s.HubSlug == hubId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s => s.Name.Contains(search) || s.Slug.Contains(search));
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Spaces.AsQueryable();
            if (!string.IsNullOrWhiteSpace(hubId))
                q = q.Where(s => s.HubSlug == hubId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s => s.Name.Contains(search) || s.Slug.Contains(search));
            return q.OrderByDescending(s => s.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(s => new AdminSpaceDto
                {
                    Slug = s.Slug,
                    Name = s.Name,
                    Description = s.Description,
                    HubSlug = s.HubSlug,
                    HubName = s.HubName,
                    CommunitySlug = s.CommunitySlug,
                    DiscussionCount = s.Discussions.Count,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);

        return new PaginatedResponse<AdminSpaceDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminDiscussionDto>> GetDiscussionsAsync(
        int page,
        int pageSize,
        string? search,
        string? spaceId,
        bool? isPinned,
        bool? isLocked,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Discussions.AsQueryable();
            if (!string.IsNullOrWhiteSpace(spaceId))
                q = q.Where(d => d.Space.Slug == spaceId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(d => d.Title.Contains(search));
            if (isPinned.HasValue)
                q = q.Where(d => d.IsPinned == isPinned.Value);
            if (isLocked.HasValue)
                q = q.Where(d => d.IsLocked == isLocked.Value);
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Discussions.AsQueryable();
            if (!string.IsNullOrWhiteSpace(spaceId))
                q = q.Where(d => d.Space.Slug == spaceId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(d => d.Title.Contains(search));
            if (isPinned.HasValue)
                q = q.Where(d => d.IsPinned == isPinned.Value);
            if (isLocked.HasValue)
                q = q.Where(d => d.IsLocked == isLocked.Value);
            return q.OrderByDescending(d => d.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(d => new AdminDiscussionDto
                {
                    Slug = d.Slug,
                    Title = d.Title,
                    AuthorDisplayName = d.CreatedByUser.DisplayName ?? "",
                    SpaceSlug = d.Space.Slug,
                    SpaceName = d.Space.Name,
                    IsPinned = d.IsPinned,
                    IsLocked = d.IsLocked,
                    PostCount = d.PostCount,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);

        return new PaginatedResponse<AdminDiscussionDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminDiscussionDetailDto?> GetDiscussionAsync(string publicId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        return await db.Discussions
            .Where(d => d.PublicId == publicId && !d.IsDeleted)
            .Select(d => new AdminDiscussionDetailDto
            {
                PublicId = d.PublicId,
                Title = d.Title,
                Slug = d.Slug,
                SpacePublicId = d.Space.PublicId,
                SpaceName = d.Space.Name,
                HubPublicId = d.Space.Hub.PublicId,
                HubName = d.Space.Hub.Name,
                CommunityPublicId = d.Space.Hub.Community.PublicId,
                CommunityName = d.Space.Hub.Community.Name,
                AuthorPublicId = d.CreatedByUser.PublicId,
                AuthorDisplayName = d.CreatedByUser.DisplayName ?? "",
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                Tags = d.Tags,
                CreatedAt = d.CreatedAt,
                LastActivityAt = d.LastActivityAt
            })
            .FirstOrDefaultAsync(ct);
    }

    // ===== Mutations by publicId (used by admin REST endpoints) =====

    public async Task<AdminDiscussionMutationResult> PinDiscussionByPublicIdAsync(
        string publicId, string actorPublicId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId && !d.IsDeleted, ct);

        if (discussion is null)
            return AdminDiscussionMutationResult.NotFound();

        if (discussion.IsPinned)
            return AdminDiscussionMutationResult.AlreadyInState("Discussion is already pinned");

        discussion.IsPinned = true;
        await AppendModerationLogAsync(actorPublicId, ModerationActionEnum.PinDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Discussion {PublicId} pinned by admin {ActorPublicId}", publicId, actorPublicId);
        return AdminDiscussionMutationResult.Ok();
    }

    public async Task<AdminDiscussionMutationResult> UnpinDiscussionByPublicIdAsync(
        string publicId, string actorPublicId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId && !d.IsDeleted, ct);

        if (discussion is null)
            return AdminDiscussionMutationResult.NotFound();

        if (!discussion.IsPinned)
            return AdminDiscussionMutationResult.AlreadyInState("Discussion is not pinned");

        discussion.IsPinned = false;
        await AppendModerationLogAsync(actorPublicId, ModerationActionEnum.UnpinDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Discussion {PublicId} unpinned by admin {ActorPublicId}", publicId, actorPublicId);
        return AdminDiscussionMutationResult.Ok();
    }

    public async Task<AdminDiscussionMutationResult> LockDiscussionByPublicIdAsync(
        string publicId, string actorPublicId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId && !d.IsDeleted, ct);

        if (discussion is null)
            return AdminDiscussionMutationResult.NotFound();

        if (discussion.IsLocked)
            return AdminDiscussionMutationResult.AlreadyInState("Discussion is already locked");

        discussion.IsLocked = true;
        await AppendModerationLogAsync(actorPublicId, ModerationActionEnum.LockDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Discussion {PublicId} locked by admin {ActorPublicId}", publicId, actorPublicId);
        return AdminDiscussionMutationResult.Ok();
    }

    public async Task<AdminDiscussionMutationResult> UnlockDiscussionByPublicIdAsync(
        string publicId, string actorPublicId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId && !d.IsDeleted, ct);

        if (discussion is null)
            return AdminDiscussionMutationResult.NotFound();

        if (!discussion.IsLocked)
            return AdminDiscussionMutationResult.AlreadyInState("Discussion is not locked");

        discussion.IsLocked = false;
        await AppendModerationLogAsync(actorPublicId, ModerationActionEnum.UnlockDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Discussion {PublicId} unlocked by admin {ActorPublicId}", publicId, actorPublicId);
        return AdminDiscussionMutationResult.Ok();
    }

    public async Task<AdminDiscussionMutationResult> SoftDeleteDiscussionByPublicIdAsync(
        string publicId, string actorPublicId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId && !d.IsDeleted, ct);

        if (discussion is null)
            return AdminDiscussionMutationResult.NotFound();

        discussion.IsDeleted = true;
        discussion.DeletedAt = DateTime.UtcNow;
        await AppendModerationLogAsync(actorPublicId, ModerationActionEnum.DeleteDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        // The soft-deleted discussion may be the cached "latest" for its space — the entry
        // has a very long TTL (write-invalidated), and the latest-discussion query filters on
        // NOT IsDeleted, so a missed removal here would persist a stale result for months.
        await cache.RemoveAsync($"space-latest-discussion:{discussion.SpaceId}", ct);

        logger.LogWarning("Discussion {PublicId} soft-deleted by admin {ActorPublicId}", publicId, actorPublicId);
        return AdminDiscussionMutationResult.Ok();
    }

    // ===== Internal helper: write a moderation log entry =====

    private async Task AppendModerationLogAsync(
        string actorPublicId,
        ModerationActionEnum action,
        DiscussionDatabaseEntity discussion,
        CancellationToken ct)
    {
        var actorId = await context.Users
            .Where(u => u.PublicId == actorPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (actorId is null)
            return;

        context.ModerationLogs.Add(new ModerationLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            ActorUserId = actorId.Value,
            ActionId = (int)action,
            TargetDiscussionId = discussion.Id,
            CommunityId = discussion.CommunityId,
            HubId = discussion.HubId,
            SpaceId = discussion.SpaceId,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ===== PublicId-based paged queries (used by admin REST endpoints) =====

    public async Task<PaginatedResponse<AdminCommunityItemDto>> GetCommunitiesPagedByPublicIdAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Communities.Where(c => !c.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(c => c.Name.ToLower().Contains(s) || c.Slug.ToLower().Contains(s));
            }
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Communities.Where(c => !c.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(c => c.Name.ToLower().Contains(s) || c.Slug.ToLower().Contains(s));
            }
            return q.OrderByDescending(c => c.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(c => new AdminCommunityItemDto
                {
                    PublicId = c.PublicId,
                    Name = c.Name,
                    Slug = c.Slug,
                    Description = c.Description,
                    Visibility = ((CommunityVisibilityEnum)c.VisibilityId).ToString(),
                    CreatedAt = c.CreatedAt,
                    HubCount = c.HubCount,
                    MemberCount = 0
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);
        return new PaginatedResponse<AdminCommunityItemDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminHubItemDto>> GetHubsPagedByPublicIdAsync(
        int page, int pageSize, string? search, string? communityPublicId, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Hubs.Where(h => !h.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(h => h.Name.ToLower().Contains(s) || h.Slug.ToLower().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(communityPublicId))
                q = q.Where(h => h.Community.PublicId == communityPublicId);
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Hubs.Where(h => !h.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(h => h.Name.ToLower().Contains(s) || h.Slug.ToLower().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(communityPublicId))
                q = q.Where(h => h.Community.PublicId == communityPublicId);
            return q.OrderByDescending(h => h.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(h => new AdminHubItemDto
                {
                    PublicId = h.PublicId,
                    Name = h.Name,
                    Slug = h.Slug,
                    Description = h.Description,
                    CommunityPublicId = h.Community.PublicId,
                    CommunityName = h.Community.Name,
                    CreatedAt = h.CreatedAt,
                    SpaceCount = h.SpaceCount
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);
        return new PaginatedResponse<AdminHubItemDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminSpaceItemDto>> GetSpacesPagedByPublicIdAsync(
        int page, int pageSize, string? search, string? hubPublicId, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Spaces.Where(s => !s.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s2 = search.ToLower();
                q = q.Where(s => s.Name.ToLower().Contains(s2) || s.Slug.ToLower().Contains(s2));
            }
            if (!string.IsNullOrWhiteSpace(hubPublicId))
                q = q.Where(s => s.Hub.PublicId == hubPublicId);
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Spaces.Where(s => !s.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s2 = search.ToLower();
                q = q.Where(s => s.Name.ToLower().Contains(s2) || s.Slug.ToLower().Contains(s2));
            }
            if (!string.IsNullOrWhiteSpace(hubPublicId))
                q = q.Where(s => s.Hub.PublicId == hubPublicId);
            return q.OrderByDescending(s => s.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(s => new AdminSpaceItemDto
                {
                    PublicId = s.PublicId,
                    Name = s.Name,
                    Slug = s.Slug,
                    Description = s.Description,
                    HubPublicId = s.Hub.PublicId,
                    HubName = s.Hub.Name,
                    CommunityPublicId = s.Hub.Community.PublicId,
                    CommunityName = s.Hub.Community.Name,
                    CreatedAt = s.CreatedAt,
                    DiscussionCount = s.DiscussionCount
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);
        return new PaginatedResponse<AdminSpaceItemDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminDiscussionItemDto>> GetDiscussionsPagedByPublicIdAsync(
        int page,
        int pageSize,
        string? search,
        string? spacePublicId,
        bool? isPinned,
        bool? isLocked,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Discussions.Where(d => !d.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(d => d.Title.ToLower().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(spacePublicId))
                q = q.Where(d => d.Space.PublicId == spacePublicId);
            if (isPinned.HasValue)
                q = q.Where(d => d.IsPinned == isPinned.Value);
            if (isLocked.HasValue)
                q = q.Where(d => d.IsLocked == isLocked.Value);
            return q.CountAsync(ct);
        });

        var listTask = ReadAsync(db =>
        {
            var q = db.Discussions.Where(d => !d.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(d => d.Title.ToLower().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(spacePublicId))
                q = q.Where(d => d.Space.PublicId == spacePublicId);
            if (isPinned.HasValue)
                q = q.Where(d => d.IsPinned == isPinned.Value);
            if (isLocked.HasValue)
                q = q.Where(d => d.IsLocked == isLocked.Value);
            return q.OrderByDescending(d => d.CreatedAt)
                .Skip(offset).Take(pageSize)
                .Select(d => new AdminDiscussionItemDto
                {
                    PublicId = d.PublicId,
                    Title = d.Title,
                    Slug = d.Slug,
                    SpacePublicId = d.Space.PublicId,
                    SpaceName = d.Space.Name,
                    HubPublicId = d.Space.Hub.PublicId,
                    HubName = d.Space.Hub.Name,
                    CommunityPublicId = d.Space.Hub.Community.PublicId,
                    CommunityName = d.Space.Hub.Community.Name,
                    AuthorPublicId = d.CreatedByUser.PublicId,
                    AuthorDisplayName = d.CreatedByUser.DisplayName ?? "",
                    PostCount = d.PostCount,
                    IsPinned = d.IsPinned,
                    IsLocked = d.IsLocked,
                    CreatedAt = d.CreatedAt,
                    LastActivityAt = d.LastActivityAt
                })
                .ToListAsync(ct);
        });

        await Task.WhenAll(countTask, listTask);
        return new PaginatedResponse<AdminDiscussionItemDto>
        {
            Items = listTask.Result,
            Total = countTask.Result,
            Page = page,
            PageSize = pageSize
        };
    }

    // ===== Legacy slug-based mutations (original callers) =====

    public async Task<bool> PinDiscussionAsync(string id, string adminUserId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id, ct);

        if (discussion is null)
            return false;

        discussion.IsPinned = true;
        await context.SaveChangesAsync(ct);

        await securityService.LogAuditAsync(
            adminUserId,
            "DiscussionPin",
            "Discussion",
            id,
            $"Pinned discussion: {discussion.Title}",
            "High");

        logger.LogInformation("Discussion {DiscussionId} pinned by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> UnpinDiscussionAsync(string id, string adminUserId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id, ct);

        if (discussion is null)
            return false;

        discussion.IsPinned = false;
        await context.SaveChangesAsync(ct);

        await securityService.LogAuditAsync(
            adminUserId,
            "DiscussionUnpin",
            "Discussion",
            id,
            $"Unpinned discussion: {discussion.Title}",
            "Low");

        logger.LogInformation("Discussion {DiscussionId} unpinned by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> LockDiscussionAsync(string id, string adminUserId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id, ct);

        if (discussion is null)
            return false;

        discussion.IsLocked = true;
        await context.SaveChangesAsync(ct);

        await securityService.LogAuditAsync(
            adminUserId,
            "DiscussionLock",
            "Discussion",
            id,
            $"Locked discussion: {discussion.Title}",
            "High");

        logger.LogInformation("Discussion {DiscussionId} locked by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> UnlockDiscussionAsync(string id, string adminUserId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id, ct);

        if (discussion is null)
            return false;

        discussion.IsLocked = false;
        await context.SaveChangesAsync(ct);

        await securityService.LogAuditAsync(
            adminUserId,
            "DiscussionUnlock",
            "Discussion",
            id,
            $"Unlocked discussion: {discussion.Title}",
            "Low");

        logger.LogInformation("Discussion {DiscussionId} unlocked by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> DeleteDiscussionAsync(string id, string adminUserId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id, ct);

        if (discussion is null)
            return false;

        var title = discussion.Title; // Store for audit log
        var spaceId = discussion.SpaceId;
        context.Discussions.Remove(discussion);
        await context.SaveChangesAsync(ct);

        // The deleted discussion may be the cached "latest" for its space — the entry has a
        // very long TTL (write-invalidated), so a missed removal here would persist for months.
        await cache.RemoveAsync($"space-latest-discussion:{spaceId}", ct);

        await securityService.LogAuditAsync(
            adminUserId,
            "DiscussionDelete",
            "Discussion",
            id,
            $"Deleted discussion: {title}",
            "Critical");

        logger.LogWarning("Discussion {DiscussionId} deleted by admin {AdminUserId}", id, adminUserId);

        return true;
    }
}
