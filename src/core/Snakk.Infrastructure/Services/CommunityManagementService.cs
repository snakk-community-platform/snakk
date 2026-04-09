using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class CommunityManagementService(
    SnakkDbContext context,
    ILogger<CommunityManagementService> _logger) : ICommunityManagementService
{
    public async Task<CommunityOverviewDto?> GetOverviewAsync(
        string communityId,
        CancellationToken cancellationToken = default)
    {
        var community = await context.Communities
            .Where(c => c.PublicId == communityId)
            .Select(c => new {
                c.Id,
                c.Slug,
                c.Name,
                c.Description,
                c.CreatedAt,
                c.HubCount,
                c.SpaceCount,
                c.DiscussionCount,
                c.PostCount })
            .FirstOrDefaultAsync(cancellationToken);

        if (community is null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);

        // Get activity stats - posts
        var postsToday = await context.Posts
            .Where(p =>
                p.Discussion.Space.Hub.CommunityId == community.Id
                && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await context.Posts
            .Where(p =>
                p.Discussion.Space.Hub.CommunityId == community.Id
                && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get moderation stats
        var pendingReports = await context.Reports
            .Where(r =>
                r.CommunityId == community.Id
                && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        var activeBans = await context.UserBans
            .Where(ub =>
                ub.CommunityId == community.Id
                && ub.UnbannedAt == null
                && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .CountAsync(cancellationToken);

        // Get team members
        var admins = await context.UserRoles
            .Where(ur =>
                (ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin || ur.RoleId == (int)UserRoleTypeEnum.CommunityMod)
                && ur.CommunityId == community.Id
                && ur.RevokedAt == null)
            .Select(ur => new CommunityMemberDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                JoinedAt = ur.AssignedAt,
                Roles = new List<string> { ((UserRoleTypeEnum)ur.RoleId).ToString() }
            })
            .ToListAsync(cancellationToken);

        // Get recent activity
        var recentActivity = await context.Posts
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

        var adminList = admins
            .Where(a => a.Roles.Contains("CommunityAdmin"))
            .ToList();

        var modList = admins
            .Where(a => a.Roles.Contains("CommunityMod"))
            .ToList();

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

    public async Task<CommunitySettingsDto?> GetSettingsAsync(
        string communityId,
        CancellationToken cancellationToken = default)
    {
        var community = await context.Communities
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community is null)
            return null;

        var adminUserIds = await context.UserRoles
            .Where(ur =>
                ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin
                && ur.CommunityId == community.Id
                && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        var modUserIds = await context.UserRoles
            .Where(ur =>
                ur.RoleId == (int)UserRoleTypeEnum.CommunityMod
                && ur.CommunityId == community.Id
                && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        var allowedTypes = await context.CommunityAllowedDiscussionTypes
            .Where(x => x.CommunityId == community.Id)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync(cancellationToken);

        return new CommunitySettingsDto
        {
            Slug = community.Slug,
            Name = community.Name,
            Description = community.Description,
            Timezone = community.Timezone,
            LanguageCode = community.LanguageCode,
            AllowedDiscussionTypes = allowedTypes,
            OwnerId = string.Empty, // TODO: Add owner tracking
            AdminUserIds = adminUserIds,
            ModeratorUserIds = modUserIds
        };
    }

    public async Task<CommunitySettingsDto?> UpdateSettingsAsync(
        string communityId,
        UpdateCommunitySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var community = await context.Communities
            .AsTracking()
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community is null)
            return null;

        community.Name = request.Name;
        community.Description = request.Description;
        community.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? null : request.Timezone;

        if (request.LanguageCode is not null || community.LanguageCode is not null)
        {
            var newLanguageCode = request.LanguageCode;
            if (newLanguageCode != community.LanguageCode)
            {
                community.LanguageCode = newLanguageCode;

                // Cascade to all child hubs and spaces
                await context.Hubs
                    .Where(h => h.CommunityId == community.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.CommunityLanguageCode, newLanguageCode), cancellationToken);

                await context.Spaces
                    .Where(s => s.Hub.CommunityId == community.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.CommunityLanguageCode, newLanguageCode), cancellationToken);
            }
        }

        // Update allowed discussion types (empty list = all types allowed)
        var existingTypes = await context.CommunityAllowedDiscussionTypes
            .Where(x => x.CommunityId == community.Id)
            .ToListAsync(cancellationToken);

        context.CommunityAllowedDiscussionTypes.RemoveRange(existingTypes);

        if (request.AllowedDiscussionTypes.Count > 0)
        {
            var newTypes = request.AllowedDiscussionTypes
                .Select(type => new CommunityAllowedDiscussionTypeDatabaseEntity
                {
                    CommunityId = community.Id,
                    DiscussionType = (int)type
                });

            context.CommunityAllowedDiscussionTypes.AddRange(newTypes);
        }

        await context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(communityId, cancellationToken);
    }

    public async Task<CommunityModerationDto> GetModerationDataAsync(
        string communityId,
        CancellationToken cancellationToken = default)
    {
        var communityDbId = await context.Communities
            .Where(c => c.PublicId == communityId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (communityDbId == 0)
            return new CommunityModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        // Get pending reports
        var pendingReports = await context.Reports
            .Where(r =>
                r.CommunityId == communityDbId
                && r.StatusId == (int)ReportStatusEnum.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ModerationReportDto
            {
                PublicId = r.PublicId,
                Description = r.Details,
                ReportedByUserId = r.ReporterUser.PublicId,
                ReportedByDisplayName = r.ReporterUser.DisplayName,
                CreatedAt = r.CreatedAt,

                Status = ((ReportStatusEnum)r.StatusId).ToString(),

                Type =
                    r.ReportedPost != null
                    ? "Post" : r.ReportedDiscussion != null
                        ? "Discussion" : "User",
                Reason = r.Reason != null ? r.Reason.Name : "Other",
                TargetUserId = r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                TargetUserDisplayName = r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                TargetPostPublicId = r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                TargetDiscussionPublicId = r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
            })
            .ToListAsync(cancellationToken);

        // Get recent moderation actions (from audit log)
        var recentActions = await context.AuditLogs
            .Where(a =>
                a.Category == "Moderation"
                && (a.Action.Contains("Ban") || a.Action.Contains("Delete") || a.Action.Contains("Moderate")))
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .Select(a => new ModerationActionDto
            {
                PublicId = a.PublicId,
                ActionType = a.Action,
                Reason = a.Reason ?? "",
                Timestamp = a.CreatedAt,

                ModeratorDisplayName = a.ActorUser != null ? a.ActorUser.DisplayName : "System",
            })
            .ToListAsync(cancellationToken);

        // Get banned users
        var bannedUsers = await context.UserBans
            .Where(ub =>
                ub.CommunityId == communityDbId
                && ub.UnbannedAt == null
                && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .Select(ub => new BannedUserDto
            {
                UserId = ub.User.PublicId,
                DisplayName = ub.User.DisplayName,
                BannedAt = ub.BannedAt,
                IsPermanent = ub.ExpiresAt == null
            })
            .ToListAsync(cancellationToken);

        // Get stats
        var totalReports = await context.Reports
            .Where(r => r.CommunityId == communityDbId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await context.Reports
            .Where(r =>
                r.CommunityId == communityDbId
                && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await context.Reports
            .Where(r =>
                r.CommunityId == communityDbId
                && r.StatusId == (int)ReportStatusEnum.Dismissed)
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

    public async Task<CommunityMembersListDto> GetMembersAsync(
        string communityId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement community members tracking
        // For now, return empty list
        return new CommunityMembersListDto
        {
            Members = [],
            Total = 0,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<bool> UpdateMemberRoleAsync(
        string communityId,
        string userId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var community = await context.Communities
            .Where(c => c.PublicId == communityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (community is null)
            return false;

        var user = await context.Users
            .Where(u => u.PublicId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return false;

        var roleId = request.Role == "Admin"
            ? (int)UserRoleTypeEnum.CommunityAdmin
            : (int)UserRoleTypeEnum.CommunityMod;

        if (request.Action == "add")
        {
            var existingRole = await context.UserRoles
                .Where(ur =>
                    ur.UserId == user.Id
                    && ur.RoleId == roleId
                    && ur.CommunityId == community.Id
                    && ur.RevokedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingRole is not null)
                return true; // Already has the role

            context.UserRoles.Add(new Infrastructure.Database.Entities.UserRoleDatabaseEntity
            {
                PublicId = Ulid.NewUlid().ToString(),
                UserId = user.Id,
                RoleId = roleId,
                CommunityId = community.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = user.Id // TODO: Get current admin user
            });
        }
        else if (request.Action == "remove")
        {
            var existingRole = await context.UserRoles
                .AsTracking()
                .Where(ur =>
                    ur.UserId == user.Id
                    && ur.RoleId == roleId
                    && ur.CommunityId == community.Id
                    && ur.RevokedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingRole is not null)
            {
                existingRole.RevokedAt = DateTime.UtcNow;
                existingRole.RevokedByUserId = user.Id; // TODO: Get current admin user
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<HubSpaceItemDto>> GetCommunitySpacesAsync(
        string communityId,
        CancellationToken cancellationToken = default) =>
        await context.Spaces
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
