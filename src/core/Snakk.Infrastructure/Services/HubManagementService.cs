using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class HubManagementService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory) : IHubManagementService
{
    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    public async Task<HubOverviewDto?> GetOverviewAsync(
        string hubId,
        CancellationToken cancellationToken = default)
    {
        var hub = await context.Hubs
            .Where(h => h.PublicId == hubId)
            .Include(h => h.Community)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub is null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);
        var hubDbId = hub.Id;
        var communitySlug = hub.Community.Slug;

        var postCountsTask = ReadAsync(db => db.Posts
            .Where(p => p.Discussion.Space.HubId == hubDbId && p.CreatedAt >= weekAgo)
            .GroupBy(_ => true)
            .Select(g => new { Today = g.Count(p => p.CreatedAt >= today), Week = g.Count() })
            .FirstOrDefaultAsync(cancellationToken));

        var discussionCountsTask = ReadAsync(db => db.Discussions
            .Where(d => d.Space.HubId == hubDbId && d.CreatedAt >= weekAgo)
            .GroupBy(_ => true)
            .Select(g => new { Today = g.Count(d => d.CreatedAt >= today), Week = g.Count() })
            .FirstOrDefaultAsync(cancellationToken));

        var pendingReportsTask = ReadAsync(db => db.Reports
            .Where(r => r.HubId == hubDbId && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken));

        var moderatorsTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.HubMod && ur.HubId == hubDbId && ur.RevokedAt == null)
            .Select(ur => new HubModeratorDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName ?? "",
                AssignedAt = ur.AssignedAt
            })
            .ToListAsync(cancellationToken));

        var recentActivityTask = ReadAsync(db => db.Posts
            .Where(p => p.Discussion.Space.HubId == hubDbId)
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

        await Task.WhenAll(postCountsTask, discussionCountsTask, pendingReportsTask, moderatorsTask, recentActivityTask);

        var postCounts       = postCountsTask.Result;
        var discussionCounts = discussionCountsTask.Result;
        var pendingReports   = pendingReportsTask.Result;
        var moderators       = moderatorsTask.Result;
        var recentActivity   = recentActivityTask.Result;

        var postsToday             = postCounts?.Today ?? 0;
        var postsThisWeek          = postCounts?.Week ?? 0;
        var newDiscussionsToday    = discussionCounts?.Today ?? 0;
        var newDiscussionsThisWeek = discussionCounts?.Week ?? 0;

        return new HubOverviewDto
        {
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

    public async Task<HubSettingsDto?> GetSettingsAsync(
        string hubId,
        CancellationToken cancellationToken = default)
    {
        var hub = await context.Hubs
            .Where(h => h.PublicId == hubId)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub is null)
            return null;

        var hid = hub.Id;

        var modUserIdsTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.HubMod && ur.HubId == hid && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken));

        var allowedTypesTask = ReadAsync(db => db.HubAllowedDiscussionTypes
            .Where(x => x.HubId == hid)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync(cancellationToken));

        await Task.WhenAll(modUserIdsTask, allowedTypesTask);
        var modUserIds   = modUserIdsTask.Result;
        var allowedTypes = allowedTypesTask.Result;

        return new HubSettingsDto
        {
            Slug = hub.Slug,
            Name = hub.Name,
            Description = hub.Description,
            LanguageCode = hub.LanguageCode,
            CommunityLanguageCode = hub.CommunityLanguageCode,
            Require2FA = hub.Require2FA,
            AllowedDiscussionTypes = allowedTypes,
            ModeratorUserIds = modUserIds
        };
    }

    public async Task<HubSettingsDto?> UpdateSettingsAsync(
        string hubId,
        UpdateHubSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var hub = await context.Hubs
            .AsTracking()
            .Where(h => h.PublicId == hubId)
            .FirstOrDefaultAsync(cancellationToken);

        if (hub is null)
            return null;

        var nameChanged = hub.Name != request.Name;
        hub.Name = request.Name;
        hub.Description = request.Description;
        hub.Require2FA = request.Require2FA;

        if (nameChanged)
        {
            try
            {
                await context.Spaces
                    .Where(s => s.HubId == hub.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.HubName, request.Name), cancellationToken);
            }
            catch (InvalidOperationException) { }
        }

        if (request.LanguageCode is not null || hub.LanguageCode is not null)
        {
            var newLanguageCode = request.LanguageCode;
            if (newLanguageCode != hub.LanguageCode)
            {
                hub.LanguageCode = newLanguageCode;

                try
                {
                    // Cascade to all child spaces
                    await context.Spaces
                        .Where(s => s.HubId == hub.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.HubLanguageCode, newLanguageCode), cancellationToken);
                }
                catch (InvalidOperationException) { }
            }
        }

        // Update allowed discussion types (empty list = all types allowed)
        var existingTypes = await context.HubAllowedDiscussionTypes
            .Where(x => x.HubId == hub.Id)
            .ToListAsync(cancellationToken);

        context.HubAllowedDiscussionTypes.RemoveRange(existingTypes);

        if (request.AllowedDiscussionTypes.Count > 0)
        {
            var newTypes = request.AllowedDiscussionTypes
                .Select(type => new HubAllowedDiscussionTypeDatabaseEntity
                {
                    HubId = hub.Id,
                    DiscussionType = (int)type
                });

            context.HubAllowedDiscussionTypes.AddRange(newTypes);
        }

        await context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(hubId, cancellationToken);
    }

    public async Task<HubModerationDto> GetModerationDataAsync(
        string hubId,
        CancellationToken cancellationToken = default)
    {
        var hubDbId = await context.Hubs
            .Where(h => h.PublicId == hubId)
            .Select(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (hubDbId == 0)
            return new HubModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        var pendingReportsTask = ReadAsync(db => db.Reports
            .Where(r => r.HubId == hubDbId && r.StatusId == (int)ReportStatusEnum.Pending)
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

        var reportCountsTask = ReadAsync(db => db.Reports
            .Where(r => r.HubId == hubDbId)
            .GroupBy(r => r.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken));

        await Task.WhenAll(pendingReportsTask, recentActionsTask, reportCountsTask);
        var pendingReports = pendingReportsTask.Result;
        var recentActions  = recentActionsTask.Result;
        var reportCounts   = reportCountsTask.Result;

        var totalReports = reportCounts.Sum(c => c.Count);
        var pendingCount = reportCounts.FirstOrDefault(c => c.StatusId == (int)ReportStatusEnum.Pending)?.Count ?? pendingReports.Count;
        var resolvedCount = reportCounts.FirstOrDefault(c => c.StatusId == (int)ReportStatusEnum.Resolved)?.Count ?? 0;
        var dismissedCount = reportCounts.FirstOrDefault(c => c.StatusId == (int)ReportStatusEnum.Dismissed)?.Count ?? 0;

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

    public async Task<HubSpacesDto> GetSpacesAsync(
        string hubId,
        CancellationToken cancellationToken = default)
    {
        var hubDbId = await context.Hubs
            .Where(h => h.PublicId == hubId)
            .Select(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var spaces = await context.Spaces
            .Where(s => s.HubId == hubDbId)
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

        return new HubSpacesDto
        {
            Spaces = spaces,
            Total = spaces.Count
        };
    }
}
