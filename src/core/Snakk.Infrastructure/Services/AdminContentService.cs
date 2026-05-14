using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

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

    public async Task<ContentOverviewDto> GetContentOverviewAsync()
    {
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
            CacheOptions);
    }

    public async Task<PaginatedResponse<AdminCommunityDto>> GetCommunitiesAsync(
        int page,
        int pageSize,
        string? search)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Communities.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));
            return q.CountAsync();
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
                .ToListAsync();
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

    public async Task<PaginatedResponse<AdminHubDto>> GetHubsAsync(
        int page,
        int pageSize,
        string? search,
        string? communityId)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Hubs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(communityId))
                q = q.Where(h => h.Community.Slug == communityId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(h => h.Name.Contains(search) || h.Slug.Contains(search));
            return q.CountAsync();
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
                .ToListAsync();
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
        string? hubId)
    {
        var offset = (page - 1) * pageSize;

        var countTask = ReadAsync(db =>
        {
            var q = db.Spaces.AsQueryable();
            if (!string.IsNullOrWhiteSpace(hubId))
                q = q.Where(s => s.HubSlug == hubId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s => s.Name.Contains(search) || s.Slug.Contains(search));
            return q.CountAsync();
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
                .ToListAsync();
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
        bool? isLocked)
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
            return q.CountAsync();
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
                .ToListAsync();
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

    public async Task<bool> PinDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id);

        if (discussion is null)
            return false;

        discussion.IsPinned = true;
        await context.SaveChangesAsync();

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

    public async Task<bool> UnpinDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id);

        if (discussion is null)
            return false;

        discussion.IsPinned = false;
        await context.SaveChangesAsync();

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

    public async Task<bool> LockDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id);

        if (discussion is null)
            return false;

        discussion.IsLocked = true;
        await context.SaveChangesAsync();

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

    public async Task<bool> UnlockDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id);

        if (discussion is null)
            return false;

        discussion.IsLocked = false;
        await context.SaveChangesAsync();

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

    public async Task<bool> DeleteDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await context.Discussions.AsTracking().FirstOrDefaultAsync(d => d.Slug == id);

        if (discussion is null)
            return false;

        var title = discussion.Title; // Store for audit log
        context.Discussions.Remove(discussion);
        await context.SaveChangesAsync();

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
