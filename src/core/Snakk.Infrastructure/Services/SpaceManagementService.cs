using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class SpaceManagementService(
    SnakkDbContext context,
    ILogger<SpaceManagementService> _logger) : ISpaceManagementService
{
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

        var followers = await context.UserFollows
            .Where(f => f.SpaceId == space.Id)
            .CountAsync(cancellationToken);

        // Get activity stats
        var postsToday = await context.Posts
            .Where(p =>
                p.Discussion.SpaceId == space.Id
                && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await context.Posts
            .Where(p =>
                p.Discussion.SpaceId == space.Id
                && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        var newDiscussionsToday = await context.Discussions
            .Where(d =>
                d.SpaceId == space.Id
                && d.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var newDiscussionsThisWeek = await context.Discussions
            .Where(d =>
                d.SpaceId == space.Id
                && d.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get pending reports
        var pendingReports = await context.Reports
            .Where(r =>
                r.SpaceId == space.Id
                && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        // Get moderators
        var moderators = await context.UserRoles
            .Where(ur =>
                ur.RoleId == (int)UserRoleTypeEnum.SpaceMod
                && ur.SpaceId == space.Id
                && ur.RevokedAt == null)
            .Select(ur => new SpaceModeratorDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                AssignedAt = ur.AssignedAt
            })
            .ToListAsync(cancellationToken);

        // Get recent activity
        var recentActivity = await context.Posts
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

    public async Task<SpaceSettingsDto?> GetSettingsAsync(
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        var space = await context.Spaces
            .Where(s => s.PublicId == spaceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space is null)
            return null;

        var allowedTypes = await context.SpaceAllowedDiscussionTypes
            .Where(x => x.SpaceId == space.Id)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync(cancellationToken);

        var modUserIds = await context.UserRoles
            .Where(ur =>
                ur.RoleId == (int)UserRoleTypeEnum.SpaceMod
                && ur.SpaceId == space.Id
                && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        return new SpaceSettingsDto
        {
            Slug = space.Slug,
            Name = space.Name,
            Description = space.Description,
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

        // Get pending reports
        var pendingReports = await context.Reports
            .Where(r =>
                r.SpaceId == spaceDbId
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

        // Get recent moderation actions
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

        // Get stats
        var totalReports = await context.Reports
            .Where(r => r.SpaceId == spaceDbId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await context.Reports
            .Where(r =>
                r.SpaceId == spaceDbId
                && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await context.Reports
            .Where(r =>
                r.SpaceId == spaceDbId
                && r.StatusId == (int)ReportStatusEnum.Dismissed)
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
}
