using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class SpaceManagementService : ISpaceManagementService
{
    private readonly SnakkDbContext _context;
    private readonly ILogger<SpaceManagementService> _logger;

    public SpaceManagementService(
        SnakkDbContext context,
        ILogger<SpaceManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SpaceOverviewDto?> GetOverviewAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        var space = await _context.Spaces
            .Where(s => s.PublicId == spaceId)
            .Include(s => s.Hub)
            .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(cancellationToken);

        if (space == null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);

        var followers = await _context.Follows
            .Where(f => f.SpaceId == space.Id)
            .CountAsync(cancellationToken);

        // Get activity stats
        var postsToday = await _context.Posts
            .Where(p => p.Discussion.SpaceId == space.Id && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await _context.Posts
            .Where(p => p.Discussion.SpaceId == space.Id && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        var newDiscussionsToday = await _context.Discussions
            .Where(d => d.SpaceId == space.Id && d.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var newDiscussionsThisWeek = await _context.Discussions
            .Where(d => d.SpaceId == space.Id && d.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get pending reports
        var pendingReports = await _context.Reports
            .Where(r => r.SpaceId == space.Id && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        // Get moderators
        var moderators = await _context.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.SpaceMod &&
                         ur.SpaceId == space.Id &&
                         ur.RevokedAt == null)
            .Select(ur => new SpaceModeratorDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                AssignedAt = ur.AssignedAt
            })
            .ToListAsync(cancellationToken);

        // Get recent activity
        var recentActivity = await _context.Posts
            .Where(p => p.Discussion.SpaceId == space.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new RecentActivityItemDto
            {
                Type = "post",
                Description = p.Discussion.Title,
                UserDisplayName = p.CreatedByUser.DisplayName,
                Timestamp = p.CreatedAt,
                LinkUrl = $"/c/{space.Hub.Community.Slug}/s/{space.Slug}/d/{p.Discussion.Id}"
            })
            .ToListAsync(cancellationToken);

        return new SpaceOverviewDto
        {
            Slug = space.Slug,
            Name = space.Name,
            Description = space.Description,
            CommunitySlug = space.Hub.Community.Slug,
            CommunityName = space.Hub.Community.Name,
            HubSlug = space.Hub.Slug,
            HubName = space.Hub.Name,
            CreatedAt = space.CreatedAt,
            TotalDiscussions = space.DiscussionCount,
            TotalPosts = space.PostCount,
            Followers = followers,
            PostsToday = postsToday,
            PostsThisWeek = postsThisWeek,
            NewDiscussionsToday = newDiscussionsToday,
            NewDiscussionsThisWeek = newDiscussionsThisWeek,
            PendingReports = pendingReports,
            Moderators = moderators,
            RecentActivity = recentActivity
        };
    }

    public async Task<SpaceSettingsDto?> GetSettingsAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        var space = await _context.Spaces
            .Where(s => s.PublicId == spaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space == null)
            return null;

        var modUserIds = await _context.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.SpaceMod &&
                         ur.SpaceId == space.Id &&
                         ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        return new SpaceSettingsDto
        {
            Slug = space.Slug,
            Name = space.Name,
            Description = space.Description,
            AllowedThreadTypes = new List<string> { "Discussion", "Question" }, // TODO: Store in database
            ModeratorUserIds = modUserIds
        };
    }

    public async Task<SpaceSettingsDto?> UpdateSettingsAsync(string spaceId, UpdateSpaceSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var space = await _context.Spaces
            .AsTracking()
            .Where(s => s.PublicId == spaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space == null)
            return null;

        space.Name = request.Name;
        space.Description = request.Description;
        // TODO: Store AllowedThreadTypes in database

        await _context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(spaceId, cancellationToken);
    }

    public async Task<SpaceModerationDto> GetModerationDataAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        var spaceDbId = await _context.Spaces
            .Where(s => s.PublicId == spaceId)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (spaceDbId == 0)
            return new SpaceModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        // Get pending reports
        var pendingReports = await _context.Reports
            .Where(r => r.SpaceId == spaceDbId && r.StatusId == (int)ReportStatusEnum.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ModerationReportDto
            {
                PublicId = r.PublicId,
                Type = r.ReportedPost != null ? "Post" : r.ReportedDiscussion != null ? "Discussion" : "User",
                Reason = r.Reason != null ? r.Reason.Name : "Other",
                Description = r.Details,
                ReportedByUserId = r.ReporterUser.PublicId,
                ReportedByDisplayName = r.ReporterUser.DisplayName,
                TargetUserId = r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                TargetUserDisplayName = r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                TargetPostPublicId = r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                TargetDiscussionPublicId = r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
                Status = ((ReportStatusEnum)r.StatusId).ToString(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Get recent moderation actions
        var recentActions = await _context.AuditLogs
            .Where(a => a.Category == "Moderation" &&
                        (a.Action.Contains("Ban") || a.Action.Contains("Delete") || a.Action.Contains("Moderate")))
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .Select(a => new ModerationActionDto
            {
                PublicId = a.PublicId,
                ActionType = a.Action,
                ModeratorDisplayName = a.ActorUser != null ? a.ActorUser.DisplayName : "System",
                Reason = a.Reason ?? "",
                Timestamp = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Get stats
        var totalReports = await _context.Reports
            .Where(r => r.SpaceId == spaceDbId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await _context.Reports
            .Where(r => r.SpaceId == spaceDbId && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await _context.Reports
            .Where(r => r.SpaceId == spaceDbId && r.StatusId == (int)ReportStatusEnum.Dismissed)
            .CountAsync(cancellationToken);

        var actionsThisWeek = recentActions.Count(a => a.Timestamp >= weekAgo);

        return new SpaceModerationDto
        {
            PendingReports = pendingReports,
            RecentActions = recentActions,
            Stats = new ModerationStatsDto
            {
                TotalReports = totalReports,
                PendingReports = pendingCount,
                ResolvedReports = resolvedCount,
                DismissedReports = dismissedCount,
                ActionsThisWeek = actionsThisWeek
            }
        };
    }

    public async Task<SpaceRulesDto> GetRulesAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        var rules = await _context.SpaceRules
            .Where(r => r.Space.PublicId == spaceId)
            .OrderBy(r => r.SortOrder)
            .Select(r => new SpaceRuleDto
            {
                Title = r.Title,
                Description = r.Description,
                Order = r.SortOrder
            })
            .ToListAsync(cancellationToken);

        return new SpaceRulesDto { Rules = rules };
    }

    public async Task<SpaceRulesDto> UpdateRulesAsync(string spaceId, UpdateSpaceRulesRequest request, CancellationToken cancellationToken = default)
    {
        var space = await _context.Spaces
            .AsTracking()
            .Where(s => s.PublicId == spaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space == null)
            return new SpaceRulesDto { Rules = new List<SpaceRuleDto>() };

        var existingRules = await _context.SpaceRules
            .Where(r => r.SpaceId == space.Id)
            .ToListAsync(cancellationToken);

        _context.SpaceRules.RemoveRange(existingRules);

        var now = DateTime.UtcNow;
        var newRules = request.Rules.Select((r, index) => new SpaceRuleDatabaseEntity
        {
            SpaceId = space.Id,
            Title = r.Title,
            Description = r.Description,
            SortOrder = index,
            CreatedAt = now
        }).ToList();

        _context.SpaceRules.AddRange(newRules);

        // Update denormalized fields
        space.HasRules = newRules.Count > 0;
        space.RulesRevision = Guid.NewGuid().ToString("N")[..8];

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRulesAsync(spaceId, cancellationToken);
    }
}
