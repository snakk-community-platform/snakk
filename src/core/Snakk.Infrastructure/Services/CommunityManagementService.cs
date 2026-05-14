using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class CommunityManagementService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory,
    IUserGrantsCacheService grantsCache,
    HybridCache cache) : ICommunityManagementService
{
    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

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
        var communityDbId = community.Id;
        var communitySlug = community.Slug;

        var postCountsTask = ReadAsync(db => db.Posts
            .Where(p => p.Discussion.Space.Hub.CommunityId == communityDbId && p.CreatedAt >= weekAgo)
            .GroupBy(_ => true)
            .Select(g => new { Today = g.Count(p => p.CreatedAt >= today), Week = g.Count() })
            .FirstOrDefaultAsync(cancellationToken));

        var pendingReportsTask = ReadAsync(db => db.Reports
            .Where(r => r.CommunityId == communityDbId && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken));

        var activeBansTask = ReadAsync(db => db.UserBans
            .Where(ub => ub.CommunityId == communityDbId && ub.UnbannedAt == null && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .CountAsync(cancellationToken));

        var adminsTask = ReadAsync(db => db.UserRoles
            .Where(ur => (ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin || ur.RoleId == (int)UserRoleTypeEnum.CommunityMod) && ur.CommunityId == communityDbId && ur.RevokedAt == null)
            .Select(ur => new CommunityMemberDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName ?? "",
                JoinedAt = ur.AssignedAt,
                Roles = new List<string> { ((UserRoleTypeEnum)ur.RoleId).ToString() }
            })
            .ToListAsync(cancellationToken));

        var recentActivityTask = ReadAsync(db => db.Posts
            .Where(p => p.Discussion.Space.Hub.CommunityId == communityDbId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new RecentActivityItemDto
            {
                Type = "post",
                Description = p.Discussion.Title,
                UserDisplayName = p.CreatedByUser.DisplayName ?? "",
                Timestamp = p.CreatedAt,
                LinkUrl = "/c/" + communitySlug + "/s/" + p.Discussion.Space.Slug + "/d/" + p.Discussion.Id
            })
            .ToListAsync(cancellationToken));

        await Task.WhenAll(postCountsTask, pendingReportsTask, activeBansTask, adminsTask, recentActivityTask);

        var postCounts     = postCountsTask.Result;
        var pendingReports = pendingReportsTask.Result;
        var activeBans     = activeBansTask.Result;
        var admins         = adminsTask.Result;
        var recentActivity = recentActivityTask.Result;

        var postsToday    = postCounts?.Today ?? 0;
        var postsThisWeek = postCounts?.Week ?? 0;

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

        var cid = community.Id;

        var adminUserIdsTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.CommunityAdmin && ur.CommunityId == cid && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken));

        var modUserIdsTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.CommunityMod && ur.CommunityId == cid && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken));

        var allowedTypesTask = ReadAsync(db => db.CommunityAllowedDiscussionTypes
            .Where(x => x.CommunityId == cid)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync(cancellationToken));

        await Task.WhenAll(adminUserIdsTask, modUserIdsTask, allowedTypesTask);
        var adminUserIds = adminUserIdsTask.Result;
        var modUserIds   = modUserIdsTask.Result;
        var allowedTypes = allowedTypesTask.Result;

        return new CommunitySettingsDto
        {
            Slug = community.Slug,
            Name = community.Name,
            Description = community.Description,
            Timezone = community.Timezone,
            LanguageCode = community.LanguageCode,
            AllowedDiscussionTypes = allowedTypes,
            HideAdultDiscussionsFromLists = community.HideAdultDiscussionsFromLists,
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

        var nameChanged = community.Name != request.Name;
        community.Name = request.Name;
        community.Description = request.Description;
        community.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? null : request.Timezone;

        if (nameChanged)
        {
            try
            {
                await context.Spaces
                    .Where(s => s.Hub.CommunityId == community.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.CommunityName, request.Name), cancellationToken);
            }
            catch (InvalidOperationException) { }
        }

        if (community.HideAdultDiscussionsFromLists != request.HideAdultDiscussionsFromLists)
        {
            community.HideAdultDiscussionsFromLists = request.HideAdultDiscussionsFromLists;
            try
            {
                await context.Spaces
                    .Where(s => s.Hub.CommunityId == community.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        sp => sp.CommunityHideAdultDiscussionsFromLists,
                        request.HideAdultDiscussionsFromLists), cancellationToken);
            }
            catch (InvalidOperationException) { }
            grantsCache.InvalidateAdultHidingSpaces();
        }

        if (request.LanguageCode is not null || community.LanguageCode is not null)
        {
            var newLanguageCode = request.LanguageCode;
            if (newLanguageCode != community.LanguageCode)
            {
                community.LanguageCode = newLanguageCode;

                try
                {
                    // Cascade to all child hubs and spaces
                    await context.Hubs
                        .Where(h => h.CommunityId == community.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(h => h.CommunityLanguageCode, newLanguageCode), cancellationToken);

                    await context.Spaces
                        .Where(s => s.Hub.CommunityId == community.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.CommunityLanguageCode, newLanguageCode), cancellationToken);
                }
                catch (InvalidOperationException) { }
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

        var pendingReportsTask = ReadAsync(db => db.Reports
            .Where(r => r.CommunityId == communityDbId && r.StatusId == (int)ReportStatusEnum.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ModerationReportDto
            {
                PublicId = r.PublicId,
                Description = r.Details,
                ReportedByUserId = r.ReporterUser.PublicId,
                ReportedByDisplayName = r.ReporterUser.DisplayName ?? "",
                CreatedAt = r.CreatedAt,
                Status = ((ReportStatusEnum)r.StatusId).ToString(),
                Type = r.ReportedPost != null ? "Post" : r.ReportedDiscussion != null ? "Discussion" : "User",
                Reason = r.Reason != null ? r.Reason.Name : "Other",
                TargetUserId = r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                TargetUserDisplayName = r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                TargetPostPublicId = r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                TargetDiscussionPublicId = r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
            })
            .ToListAsync(cancellationToken));

        var recentActionsTask = ReadAsync(db => db.AuditLogs
            .Where(a => a.Category == "Moderation" && (a.Action.Contains("Ban") || a.Action.Contains("Delete") || a.Action.Contains("Moderate")))
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .Select(a => new ModerationActionDto
            {
                PublicId = a.PublicId,
                ActionType = a.Action,
                Reason = a.Reason ?? "",
                Timestamp = a.CreatedAt,
                ModeratorDisplayName = a.ActorUser != null ? a.ActorUser.DisplayName ?? "" : "System",
            })
            .ToListAsync(cancellationToken));

        var bannedUsersTask = ReadAsync(db => db.UserBans
            .Where(ub => ub.CommunityId == communityDbId && ub.UnbannedAt == null && (ub.ExpiresAt == null || ub.ExpiresAt > now))
            .Select(ub => new BannedUserDto
            {
                UserId = ub.User.PublicId,
                DisplayName = ub.User.DisplayName ?? "",
                BannedAt = ub.BannedAt,
                IsPermanent = ub.ExpiresAt == null
            })
            .ToListAsync(cancellationToken));

        var reportCountsTask = ReadAsync(db => db.Reports
            .Where(r => r.CommunityId == communityDbId)
            .GroupBy(r => r.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken));

        await Task.WhenAll(pendingReportsTask, recentActionsTask, bannedUsersTask, reportCountsTask);
        var pendingReports = pendingReportsTask.Result;
        var recentActions  = recentActionsTask.Result;
        var bannedUsers    = bannedUsersTask.Result;
        var reportCounts   = reportCountsTask.Result;

        var totalReports = reportCounts.Sum(c => c.Count);
        var pendingCount = reportCounts.FirstOrDefault(c => c.StatusId == (int)ReportStatusEnum.Pending)?.Count ?? pendingReports.Count;
        var resolvedCount = reportCounts.FirstOrDefault(c => c.StatusId == (int)ReportStatusEnum.Resolved)?.Count ?? 0;
        var dismissedCount = reportCounts.FirstOrDefault(c => c.StatusId == (int)ReportStatusEnum.Dismissed)?.Count ?? 0;

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
        await cache.RemoveByTagAsync($"manage_perms_user_{userId}");

        return true;
    }

    public async Task<List<HubSpaceItemDto>> GetCommunitySpacesAsync(
        string communityId,
        CancellationToken cancellationToken = default) =>
        await context.Spaces
            .Where(s => s.CommunityPublicId == communityId)
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
