using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
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

    public async Task<CommunityOverviewDto?> GetOverviewAsync(string slug, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.Slug == slug)
            .Select(c => new
            {
                c.Slug,
                c.Name,
                c.Description,
                c.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);

        // Get stats
        var totalHubs = await _context.Hubs
            .Where(h => h.Community.Slug == slug)
            .CountAsync(cancellationToken);

        var totalSpaces = await _context.Spaces
            .Where(s => s.Hub.Community.Slug == slug)
            .CountAsync(cancellationToken);

        var totalDiscussions = await _context.Discussions
            .Where(d => d.Space.Hub.Community.Slug == slug)
            .CountAsync(cancellationToken);

        var totalPosts = await _context.Posts
            .Where(p => p.Discussion.Space.Hub.Community.Slug == slug)
            .CountAsync(cancellationToken);

        // Get activity stats - posts
        var postsToday = await _context.Posts
            .Where(p => p.Discussion.Space.Hub.Community.Slug == slug && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await _context.Posts
            .Where(p => p.Discussion.Space.Hub.Community.Slug == slug && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get moderation stats
        var pendingReports = await _context.Reports
            .Where(r => r.Community.Slug == slug && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        var activeBans = await _context.UserBans
            .Where(ub => ub.CommunityId != null &&
                         ub.Community!.Slug == slug &&
                         ub.UnbannedAt == null &&
                         (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .CountAsync(cancellationToken);

        // Get team members
        var admins = await _context.UserRoles
            .Where(ur => (ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin || ur.RoleId == (int)UserRoleTypeEnum.CommunityMod) &&
                         ur.CommunityId != null &&
                         ur.Community!.Slug == slug &&
                         ur.RevokedAt == null)
            .Include(ur => ur.User)
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
            .Where(p => p.Discussion.Space.Hub.Community.Slug == slug)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new RecentActivityItemDto
            {
                Type = "post",
                Description = p.Discussion.Title,
                UserDisplayName = p.CreatedByUser.DisplayName,
                Timestamp = p.CreatedAt,
                LinkUrl = $"/c/{slug}/s/{p.Discussion.Space.Slug}/d/{p.Discussion.Id}"
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
            TotalHubs = totalHubs,
            TotalSpaces = totalSpaces,
            TotalDiscussions = totalDiscussions,
            TotalPosts = totalPosts,
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

    public async Task<CommunitySettingsDto?> GetSettingsAsync(string slug, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.Slug == slug)
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

    public async Task<CommunitySettingsDto?> UpdateSettingsAsync(string slug, UpdateCommunitySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.Slug == slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (community == null)
            return null;

        community.Name = request.Name;
        community.Description = request.Description;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(slug, cancellationToken);
    }

    public async Task<CommunityModerationDto> GetModerationDataAsync(string slug, CancellationToken cancellationToken = default)
    {
        var communityId = await _context.Communities
            .Where(c => c.Slug == slug)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (communityId == 0)
            return new CommunityModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        // Get pending reports
        var pendingReports = await _context.Reports
            .Where(r => r.CommunityId == communityId && r.StatusId == (int)ReportStatusEnum.Pending)
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

        // Get recent moderation actions (from audit log)
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

        // Get banned users
        var bannedUsers = await _context.UserBans
            .Where(ub => ub.CommunityId == communityId &&
                         ub.UnbannedAt == null &&
                         (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .Include(ub => ub.User)
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
            .Where(r => r.CommunityId == communityId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await _context.Reports
            .Where(r => r.CommunityId == communityId && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await _context.Reports
            .Where(r => r.CommunityId == communityId && r.StatusId == (int)ReportStatusEnum.Dismissed)
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

    public async Task<CommunityMembersListDto> GetMembersAsync(string slug, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
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

    public async Task<bool> UpdateMemberRoleAsync(string slug, string userId, UpdateMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        var community = await _context.Communities
            .Where(c => c.Slug == slug)
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

    public async Task<List<HubSpaceItemDto>> GetCommunitySpacesAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Spaces
            .Where(s => s.Hub.Community.Slug == slug)
            .Select(s => new HubSpaceItemDto
            {
                Id = s.Id,
                Slug = s.Slug,
                Name = s.Name,
                Description = s.Description,
                DiscussionCount = s.Discussions.Count,
                PostCount = s.Discussions.SelectMany(d => d.Posts).Count(),
                CreatedAt = s.CreatedAt,
                IsActive = true
            })
            .ToListAsync(cancellationToken);
    }
}
