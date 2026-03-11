namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Responses;
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
            .WithName("AdminBanUser")
            .Produces<AdminBanResponse>();

        userGroup.MapDelete("/{userId}/ban", UnbanUserAsync)
            .WithName("AdminUnbanUser");

        userGroup.MapPut("/{userId}/role", UpdateUserRoleAsync)
            .WithName("AdminUpdateUserRole")
            .Produces<AdminRoleResponse>();

        var modGroup = app.MapGroup("/admin/moderation")
            .WithTags("Admin - Moderation Tools")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // Reports
        modGroup.MapGet("/reports", GetReportsAsync)
            .WithName("AdminGetReports")
            .Produces<AdminReportListResponse>();

        modGroup.MapGet("/reports/{id}", GetReportAsync)
            .WithName("AdminGetReport")
            .Produces<AdminReportDetailResponse>();

        modGroup.MapPost("/reports/{id}/resolve", ResolveReportAsync)
            .WithName("AdminResolveReport");

        modGroup.MapPost("/reports/{id}/dismiss", DismissReportAsync)
            .WithName("AdminDismissReport");

        // Moderation log
        modGroup.MapGet("/log", GetModerationLogAsync)
            .WithName("AdminGetModerationLog")
            .Produces<AdminModerationLogResponse>();

        // Active bans
        modGroup.MapGet("/bans", GetActiveBansAsync)
            .WithName("AdminGetActiveBans")
            .Produces<AdminActiveBansResponse>();
    }

    private static async Task<IResult> BanUserAsync(
        string userId,
        BanUserAdminRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase,
        IAdminUserService adminUserService)
    {
        var adminUserId = GetUserId(httpContext);

        if (adminUserId is null)
            return Results.Unauthorized();

        // Verify user exists
        if (!await adminUserService.UserExistsAsync(userId))
            return Results.NotFound(new { error = "User not found" });

        // Calculate expiry date if duration is provided (in days)
        var expiresAt = request.Duration is > 0
            ? DateTime.UtcNow.AddDays(request.Duration.Value)
            : (DateTime?)null;

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

        return TypedResults.Ok(new AdminBanResponse(
            result.Value!.PublicId,
            result.Value.BanType.ToString(),
            result.Value.BannedAt,
            result.Value.ExpiresAt,
            request.Reason));
    }

    private static async Task<IResult> UnbanUserAsync(
        string userId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase,
        IAdminUserService adminUserService,
        SnakkDbContext context)
    {
        var adminUserId = GetUserId(httpContext);

        if (adminUserId is null)
            return Results.Unauthorized();

        // Verify user exists
        if (!await adminUserService.UserExistsAsync(userId))
            return Results.NotFound(new { error = "User not found" });

        var now = DateTime.UtcNow;

        // Find active ban for this user
        var activeBan = await context.UserBans
            .FirstOrDefaultAsync(b =>
                b.User.PublicId == userId
                && b.UnbannedAt == null
                && (b.ExpiresAt == null || b.ExpiresAt > now));

        if (activeBan is null)
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

        if (adminUserId is null)
            return Results.Unauthorized();

        // Verify user exists - also need to get database Id for role lookups
        var targetUser = await context.Users
            .FirstOrDefaultAsync(u => u.PublicId == userId);

        if (targetUser is null)
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

        if (roleType is null && request.Role.ToLowerInvariant() != "user")
        {
            return Results.BadRequest(new { error = "Invalid role. Must be 'Admin' or 'User'" });
        }

        // If the role is "User", revoke any existing platform-wide role
        if (roleType is null)
        {
            var existingRole = await context.UserRoles
                .FirstOrDefaultAsync(ur =>
                    ur.UserId == targetUser.Id
                    && ur.CommunityId == null
                    && ur.HubId == null
                    && ur.SpaceId == null
                    && ur.RevokedAt == null);

            if (existingRole is not null)
            {
                var revokeResult = await moderationUseCase.RevokeRoleAsync(existingRole.PublicId, adminUserId);

                if (!revokeResult.IsSuccess)
                    return Results.BadRequest(new { error = revokeResult.Error });
            }

            return Results.NoContent();
        }

        // Check if user already has a platform-wide role
        var currentRole = await context.UserRoles
            .FirstOrDefaultAsync(ur =>
                ur.UserId == targetUser.Id
                && ur.CommunityId == null
                && ur.HubId == null
                && ur.SpaceId == null
                && ur.RevokedAt == null);

        // Check the role type
        if (currentRole is not null)
        {
            var currentRoleType = currentRole.RoleId;

            if (currentRoleType == (int)roleType.Value)
            {
                return TypedResults.Ok(new MessageResponse("User already has this role"));
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

        return TypedResults.Ok(new AdminRoleResponse(
            result.Value!.PublicId,
            result.Value.Role.ToString(),
            result.Value.AssignedAt));
    }

    // ==================== Reports Management ====================

    private static async Task<IResult> GetReportsAsync(
        int page,
        string? status,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var adminUserId = GetUserId(httpContext);

        if (adminUserId is null)
            return Results.Unauthorized();

        const int pageSize = 20;
        var offset = (page - 1) * pageSize;
        var statusId = ParseReportStatus(status);

        var result = await moderationUseCase.GetReportsForModeratorAsync(
            adminUserId, // Admin can see all reports platform-wide
            statusId,
            offset,
            pageSize);

        return TypedResults.Ok(new AdminReportListResponse(
            Reports: result.Items
                .Select(r => new AdminReportItemResponse(
                    Id: r.PublicId,
                    ReporterUsername: r.ReporterUserDisplayName,
                    ReportedUsername: r.ReportedUserDisplayName,
                    ContentType:
                        r.ReportedPostPublicId != null
                        ? "Post" : r.ReportedDiscussionPublicId != null
                            ? "Discussion" : r.ReportedUserPublicId != null
                                ? "User" : "Unknown",
                    Reason: r.ReasonName,
                    Status: r.Status,
                    CreatedAt: r.CreatedAt,
                    ResolvedAt: r.ResolvedAt,
                    ResolverUsername: r.ResolvedByUserDisplayName,
                    Details: r.Details))
                .ToList(),
            Total: result.Items.Count(),
            Page: page,
            PageSize: pageSize));
    }

    private static async Task<IResult> GetReportAsync(
        string id,
        ModerationUseCase moderationUseCase,
        SnakkDbContext context)
    {
        var report = await context.Reports
            .Where(r => r.PublicId == id)
            .Select(r => new AdminReportDetailResponse(
                Id: r.PublicId,
                ReporterId: r.ReporterUser.PublicId,
                ReporterUsername: r.ReporterUser.DisplayName,
                ReportedUserId: r.ReportedUser != null ? r.ReportedUser.PublicId : null,
                ReportedUsername: r.ReportedUser != null ? r.ReportedUser.DisplayName : null,
                PostId: r.ReportedPost != null ? r.ReportedPost.PublicId : null,
                DiscussionId: r.ReportedDiscussion != null ? r.ReportedDiscussion.PublicId : null,
                Reason: r.Reason != null ? r.Reason.Name : null,
                ReasonDescription: r.Reason != null ? r.Reason.Description : null,
                Details: r.Details,
                Status: ((Snakk.Shared.Enums.ReportStatusEnum)r.StatusId).ToString(),
                CreatedAt: r.CreatedAt,
                ResolvedAt: r.ResolvedAt,
                ResolverId: r.ResolvedByUser != null ? r.ResolvedByUser.PublicId : null,
                ResolverUsername: r.ResolvedByUser != null ? r.ResolvedByUser.DisplayName : null,
                ResolutionNote: r.ResolutionNote))
            .FirstOrDefaultAsync();

        if (report is null)
            return Results.NotFound(new { error = "Report not found" });

        return TypedResults.Ok(report);
    }

    private static async Task<IResult> ResolveReportAsync(
        string id,
        ResolveReportAdminRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var adminUserId = GetUserId(httpContext);

        if (adminUserId is null)
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

        if (adminUserId is null)
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

        return TypedResults.Ok(new AdminModerationLogResponse(
            Actions: result.Items
                .Select(a => new AdminModerationLogItemResponse(
                    Id: a.PublicId,
                    ActionType: a.Action,
                    ModeratorUsername: a.ActorUserDisplayName,
                    TargetType:
                        a.TargetPostPublicId != null
                        ? "Post" : a.TargetDiscussionPublicId != null
                            ? "Discussion" : a.TargetUserPublicId != null
                                ? "User" : "Unknown",
                    TargetId: a.TargetPostPublicId ?? a.TargetDiscussionPublicId ?? a.TargetUserPublicId ?? "",
                    Reason: a.Reason,
                    Details: a.Details,
                    CreatedAt: a.CreatedAt,
                    CommunityName: a.CommunityName,
                    HubName: a.HubName,
                    SpaceName: a.SpaceName))
                .ToList(),
            Total: result.Items.Count(),
            Page: page,
            PageSize: pageSize));
    }

    // ==================== Active Bans ====================

    private static async Task<IResult> GetActiveBansAsync(
        int page,
        IAdminUserService adminUserService)
    {
        const int pageSize = 20;

        var result = await adminUserService.GetActiveBansAsync(page, pageSize);

        return TypedResults.Ok(new AdminActiveBansResponse(
            Bans: result.Items
                .Select(b => new AdminActiveBanItemResponse(
                    UserId: b.UserId,
                    Username: b.UserDisplayName,
                    Reason: b.Reason,
                    BannedAt: b.BannedAt,
                    ExpiresAt: b.ExpiresAt,
                    BannedByUsername: b.BannedBy))
                .ToList(),
            Total: result.Total,
            Page: result.Page,
            PageSize: result.PageSize));
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
