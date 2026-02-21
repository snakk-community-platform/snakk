namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;
using System.Security.Claims;

public static class AdminModerationEndpoints
{
    public static void MapAdminModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var userGroup = app.MapGroup("/admin/users")
            .WithTags("Admin - User Moderation")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // Simplified admin endpoints that work with userId instead of banId/roleId
        userGroup.MapPost("/{userId}/ban", BanUserAsync)
            .WithName("AdminBanUser");

        userGroup.MapDelete("/{userId}/ban", UnbanUserAsync)
            .WithName("AdminUnbanUser");

        userGroup.MapPut("/{userId}/role", UpdateUserRoleAsync)
            .WithName("AdminUpdateUserRole");

        var modGroup = app.MapGroup("/admin/moderation")
            .WithTags("Admin - Moderation Tools")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // Reports
        modGroup.MapGet("/reports", GetReportsAsync)
            .WithName("AdminGetReports");

        modGroup.MapGet("/reports/{id}", GetReportAsync)
            .WithName("AdminGetReport");

        modGroup.MapPost("/reports/{id}/resolve", ResolveReportAsync)
            .WithName("AdminResolveReport");

        modGroup.MapPost("/reports/{id}/dismiss", DismissReportAsync)
            .WithName("AdminDismissReport");

        // Moderation log
        modGroup.MapGet("/log", GetModerationLogAsync)
            .WithName("AdminGetModerationLog");

