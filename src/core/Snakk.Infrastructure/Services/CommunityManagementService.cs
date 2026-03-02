using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class CommunityManagementService : ICommunityManagementService
{
    private readonly SnakkDbContext _context;
    private readonly ILogger<CommunityManagementService> _logger;

    public CommunityManagementService(
        SnakkDbContext context,
        ILogger<CommunityManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CommunityOverviewDto?> GetOverviewAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.PublicId == communityId)
            .Select(c => new
            {
                c.Id,
                c.Slug,
                c.Name,
                c.Description,
                c.CreatedAt,
                c.HubCount,
                c.SpaceCount,
                c.DiscussionCount,
                c.PostCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);

        // Get activity stats - posts
        var postsToday = await _context.Posts
            .Where(p => p.Discussion.Space.Hub.CommunityId == community.Id && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await _context.Posts
            .Where(p => p.Discussion.Space.Hub.CommunityId == community.Id && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get moderation stats
        var pendingReports = await _context.Reports
            .Where(r => r.CommunityId == community.Id && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        var activeBans = await _context.UserBans
            .Where(ub => ub.CommunityId == community.Id &&
                         ub.UnbannedAt == null &&
                         (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .CountAsync(cancellationToken);

        // Get team members
        var admins = await _context.UserRoles
            .Where(ur => (ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin || ur.RoleId == (int)UserRoleTypeEnum.CommunityMod) &&
                         ur.CommunityId == community.Id &&
                         ur.RevokedAt == null)
            .Select(ur => new CommunityMemberDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                JoinedAt = ur.AssignedAt,
                Roles = new List<string> { ((UserRoleTypeEnum)ur.RoleId).ToString() }
            })
            .ToListAsync(cancellationToken);

        // Get recent activity
        var recentActivity = await _context.Posts
            .Where(p => p.Discussion.Space.Hub.CommunityId == community.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new RecentActivityItemDto
            {
                Type = "post",
                Description = p.Discussion.Title,
                UserDisplayName = p.CreatedByUser.DisplayName,
                Timestamp = p.CreatedAt,
                LinkUrl = $"/c/{community.Slug}/s/{p.Discussion.Space.Slug}/d/{p.Discussion.Id}"
            })
            .ToListAsync(cancellationToken);

        var adminList = admins.Where(a => a.Roles.Contains("CommunityAdmin")).ToList();
        var modList = admins.Where(a => a.Roles.Contains("CommunityMod")).ToList();

        return new CommunityOverviewDto
        {
            Slug = community.Slug,
            Name = community.Name,
            Description = community.Description,
            CreatedAt = community.CreatedAt,
            TotalMembers = 0, // TODO: Implement member tracking
            TotalHubs = community.HubCount,
            TotalSpaces = community.SpaceCount,
            TotalDiscussions = community.DiscussionCount,
            TotalPosts = community.PostCount,
            NewMembersToday = 0, // TODO: Implement member tracking
            NewMembersThisWeek = 0, // TODO: Implement member tracking
            PostsToday = postsToday,
            PostsThisWeek = postsThisWeek,
            PendingReports = pendingReports,
            ActiveBans = activeBans,
            Admins = adminList,
            Moderators = modList,
            RecentActivity = recentActivity
        };
    }

    public async Task<CommunitySettingsDto?> GetSettingsAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return null;

        var adminUserIds = await _context.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin &&
                         ur.CommunityId == community.Id &&
                         ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        var modUserIds = await _context.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.CommunityMod &&
                         ur.CommunityId == community.Id &&
                         ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        return new CommunitySettingsDto
        {
            Slug = community.Slug,
            Name = community.Name,
            Description = community.Description,
            OwnerId = string.Empty, // TODO: Add owner tracking
            AdminUserIds = adminUserIds,
            ModeratorUserIds = modUserIds
        };
    }

    public async Task<CommunitySettingsDto?> UpdateSettingsAsync(string communityId, UpdateCommunitySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .AsTracking()
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return null;

        community.Name = request.Name;
        community.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(communityId, cancellationToken);
    }

    public async Task<CommunityModerationDto> GetModerationDataAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var communityDbId = await _context.Communities
            .Where(c => c.PublicId == communityId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (communityDbId == 0)
            return new CommunityModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        // Get pending reports
        var pendingReports = await _context.Reports
            .Where(r => r.CommunityId == communityDbId && r.StatusId == (int)ReportStatusEnum.Pending)
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

        // Get recent moderation actions (from audit log)
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

        // Get banned users
        var bannedUsers = await _context.UserBans
            .Where(ub => ub.CommunityId == communityDbId &&
                         ub.UnbannedAt == null &&
                         (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .Select(ub => new BannedUserDto
            {
                UserId = ub.User.PublicId,
                DisplayName = ub.User.DisplayName,
                BannedAt = ub.BannedAt,
                IsPermanent = ub.ExpiresAt == null
            })
            .ToListAsync(cancellationToken);

        // Get stats
        var totalReports = await _context.Reports
            .Where(r => r.CommunityId == communityDbId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await _context.Reports
            .Where(r => r.CommunityId == communityDbId && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await _context.Reports
            .Where(r => r.CommunityId == communityDbId && r.StatusId == (int)ReportStatusEnum.Dismissed)
            .CountAsync(cancellationToken);

        var actionsThisWeek = recentActions.Count(a => a.Timestamp >= weekAgo);

        return new CommunityModerationDto
        {
            PendingReports = pendingReports,
            RecentActions = recentActions,
            BannedUsers = bannedUsers,
            Stats = new ModerationStatsDto
            {
                TotalReports = totalReports,
                PendingReports = pendingCount,
                ResolvedReports = resolvedCount,
                DismissedReports = dismissedCount,
                TotalBans = bannedUsers.Count,
                ActiveBans = bannedUsers.Count,
                ActionsThisWeek = actionsThisWeek
            }
        };
    }

    public async Task<CommunityMembersListDto> GetMembersAsync(string communityId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        // TODO: Implement community members tracking
        // For now, return empty list
        return new CommunityMembersListDto
        {
            Members = new List<CommunityMemberDto>(),
            Total = 0,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> UpdateMemberRoleAsync(string communityId, string userId, UpdateMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return false;

        var user = await _context.Users
            .Where(u => u.PublicId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return false;

        var roleId = request.Role == "Admin"
            ? (int)UserRoleTypeEnum.CommunityAdmin
            : (int)UserRoleTypeEnum.CommunityMod;

        if (request.Action == "add")
        {
            var existingRole = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id &&
                             ur.RoleId == roleId &&
                             ur.CommunityId == community.Id &&
                             ur.RevokedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingRole != null)
                return true; // Already has the role

            _context.UserRoles.Add(new Infrastructure.Database.Entities.UserRoleDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                UserId = user.Id,
                RoleId = roleId,
                CommunityId = community.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = user.Id // TODO: Get current admin user
            });
        }
        else if (request.Action == "remove")
        {
            var existingRole = await _context.UserRoles
                .AsTracking()
                .Where(ur => ur.UserId == user.Id &&
                             ur.RoleId == roleId &&
                             ur.CommunityId == community.Id &&
                             ur.RevokedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingRole != null)
            {
                existingRole.RevokedAt = DateTime.UtcNow;
                existingRole.RevokedByUserId = user.Id; // TODO: Get current admin user
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<HubSpaceItemDto>> GetCommunitySpacesAsync(string communityId, CancellationToken cancellationToken = default)
    {
        return await _context.Spaces
            .Where(s => s.Hub.Community.PublicId == communityId)
            .Select(s => new HubSpaceItemDto
            {
                Slug = s.Slug,
                Name = s.Name,
                Description = s.Description,
                DiscussionCount = s.DiscussionCount,
                PostCount = s.PostCount,
                CreatedAt = s.CreatedAt,
                IsActive = true
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CommunityRulesDto> GetRulesAsync(string communityId, CancellationToken cancellationToken = default)
    {
        var rules = await _context.CommunityRules
            .Where(r => r.Community.PublicId == communityId)
            .OrderBy(r => r.SortOrder)
            .Select(r => new CommunityRuleDto
            {
                Title = r.Title,
                Description = r.Description,
                Order = r.SortOrder
            })
            .ToListAsync(cancellationToken);

        return new CommunityRulesDto { Rules = rules };
    }

    public async Task<CommunityRulesDto> UpdateRulesAsync(string communityId, UpdateCommunityRulesRequest request, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .AsTracking()
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return new CommunityRulesDto { Rules = new List<CommunityRuleDto>() };

        var existingRules = await _context.CommunityRules
            .Where(r => r.CommunityId == community.Id)
            .ToListAsync(cancellationToken);

        _context.CommunityRules.RemoveRange(existingRules);

        var now = DateTime.UtcNow;
        var newRules = request.Rules.Select((r, index) => new CommunityRuleDatabaseEntity
        {
            CommunityId = community.Id,
            Title = r.Title,
            Description = r.Description,
            SortOrder = index,
            CreatedAt = now
        }).ToList();

        _context.CommunityRules.AddRange(newRules);

        // Update denormalized fields
        community.HasRules = newRules.Count > 0;
        community.RulesRevision = Guid.NewGuid().ToString("N")[..8];

        var hasRules = newRules.Count > 0;

        // Cascade: update ParentCommunityHasRules on all child hubs
        var hubIds = await _context.Hubs
            .Where(h => h.CommunityId == community.Id)
            .Select(h => h.Id)
            .ToListAsync(cancellationToken);

        await _context.Hubs
            .Where(h => h.CommunityId == community.Id)
            .ExecuteUpdateAsync(h => h.SetProperty(x => x.ParentCommunityHasRules, hasRules), cancellationToken);

        // Cascade: update ParentCommunityHasRules on all child spaces (through hubs)
        await _context.Spaces
            .Where(s => hubIds.Contains(s.HubId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentCommunityHasRules, hasRules), cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRulesAsync(communityId, cancellationToken);
    }
}
