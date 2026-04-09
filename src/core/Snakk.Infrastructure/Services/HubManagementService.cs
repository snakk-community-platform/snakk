using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.DTOs.Management;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Services;

public class HubManagementService(
    SnakkDbContext context,
    ILogger<HubManagementService> _logger) : IHubManagementService
{
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

        // Get activity stats
        var postsToday = await context.Posts
            .Where(p =>
                p.Discussion.Space.HubId == hub.Id
                && p.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var postsThisWeek = await context.Posts
            .Where(p =>
                p.Discussion.Space.HubId == hub.Id
                && p.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        var newDiscussionsToday = await context.Discussions
            .Where(d =>
                d.Space.HubId == hub.Id
                && d.CreatedAt >= today)
            .CountAsync(cancellationToken);

        var newDiscussionsThisWeek = await context.Discussions
            .Where(d =>
                d.Space.HubId == hub.Id
                && d.CreatedAt >= weekAgo)
            .CountAsync(cancellationToken);

        // Get pending reports
        var pendingReports = await context.Reports
            .Where(r =>
                r.HubId == hub.Id
                && r.StatusId == (int)ReportStatusEnum.Pending)
            .CountAsync(cancellationToken);

        // Get moderators
        var moderators = await context.UserRoles
            .Where(ur =>
                ur.RoleId == (int)UserRoleTypeEnum.HubMod
                && ur.HubId == hub.Id
                && ur.RevokedAt == null)
            .Select(ur => new HubModeratorDto
            {
                UserId = ur.User.PublicId,
                DisplayName = ur.User.DisplayName,
                AssignedAt = ur.AssignedAt
            })
            .ToListAsync(cancellationToken);

        // Get recent activity
        var recentActivity = await context.Posts
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

        var modUserIds = await context.UserRoles
            .Where(ur =>
                ur.RoleId == (int)UserRoleTypeEnum.HubMod
                && ur.HubId == hub.Id
                && ur.RevokedAt == null)
            .Select(ur => ur.User.PublicId)
            .ToListAsync(cancellationToken);

        var allowedTypes = await context.HubAllowedDiscussionTypes
            .Where(x => x.HubId == hub.Id)
            .Select(x => (DiscussionTypeEnum)x.DiscussionType)
            .ToListAsync(cancellationToken);

        return new HubSettingsDto
        {
            Slug = hub.Slug,
            Name = hub.Name,
            Description = hub.Description,
            LanguageCode = hub.LanguageCode,
            CommunityLanguageCode = hub.CommunityLanguageCode,
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

        hub.Name = request.Name;
        hub.Description = request.Description;

        if (request.LanguageCode is not null || hub.LanguageCode is not null)
        {
            var newLanguageCode = request.LanguageCode;
            if (newLanguageCode != hub.LanguageCode)
            {
                hub.LanguageCode = newLanguageCode;

                // Cascade to all child spaces
                await context.Spaces
                    .Where(s => s.HubId == hub.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(sp => sp.HubLanguageCode, newLanguageCode), cancellationToken);
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

        // Get pending reports
        var pendingReports = await context.Reports
            .Where(r =>
                r.HubId == hubDbId
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
            .Where(r => r.HubId == hubDbId)
            .CountAsync(cancellationToken);

        var pendingCount = pendingReports.Count;

        var resolvedCount = await context.Reports
            .Where(r =>
                r.HubId == hubDbId
                && r.StatusId == (int)ReportStatusEnum.Resolved)
            .CountAsync(cancellationToken);

        var dismissedCount = await context.Reports
            .Where(r =>
                r.HubId == hubDbId
                && r.StatusId == (int)ReportStatusEnum.Dismissed)
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