        // Active bans
        modGroup.MapGet("/bans", GetActiveBansAsync)
            .WithName("AdminGetActiveBans");
    }

    private static async Task<IResult> BanUserAsync(
        string userId,
        BanUserAdminRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase,
        IAdminUserService adminUserService)
    {
        var adminUserId = GetUserId(httpContext);
        if (adminUserId == null)
            return Results.Unauthorized();

        // Verify user exists
        if (!await adminUserService.UserExistsAsync(userId))
            return Results.NotFound(new { error = "User not found" });

        // Calculate expiry date if duration is provided (in days)
        DateTime? expiresAt = null;
        if (request.Duration.HasValue && request.Duration.Value > 0)
        {
            expiresAt = DateTime.UtcNow.AddDays(request.Duration.Value);
        }

        // Ban the user (platform-wide ban with full read/write restrictions)
        var result = await moderationUseCase.BanUserAsync(
            userId,
            BanType.ReadWrite, // Full ban - cannot read or write
            null, // communityPublicId
            null, // hubPublicId
            null, // spacePublicId
            request.Reason,
            expiresAt,
            adminUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId,
            banType = result.Value.BanType.ToString(),
            bannedAt = result.Value.BannedAt,
            expiresAt = result.Value.ExpiresAt,
            reason = request.Reason
        });
    }

    private static async Task<IResult> UnbanUserAsync(
        string userId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase,
        IAdminUserService adminUserService,
        SnakkDbContext context)
    {
        var adminUserId = GetUserId(httpContext);
        if (adminUserId == null)
            return Results.Unauthorized();

        // Verify user exists
        if (!await adminUserService.UserExistsAsync(userId))
            return Results.NotFound(new { error = "User not found" });

        var now = DateTime.UtcNow;

        // Find active ban for this user
        var activeBan = await context.UserBans
            .FirstOrDefaultAsync(b =>
                b.User.PublicId == userId &&
                b.UnbannedAt == null &&
                (b.ExpiresAt == null || b.ExpiresAt > now));

        if (activeBan == null)
            return Results.BadRequest(new { error = "User is not currently banned" });

        // Unban the user
        var result = await moderationUseCase.UnbanUserAsync(activeBan.PublicId, adminUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateUserRoleAsync(
        string userId,
        UpdateUserRoleRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase,
        IAdminUserService adminUserService,
        SnakkDbContext context)
    {
        var adminUserId = GetUserId(httpContext);
        if (adminUserId == null)
            return Results.Unauthorized();

        // Verify user exists - also need to get database Id for role lookups
        var targetUser = await context.Users
            .FirstOrDefaultAsync(u => u.PublicId == userId);
        if (targetUser == null)
            return Results.NotFound(new { error = "User not found" });

        // Parse role type - only support GlobalAdmin for now (simple platform-wide admin)
        var roleType = request.Role.ToLowerInvariant() switch
        {
            "admin" => UserRoleType.GlobalAdmin,
            "moderator" => UserRoleType.CommunityMod, // Note: Requires community scope in full implementation
            "user" => (UserRoleType?)null, // Revoke role
            _ => (UserRoleType?)null
        };

        // Validate role
        if (request.Role.ToLowerInvariant() == "moderator")
        {
            return Results.BadRequest(new { error = "Community moderator role requires a community scope. Use platform admin instead." });
        }

        if (roleType == null && request.Role.ToLowerInvariant() != "user")
        {
            return Results.BadRequest(new { error = "Invalid role. Must be 'Admin' or 'User'" });
        }

        // If the role is "User", revoke any existing platform-wide role
        if (roleType == null)
        {
            var existingRole = await context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == targetUser.Id &&
                                          ur.CommunityId == null &&
                                          ur.HubId == null &&
                                          ur.SpaceId == null &&
                                          ur.RevokedAt == null);

            if (existingRole != null)
            {
                var revokeResult = await moderationUseCase.RevokeRoleAsync(existingRole.PublicId, adminUserId);
                if (!revokeResult.IsSuccess)
                    return Results.BadRequest(new { error = revokeResult.Error });
            }

            return Results.NoContent();
        }

        // Check if user already has a platform-wide role
        var currentRole = await context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == targetUser.Id &&
                                      ur.CommunityId == null &&
                                      ur.HubId == null &&
                                      ur.SpaceId == null &&
                                      ur.RevokedAt == null);

        // Check the role type
        if (currentRole != null)
        {
            var currentRoleType = currentRole.RoleId;

            if (currentRoleType == (int)roleType.Value)
            {
                return Results.Ok(new { message = "User already has this role" });
            }

            // Revoke existing role
            var revokeResult = await moderationUseCase.RevokeRoleAsync(currentRole.PublicId, adminUserId);
            if (!revokeResult.IsSuccess)
                return Results.BadRequest(new { error = revokeResult.Error });
        }

        // Assign new role (platform-wide, not scoped to community/hub/space)
        var result = await moderationUseCase.AssignRoleAsync(
            userId,
            roleType.Value,
            null, // communityPublicId
            null, // hubPublicId
            null, // spacePublicId
            adminUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId,
            role = result.Value.Role.ToString(),
            assignedAt = result.Value.AssignedAt
        });
    }

    // ==================== Reports Management ====================

    private static async Task<IResult> GetReportsAsync(
        int page,
        string? status,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var adminUserId = GetUserId(httpContext);
        if (adminUserId == null)
            return Results.Unauthorized();

        const int pageSize = 20;
        var offset = (page - 1) * pageSize;

        var statusId = ParseReportStatus(status);

        var result = await moderationUseCase.GetReportsForModeratorAsync(
            adminUserId, // Admin can see all reports platform-wide
            statusId,
            offset,
            pageSize);

        return Results.Ok(new
        {
            reports = result.Items.Select(r => new
            {
                id = r.PublicId,
                reporterUsername = r.ReporterUserDisplayName,
                reportedUsername = r.ReportedUserDisplayName,
                contentType = r.ReportedPostPublicId != null ? "Post" :
                             r.ReportedDiscussionPublicId != null ? "Discussion" :
                             r.ReportedUserPublicId != null ? "User" : "Unknown",
                reason = r.ReasonName,
                status = r.Status,
                createdAt = r.CreatedAt,
                resolvedAt = r.ResolvedAt,
                resolverUsername = r.ResolvedByUserDisplayName,
                details = r.Details
            }).ToList(),
            total = result.Items.Count(), // TODO: Get actual total count from use case
            page,
            pageSize
        });
    }

    private static async Task<IResult> GetReportAsync(
        string id,
        ModerationUseCase moderationUseCase,
        SnakkDbContext context)
    {
        var report = await context.Reports
            .Include(r => r.ReporterUser)
            .Include(r => r.ReportedUser)
            .Include(r => r.ReportedPost)
            .Include(r => r.ReportedDiscussion)
            .Include(r => r.Reason)
            .Include(r => r.ResolvedByUser)
            .FirstOrDefaultAsync(r => r.PublicId == id);

        if (report == null)
            return Results.NotFound(new { error = "Report not found" });

        return Results.Ok(new
        {
            id = report.PublicId,
            reporterId = report.ReporterUser.PublicId,
            reporterUsername = report.ReporterUser.DisplayName,
            reportedUserId = report.ReportedUser?.PublicId,
            reportedUsername = report.ReportedUser?.DisplayName,
            postId = report.ReportedPost?.PublicId,
            discussionId = report.ReportedDiscussion?.PublicId,
            reason = report.Reason?.Name,
            reasonDescription = report.Reason?.Description,
            details = report.Details,
            status = ((Snakk.Shared.Enums.ReportStatusEnum)report.StatusId).ToString(),
            createdAt = report.CreatedAt,
            resolvedAt = report.ResolvedAt,
            resolverId = report.ResolvedByUser?.PublicId,
            resolverUsername = report.ResolvedByUser?.DisplayName,
            resolutionNote = report.ResolutionNote
        });
    }

    private static async Task<IResult> ResolveReportAsync(
        string id,
        ResolveReportAdminRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var adminUserId = GetUserId(httpContext);
        if (adminUserId == null)
            return Results.Unauthorized();

        var result = await moderationUseCase.ResolveReportAsync(
            id,
            adminUserId,
            request.ResolutionNote,
            dismiss: false);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> DismissReportAsync(
        string id,
        ResolveReportAdminRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var adminUserId = GetUserId(httpContext);
        if (adminUserId == null)
            return Results.Unauthorized();

        var result = await moderationUseCase.ResolveReportAsync(
            id,
            adminUserId,
            request.ResolutionNote,
            dismiss: true);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    // ==================== Moderation Log ====================

    private static async Task<IResult> GetModerationLogAsync(
        int page,
        string? actionType,
        ModerationUseCase moderationUseCase)
    {
        const int pageSize = 20;
        var offset = (page - 1) * pageSize;

        // Admin can see all moderation actions across the entire platform
        var result = await moderationUseCase.GetModerationLogAsync(
            null, // communityId
            null, // hubId
            null, // spaceId
            offset,
            pageSize);

        return Results.Ok(new
        {
            actions = result.Items.Select(a => new
            {
                id = a.PublicId,
                actionType = a.Action,
                moderatorUsername = a.ActorUserDisplayName,
                targetType = a.TargetPostPublicId != null ? "Post" :
                            a.TargetDiscussionPublicId != null ? "Discussion" :
                            a.TargetUserPublicId != null ? "User" : "Unknown",
                targetId = a.TargetPostPublicId ?? a.TargetDiscussionPublicId ?? a.TargetUserPublicId ?? "",
                reason = a.Reason,
                details = a.Details,
                createdAt = a.CreatedAt,
                communityName = a.CommunityName,
                hubName = a.HubName,
                spaceName = a.SpaceName
            }).ToList(),
            total = result.Items.Count(), // TODO: Get actual total count
            page,
            pageSize
        });
    }

    // ==================== Active Bans ====================

    private static async Task<IResult> GetActiveBansAsync(
        int page,
        IAdminUserService adminUserService)
    {
        const int pageSize = 20;

        var result = await adminUserService.GetActiveBansAsync(page, pageSize);

        return Results.Ok(new
        {
            bans = result.Items.Select(b => new
            {
                userId = b.UserId,
                username = b.UserDisplayName,
                reason = b.Reason,
                bannedAt = b.BannedAt,
                expiresAt = b.ExpiresAt,
                bannedByUsername = b.BannedBy
            }).ToList(),
            total = result.Total,
            page = result.Page,
            pageSize = result.PageSize
        });
    }

    private static string? GetUserId(HttpContext httpContext)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return null;

        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static int? ParseReportStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "pending" => (int)ReportStatusEnum.Pending,
        "resolved" => (int)ReportStatusEnum.Resolved,
        "dismissed" => (int)ReportStatusEnum.Dismissed,
        _ => null
    };
}

// Request models
public record BanUserAdminRequest(string Reason, int? Duration);
public record UpdateUserRoleRequest(string Role);
public record ResolveReportAdminRequest(string? ResolutionNote);
