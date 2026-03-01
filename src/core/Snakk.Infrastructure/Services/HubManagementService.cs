using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class HubManagementService : IHubManagementService
{
    private readonly SnakkDbContext _context;
    private readonly ILogger<HubManagementService> _logger;

    public HubManagementService(
        SnakkDbContext context,
        ILogger<HubManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HubOverviewDto?> GetOverviewAsync(string hubId, CancellationToken cancellationToken = default)
    {
        var hub = await _context.Hubs
            .Where(h => h.PublicId == hubId)
            .Include(h => h.Community)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub == null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);

        // Get activity stats
        var postsToday = await _context.Posts
            .Where(p => p.Discussion.Space.HubId == hub.Id && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await _context.Posts
            .Where(p => p.Discussion.Space.HubId == hub.Id && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        var newDiscussionsToday = await _context.Discussions
            .Where(d => d.Space.HubId == hub.Id && d.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var newDiscussionsThisWeek = await _context.Discussions
            .Where(d => d.Space.HubId == hub.Id && d.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get pending reports
        var pendingReports = await _context.Reports
            .Where(r => r.HubId == hub.Id && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        // Get moderators
        var moderators = await _context.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.HubMod &&
                         ur.HubId == hub.Id &&
                         ur.RevokedAt == null)
            .Include(ur => ur.User)
            .Select(ur => new HubModeratorDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                AssignedAt = ur.AssignedAt
            })
            .ToListAsync(cancellationToken);

        // Get recent activity
        var recentActivity = await _context.Posts
            .Where(p => p.Discussion.Space.HubId == hub.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new RecentActivityItemDto
            {
                Type = "post",
                Description = p.Discussion.Title,
                UserDisplayName = p.CreatedByUser.DisplayName,
                Timestamp = p.CreatedAt,
                LinkUrl = $"/c/{hub.Community.Slug}/s/{p.Discussion.Space.Slug}/d/{p.Discussion.Id}"
            })
            .ToListAsync(cancellationToken);

        return new HubOverviewDto
        {
            Id = hub.Id,
            Slug = hub.Slug,
            Name = hub.Name,
            Description = hub.Description,
            CommunitySlug = hub.Community.Slug,
            CommunityName = hub.Community.Name,
            CreatedAt = hub.CreatedAt,
            TotalSpaces = hub.SpaceCount,
            TotalDiscussions = hub.DiscussionCount,
            TotalPosts = hub.PostCount,
            PostsToday = postsToday,
            PostsThisWeek = postsThisWeek,
            NewDiscussionsToday = newDiscussionsToday,
            NewDiscussionsThisWeek = newDiscussionsThisWeek,
            PendingReports = pendingReports,
            Moderators = moderators,
            RecentActivity = recentActivity
        };
    }

    public async Task<HubSettingsDto?> GetSettingsAsync(string hubId, CancellationToken cancellationToken = default)
    {
        var hub = await _context.Hubs
            .Where(h => h.PublicId == hubId)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub == null)
            return null;

        var modUserIds = await _context.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.HubMod &&
                         ur.HubId == hub.Id &&
                         ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        return new HubSettingsDto
        {
            Id = hub.Id,
            Slug = hub.Slug,
            Name = hub.Name,
            Description = hub.Description,
            ModeratorUserIds = modUserIds
        };
    }

    public async Task<HubSettingsDto?> UpdateSettingsAsync(string hubId, UpdateHubSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var hub = await _context.Hubs
            .Where(h => h.PublicId == hubId)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub == null)
            return null;

        hub.Name = request.Name;
        hub.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(hubId, cancellationToken);
    }

    public async Task<HubModerationDto> GetModerationDataAsync(string hubId, CancellationToken cancellationToken = default)
    {
        var hubDbId = await _context.Hubs
            .Where(h => h.PublicId == hubId)
            .Select(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (hubDbId == 0)
            return new HubModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        // Get pending reports
        var pendingReports = await _context.Reports
            .Where(r => r.HubId == hubDbId && r.StatusId == (int)ReportStatusEnum.Pending)
            .Include(r => r.ReporterUser)
            .Include(r => r.ReportedUser)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ModerationReportDto
            {
                Id = r.Id,
                Type = r.ReportedPost != null ? "Post" : r.ReportedDiscussion != null ? "Discussion" : "User",
                Reason = r.Reason != null ? r.Reason.Name : "Other",
                Description = r.Details,
                ReportedByUserId = r.ReporterUser.PublicId,
                ReportedByDisplayName = r.ReporterUser.DisplayName,
                TargetUserId = r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                TargetUserDisplayName = r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
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
                Id = a.Id,
                ActionType = a.Action,
                ModeratorDisplayName = a.ActorUser != null ? a.ActorUser.DisplayName : "System",
                Reason = a.Reason ?? "",
                Timestamp = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Get stats
        var totalReports = await _context.Reports
            .Where(r => r.HubId == hubDbId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await _context.Reports
            .Where(r => r.HubId == hubDbId && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await _context.Reports
            .Where(r => r.HubId == hubDbId && r.StatusId == (int)ReportStatusEnum.Dismissed)
            .CountAsync(cancellationToken);

        var actionsThisWeek = recentActions.Count(a => a.Timestamp >= weekAgo);

        return new HubModerationDto
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

    public async Task<HubSpacesDto> GetSpacesAsync(string hubId, CancellationToken cancellationToken = default)
    {
        var hubDbId = await _context.Hubs
            .Where(h => h.PublicId == hubId)
            .Select(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var spaces = await _context.Spaces
            .Where(s => s.HubId == hubDbId)
            .Select(s => new HubSpaceItemDto
            {
                Id = s.Id,
                Slug = s.Slug,
                Name = s.Name,
                Description = s.Description,
                DiscussionCount = s.DiscussionCount,
                PostCount = s.PostCount,
                CreatedAt = s.CreatedAt,
                IsActive = true
            })
            .ToListAsync(cancellationToken);

        return new HubSpacesDto
        {
            Spaces = spaces,
            Total = spaces.Count
        };
    }

    public async Task<HubRulesDto> GetRulesAsync(string hubId, CancellationToken cancellationToken = default)
    {
        var rules = await _context.HubRules
            .Where(r => r.Hub.PublicId == hubId)
            .OrderBy(r => r.SortOrder)
            .Select(r => new HubRuleDto
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Order = r.SortOrder
            })
            .ToListAsync(cancellationToken);

        return new HubRulesDto { Rules = rules };
    }

    public async Task<HubRulesDto> UpdateRulesAsync(string hubId, UpdateHubRulesRequest request, CancellationToken cancellationToken = default)
    {
        var hub = await _context.Hubs
            .Where(h => h.PublicId == hubId)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub == null)
            return new HubRulesDto { Rules = new List<HubRuleDto>() };

        var existingRules = await _context.HubRules
            .Where(r => r.HubId == hub.Id)
            .ToListAsync(cancellationToken);

        _context.HubRules.RemoveRange(existingRules);

        var now = DateTime.UtcNow;
        var newRules = request.Rules.Select((r, index) => new HubRuleDatabaseEntity
        {
            HubId = hub.Id,
            Title = r.Title,
            Description = r.Description,
            SortOrder = index,
            CreatedAt = now
        }).ToList();

        _context.HubRules.AddRange(newRules);

        // Update denormalized fields
        hub.HasRules = newRules.Count > 0;
        hub.RulesRevision = Guid.NewGuid().ToString("N")[..8];

        // Cascade: update ParentHubHasRules on all child spaces
        await _context.Spaces
            .Where(s => s.HubId == hub.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentHubHasRules, newRules.Count > 0), cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRulesAsync(hubId, cancellationToken);
    }
}
