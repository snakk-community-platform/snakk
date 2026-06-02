using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class SpaceManagementService(
    SnakkDbContext context,
    IDbContextFactory<SnakkDbContext> dbFactory) : ISpaceManagementService
{
    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await query(db);
    }

    public async Task<SpaceOverviewDto?> GetOverviewAsync(
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        var space = await context.Spaces
            .Where(s => s.PublicId == spaceId)
            .Include(s => s.Hub)
            .ThenInclude(h => h.Community)
            .FirstOrDefaultAsync(cancellationToken);

        if (space is null)
            return null;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);
        var spaceDbId = space.Id;
        var spaceSlug = space.Slug;
        var communitySlug = space.Hub.Community.Slug;

        var followersTask = ReadAsync(db => db.UserFollows
            .Where(f => f.SpaceId == spaceDbId)
            .CountAsync(cancellationToken));

        var postCountsTask = ReadAsync(db => db.Posts
            .Where(p => p.Discussion.SpaceId == spaceDbId && p.CreatedAt >= weekAgo)
            .GroupBy(_ => true)
            .Select(g => new { Today = g.Count(p => p.CreatedAt >= today), Week = g.Count() })
            .FirstOrDefaultAsync(cancellationToken));

        var discussionCountsTask = ReadAsync(db => db.Discussions
            .Where(d => d.SpaceId == spaceDbId && d.CreatedAt >= weekAgo)
            .GroupBy(_ => true)
            .Select(g => new { Today = g.Count(d => d.CreatedAt >= today), Week = g.Count() })
            .FirstOrDefaultAsync(cancellationToken));

        var pendingReportsTask = ReadAsync(db => db.Reports
            .Where(r => r.SpaceId == spaceDbId && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken));

        var moderatorsTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.SpaceMod && ur.SpaceId == spaceDbId && ur.RevokedAt == null)
            .Select(ur => new SpaceModeratorDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName ?? "",
                AssignedAt = ur.AssignedAt
            })
            .ToListAsync(cancellationToken));

        var recentActivityTask = ReadAsync(db => db.Posts
            .Where(p => p.Discussion.SpaceId == spaceDbId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new RecentActivityItemDto
            {
                Type = "post",
                Description = p.Discussion.Title,
                UserDisplayName = p.CreatedByUser.DisplayName ?? "",
                Timestamp = p.CreatedAt,
                LinkUrl = "/c/" + communitySlug + "/s/" + spaceSlug + "/d/" + p.Discussion.Id
            })
            .ToListAsync(cancellationToken));

        await Task.WhenAll(followersTask, postCountsTask, discussionCountsTask, pendingReportsTask, moderatorsTask, recentActivityTask);

        var followers         = followersTask.Result;
        var postCounts        = postCountsTask.Result;
        var discussionCounts  = discussionCountsTask.Result;
        var pendingReports    = pendingReportsTask.Result;
        var moderators        = moderatorsTask.Result;
        var recentActivity    = recentActivityTask.Result;

        var postsToday            = postCounts?.Today ?? 0;
        var postsThisWeek         = postCounts?.Week ?? 0;
        var newDiscussionsToday   = discussionCounts?.Today ?? 0;
        var newDiscussionsThisWeek = discussionCounts?.Week ?? 0;

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

    public async Task<SpaceSettingsDto?> GetSettingsAsync(
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        var space = await context.Spaces
            .Where(s => s.PublicId == spaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space is null)
            return null;

        var sid = space.Id;

        var allowedTypesTask = ReadAsync(db => db.SpaceAllowedDiscussionTypes
            .Where(x => x.SpaceId == sid)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync(cancellationToken));

        var modUserIdsTask = ReadAsync(db => db.UserRoles
            .Where(ur => ur.RoleId == (int)UserRoleTypeEnum.SpaceMod && ur.SpaceId == sid && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken));

        await Task.WhenAll(allowedTypesTask, modUserIdsTask);
        var allowedTypes = allowedTypesTask.Result;
        var modUserIds   = modUserIdsTask.Result;

        return new SpaceSettingsDto
        {
            Slug = space.Slug,
            Name = space.Name,
            Description = space.Description,
            LanguageCode = space.LanguageCode,
            HubLanguageCode = space.HubLanguageCode,
            CommunityLanguageCode = space.CommunityLanguageCode,
            AutoParagraphEnabled = space.AutoParagraphEnabled,
            IsAdultOnly = space.IsAdultOnly,
            AllowsAdultContent = space.AllowsAdultContent,
            Require2FA = space.Require2FA,
            AllowedDiscussionTypes = allowedTypes,
            ModeratorUserIds = modUserIds
        };
    }

    public async Task<SpaceSettingsDto?> UpdateSettingsAsync(
        string spaceId,
        UpdateSpaceSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var space = await context.Spaces
            .AsTracking()
            .Where(s => s.PublicId == spaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space is null)
            return null;

        space.Name = request.Name;
        space.Description = request.Description;
        space.AutoParagraphEnabled = request.AutoParagraphEnabled;
        space.IsAdultOnly = request.IsAdultOnly;
        // Adult-only implies allows-adult is irrelevant; force false for clarity.
        space.AllowsAdultContent = !request.IsAdultOnly && request.AllowsAdultContent;
        space.Require2FA = request.Require2FA;

        if (request.LanguageCode is not null || space.LanguageCode is not null)
        {
            space.LanguageCode = request.LanguageCode;
        }

        // Update allowed discussion types
        var existingTypes = await context.SpaceAllowedDiscussionTypes
            .Where(x => x.SpaceId == space.Id)
            .ToListAsync(cancellationToken);

        context.SpaceAllowedDiscussionTypes.RemoveRange(existingTypes);

        var newTypes = request.AllowedDiscussionTypes
            .Select(type => new SpaceAllowedDiscussionTypeDatabaseEntity
            {
                SpaceId = space.Id,
                DiscussionType = (int)type
            });

        context.SpaceAllowedDiscussionTypes.AddRange(newTypes);

        await context.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(spaceId, cancellationToken);
    }

    public async Task<SpaceModerationDto> GetModerationDataAsync(
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        var spaceDbId = await context.Spaces
            .Where(s => s.PublicId == spaceId)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (spaceDbId == 0)
            return new SpaceModerationDto();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);

        var pendingReportsTask = ReadAsync(db => db.Reports
            .Where(r => r.SpaceId == spaceDbId && r.StatusId == (int)ReportStatusEnum.Pending)
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
            .Where(r => r.SpaceId == spaceDbId)
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
}
