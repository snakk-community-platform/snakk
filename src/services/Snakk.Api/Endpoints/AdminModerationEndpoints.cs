namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;
using System.Security.Claims;

public static class AdminModerationEndpoints
{
    public static void MapAdminModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var userGroup = app.MapGroup("/admin/users")
            .WithTags("Admin - User Moderation")
            .RequireAuthorization(policy => policy.RequireRole("GlobalAdmin"));

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
            .RequireAuthorization(policy => policy.RequireRole("GlobalAdmin"));

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
        IAdminModerationDataService adminModerationData,
        CancellationToken ct)
    {
        var adminUserId = GetUserId(httpContext);

        if (adminUserId is null)
            return Results.Unauthorized();

        // Verify user exists
        if (!await adminUserService.UserExistsAsync(userId))
            return Results.NotFound(new { error = "User not found" });

        // Find active ban for this user
        var activeBanPublicId = await adminModerationData.GetActiveBanPublicIdAsync(userId, ct);

        if (activeBanPublicId is null)
            return Results.BadRequest(new { error = "User is not currently banned" });

        // Unban the user
        var result = await moderationUseCase.UnbanUserAsync(activeBanPublicId, adminUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateUserRoleAsync(
        string userId,
        UpdateUserRoleRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase,
        IAdminModerationDataService adminModerationData,
        CancellationToken ct)
    {
        var adminUserId = GetUserId(httpContext);

        if (adminUserId is null)
            return Results.Unauthorized();

        // Verify user exists and get database role info
        var userRoleInfo = await adminModerationData.GetUserRoleInfoAsync(userId, ct);

        if (userRoleInfo is null)
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
            if (userRoleInfo.ActivePlatformRolePublicId is not null)
            {
                var revokeResult = await moderationUseCase.RevokeRoleAsync(
                    userRoleInfo.ActivePlatformRolePublicId, adminUserId);

                if (!revokeResult.IsSuccess)
                    return Results.BadRequest(new { error = revokeResult.Error });
            }

            return Results.NoContent();
        }

        // Check if user already has a platform-wide role
        if (userRoleInfo.ActivePlatformRolePublicId is not null)
        {
            if (userRoleInfo.ActivePlatformRoleId == (int)roleType.Value)
            {
                return TypedResults.Ok(new MessageResponse("User already has this role"));
            }

            // Revoke existing role
            var revokeResult = await moderationUseCase.RevokeRoleAsync(
                userRoleInfo.ActivePlatformRolePublicId, adminUserId);

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
        IAdminModerationDataService adminModerationData,
        CancellationToken ct)
    {
        var report = await adminModerationData.GetReportDetailAsync(id, ct);

        if (report is null)
            return Results.NotFound(new { error = "Report not found" });

        return TypedResults.Ok(new AdminReportDetailResponse(
            Id: report.PublicId,
            ReporterId: report.ReporterPublicId,
            ReporterUsername: report.ReporterDisplayName,
            ReportedUserId: report.ReportedUserPublicId,
            ReportedUsername: report.ReportedUserDisplayName,
            PostId: report.ReportedPostPublicId,
            DiscussionId: report.ReportedDiscussionPublicId,
            Reason: report.ReasonName,
            ReasonDescription: report.ReasonDescription,
            Details: report.Details,
            Status: report.Status,
            CreatedAt: report.CreatedAt,
            ResolvedAt: report.ResolvedAt,
            ResolverId: report.ResolverPublicId,
            ResolverUsername: report.ResolverDisplayName,
            ResolutionNote: report.ResolutionNote));
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
