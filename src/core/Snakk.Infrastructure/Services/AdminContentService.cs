using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Services;

public class AdminContentService : IAdminContentService
{
    private readonly SnakkDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ISecurityService _securityService;
    private readonly ILogger<AdminContentService> _logger;

    public AdminContentService(
        SnakkDbContext context,
        IMemoryCache cache,
        ISecurityService securityService,
        ILogger<AdminContentService> logger)
    {
        _context = context;
        _cache = cache;
        _securityService = securityService;
        _logger = logger;
    }

    public async Task<ContentOverviewDto> GetContentOverviewAsync()
    {
        // Try cache first
        var cacheKey = "admin_content_overview";
        if (_cache.TryGetValue<ContentOverviewDto>(cacheKey, out var cachedOverview))
            return cachedOverview!;

        var overview = new ContentOverviewDto
        {
            TotalCommunities = await _context.Communities.CountAsync(),
            TotalHubs = await _context.Hubs.CountAsync(),
            TotalSpaces = await _context.Spaces.CountAsync(),
            TotalDiscussions = await _context.Discussions.CountAsync(),
            TotalPosts = await _context.Posts.CountAsync()
        };

        // Cache for 5 minutes
        _cache.Set(cacheKey, overview, TimeSpan.FromMinutes(5));

        return overview;
    }

    public async Task<PaginatedResponse<AdminCommunityDto>> GetCommunitiesAsync(int page, int pageSize, string? search)
    {
        var offset = (page - 1) * pageSize;

        var query = _context.Communities.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));
        }

        var total = await query.CountAsync();
        var communities = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
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

        return new PaginatedResponse<AdminCommunityDto>
        {
            Items = communities,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminHubDto>> GetHubsAsync(int page, int pageSize, string? search, string? communityId)
    {
        var offset = (page - 1) * pageSize;

        var query = _context.Hubs
            .Include(h => h.Community)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(communityId))
        {
            query = query.Where(h => h.Community.Slug == communityId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(h => h.Name.Contains(search) || h.Slug.Contains(search));
        }

        var total = await query.CountAsync();
        var hubs = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
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

        return new PaginatedResponse<AdminHubDto>
        {
            Items = hubs,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminSpaceDto>> GetSpacesAsync(int page, int pageSize, string? search, string? hubId)
    {
        var offset = (page - 1) * pageSize;

        var query = _context.Spaces
            .Include(s => s.Hub)
                .ThenInclude(h => h.Community)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(hubId))
        {
            query = query.Where(s => s.Hub.Slug == hubId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.Name.Contains(search) || s.Slug.Contains(search));
        }

        var total = await query.CountAsync();
        var spaces = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(s => new AdminSpaceDto
            {
                Slug = s.Slug,
                Name = s.Name,
                Description = s.Description,
                HubSlug = s.Hub.Slug,
                HubName = s.Hub.Name,
                CommunitySlug = s.Hub.Community.Slug,
                DiscussionCount = s.Discussions.Count,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponse<AdminSpaceDto>
        {
            Items = spaces,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResponse<AdminDiscussionDto>> GetDiscussionsAsync(int page, int pageSize, string? search, string? spaceId, bool? isPinned, bool? isLocked)
    {
        var offset = (page - 1) * pageSize;

        var query = _context.Discussions
            .Include(d => d.Space)
            .Include(d => d.CreatedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(spaceId))
        {
            query = query.Where(d => d.Space.Slug == spaceId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => d.Title.Contains(search));
        }

        if (isPinned.HasValue)
        {
            query = query.Where(d => d.IsPinned == isPinned.Value);
        }

        if (isLocked.HasValue)
        {
            query = query.Where(d => d.IsLocked == isLocked.Value);
        }

        var total = await query.CountAsync();
        var discussions = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(d => new AdminDiscussionDto
            {
                Slug = d.Slug,
                Title = d.Title,
                AuthorDisplayName = d.CreatedByUser.DisplayName,
                SpaceSlug = d.Space.Slug,
                SpaceName = d.Space.Name,
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                PostCount = d.PostCount,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResponse<AdminDiscussionDto>
        {
            Items = discussions,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> PinDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.Slug == id);
        if (discussion == null)
            return false;

        discussion.IsPinned = true;
        await _context.SaveChangesAsync();

        await _securityService.LogAuditAsync(
            adminUserId,
            "DiscussionPin",
            "Discussion",
            id,
            $"Pinned discussion: {discussion.Title}",
            "High");

        _logger.LogInformation("Discussion {DiscussionId} pinned by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> UnpinDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.Slug == id);
        if (discussion == null)
            return false;

        discussion.IsPinned = false;
        await _context.SaveChangesAsync();

        await _securityService.LogAuditAsync(
            adminUserId,
            "DiscussionUnpin",
            "Discussion",
            id,
            $"Unpinned discussion: {discussion.Title}",
            "Low");

        _logger.LogInformation("Discussion {DiscussionId} unpinned by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> LockDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.Slug == id);
        if (discussion == null)
            return false;

        discussion.IsLocked = true;
        await _context.SaveChangesAsync();

        await _securityService.LogAuditAsync(
            adminUserId,
            "DiscussionLock",
            "Discussion",
            id,
            $"Locked discussion: {discussion.Title}",
            "High");

        _logger.LogInformation("Discussion {DiscussionId} locked by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> UnlockDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.Slug == id);
        if (discussion == null)
            return false;

        discussion.IsLocked = false;
        await _context.SaveChangesAsync();

        await _securityService.LogAuditAsync(
            adminUserId,
            "DiscussionUnlock",
            "Discussion",
            id,
            $"Unlocked discussion: {discussion.Title}",
            "Low");

        _logger.LogInformation("Discussion {DiscussionId} unlocked by admin {AdminUserId}", id, adminUserId);

        return true;
    }

    public async Task<bool> DeleteDiscussionAsync(string id, string adminUserId)
    {
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.Slug == id);
        if (discussion == null)
            return false;

        var title = discussion.Title; // Store for audit log
        _context.Discussions.Remove(discussion);
        await _context.SaveChangesAsync();

        await _securityService.LogAuditAsync(
            adminUserId,
            "DiscussionDelete",
            "Discussion",
            id,
            $"Deleted discussion: {title}",
            "Critical");

        _logger.LogWarning("Discussion {DiscussionId} deleted by admin {AdminUserId}", id, adminUserId);

        return true;
    }
}
