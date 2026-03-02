namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.UseCases;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;
using System.Security.Claims;

public static class ModerationEndpoints
{
    public static void MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/moderation")
            .WithTags("Moderation")
            .RequireAuthorization();

        // Role management
        group.MapPost("/roles", AssignRoleAsync)
            .WithName("AssignRole")
            .Produces<RoleAssignedResponse>(201);

        group.MapDelete("/roles/{roleId}", RevokeRoleAsync)
            .WithName("RevokeRole");

        group.MapGet("/users/{userId}/roles", GetUserRolesAsync)
            .WithName("GetUserRoles")
            .Produces<UserRolesResponse>();

        // Ban management
        group.MapPost("/bans", BanUserAsync)
            .WithName("BanUser")
            .Produces<BanCreatedResponse>(201);

        group.MapDelete("/bans/{banId}", UnbanUserAsync)
            .WithName("UnbanUser");

        group.MapGet("/users/{userId}/banned", CheckUserBannedAsync)
            .WithName("CheckUserBanned")
            .Produces<BanStatusResponse>()
            .AllowAnonymous();

        // Reports
        group.MapPost("/reports", CreateReportAsync)
            .WithName("CreateReport")
            .Produces<ReportCreatedResponse>(201);

        group.MapGet("/reports", GetReportsAsync)
            .WithName("GetReports")
            .Produces<PagedResponse<object>>();

        group.MapGet("/reports/{reportId}", GetReportAsync)
            .WithName("GetReport");

        group.MapPost("/reports/{reportId}/resolve", ResolveReportAsync)
            .WithName("ResolveReport");

        group.MapPost("/reports/{reportId}/dismiss", DismissReportAsync)
            .WithName("DismissReport");

        group.MapPost("/reports/{reportId}/comments", AddReportCommentAsync)
            .WithName("AddReportComment")
            .Produces<ReportCommentCreatedResponse>(201);

        group.MapGet("/reports/reasons", GetReportReasonsAsync)
            .WithName("GetReportReasons")
            .Produces<ReportReasonsResponse>()
            .AllowAnonymous();

        // Content moderation
        group.MapPost("/posts/{postId}/delete", ModeratorDeletePostAsync)
            .WithName("ModeratorDeletePost");

        group.MapPost("/discussions/{discussionId}/delete", ModeratorDeleteDiscussionAsync)
            .WithName("ModeratorDeleteDiscussion");

        group.MapPost("/discussions/{discussionId}/lock", LockDiscussionAsync)
            .WithName("LockDiscussion");

        group.MapPost("/discussions/{discussionId}/unlock", UnlockDiscussionAsync)
            .WithName("UnlockDiscussion");

        // Moderation log
        group.MapGet("/log", GetModerationLogAsync)
            .WithName("GetModerationLog")
            .Produces<PagedResponse<object>>();
    }

    // ==================== Role Management ====================

    private static async Task<IResult> AssignRoleAsync(
        AssignRoleRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var assignerUserId = GetUserId(httpContext);

        if (assignerUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.AssignRoleAsync(
            request.TargetUserId,
            request.RoleType.ToDomain(),
            request.CommunityId,
            request.HubId,
            request.SpaceId,
            assignerUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Created(
            $"/moderation/roles/{result.Value!.PublicId}",
            new RoleAssignedResponse(result.Value.PublicId, result.Value.Role, result.Value.AssignedAt));
    }

    private static async Task<IResult> RevokeRoleAsync(
        string roleId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var revokerUserId = GetUserId(httpContext);

        if (revokerUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.RevokeRoleAsync(roleId, revokerUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> GetUserRolesAsync(
        string userId,
        ModerationUseCase moderationUseCase)
    {
        var roles = await moderationUseCase.GetUserRolesAsync(userId);

        return TypedResults.Ok(new UserRolesResponse(roles.Select(r => new UserRoleItemResponse(
            r.PublicId,
            r.Role,
            r.CommunityPublicId,
            r.CommunityName,
            r.HubPublicId,
            r.HubName,
            r.SpacePublicId,
            r.SpaceName,
            r.AssignedAt))));
    }

    // ==================== Ban Management ====================

    private static async Task<IResult> BanUserAsync(
        BanUserRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var bannerUserId = GetUserId(httpContext);

        if (bannerUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.BanUserAsync(
            request.TargetUserId,
            request.BanType.ToDomain(),
            request.CommunityId,
            request.HubId,
            request.SpaceId,
            request.Reason,
            request.ExpiresAt,
            bannerUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Created(
            $"/moderation/bans/{result.Value!.PublicId}",
            new BanCreatedResponse(result.Value.PublicId, result.Value.BanType, result.Value.BannedAt, result.Value.ExpiresAt));
    }

    private static async Task<IResult> UnbanUserAsync(
        string banId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var unbannerUserId = GetUserId(httpContext);

        if (unbannerUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.UnbanUserAsync(banId, unbannerUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> CheckUserBannedAsync(
        string userId,
        string? spaceId,
        ModerationUseCase moderationUseCase)
    {
        var isBanned = await moderationUseCase.IsUserBannedAsync(userId, spaceId);
        return TypedResults.Ok(new BanStatusResponse(isBanned));
    }

    // ==================== Report Management ====================

    private static async Task<IResult> CreateReportAsync(
        CreateReportRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var reporterUserId = GetUserId(httpContext);

        if (reporterUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.CreateReportAsync(
            reporterUserId,
            request.PostId,
            request.DiscussionId,
            request.UserId,
            request.ReasonId,
            request.Details);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Created(
            $"/moderation/reports/{result.Value!.PublicId}",
            new ReportCreatedResponse(result.Value.PublicId, result.Value.Status, result.Value.CreatedAt));
    }

    private static async Task<IResult> GetReportsAsync(
        string? status,
        int offset,
        int pageSize,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);

        if (moderatorUserId is null)
            return Results.Unauthorized();

        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);
        var statusId = ParseReportStatus(status);

        var result = await moderationUseCase.GetReportsForModeratorAsync(
            moderatorUserId, statusId, offset, pageSize);

        return TypedResults.Ok(new PagedResponse<object>(
            result.Items,
            result.Offset,
            result.PageSize,
            result.HasMoreItems));
    }

    private static async Task<IResult> GetReportAsync(
        string reportId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase) =>
        // TODO: Implement GetReportByIdAsync in use case
        Results.NotFound();

    private static async Task<IResult> ResolveReportAsync(
        string reportId,
        ResolveReportRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var resolverUserId = GetUserId(httpContext);

        if (resolverUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.ResolveReportAsync(
            reportId, resolverUserId, request.ResolutionNote, dismiss: false);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> DismissReportAsync(
        string reportId,
        ResolveReportRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var resolverUserId = GetUserId(httpContext);

        if (resolverUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.ResolveReportAsync(
            reportId, resolverUserId, request.ResolutionNote, dismiss: true);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> AddReportCommentAsync(
        string reportId,
        AddReportCommentRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var authorUserId = GetUserId(httpContext);

        if (authorUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.AddReportCommentAsync(
            reportId, authorUserId, request.Content);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return TypedResults.Created(
            $"/moderation/reports/{reportId}/comments/{result.Value!.PublicId}",
            new ReportCommentCreatedResponse(result.Value.PublicId, result.Value.Content, result.Value.CreatedAt));
    }

    private static async Task<IResult> GetReportReasonsAsync(
        string? spaceId,
        ModerationUseCase moderationUseCase)
    {
        var reasons = await moderationUseCase.GetReportReasonsAsync(spaceId);

        return TypedResults.Ok(new ReportReasonsResponse(reasons.Select(r =>
            new ReportReasonResponse(r.PublicId, r.Name, r.Description))));
    }

    // ==================== Content Moderation ====================

    private static async Task<IResult> ModeratorDeletePostAsync(
        string postId,
        ModerationActionRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);

        if (moderatorUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.ModeratorDeletePostAsync(
            postId, moderatorUserId, request.Reason);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> ModeratorDeleteDiscussionAsync(
        string discussionId,
        ModerationActionRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);

        if (moderatorUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.ModeratorDeleteDiscussionAsync(
            discussionId, moderatorUserId, request.Reason);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> LockDiscussionAsync(
        string discussionId,
        ModerationActionRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);

        if (moderatorUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.LockDiscussionAsync(
            discussionId, moderatorUserId, request.Reason);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    private static async Task<IResult> UnlockDiscussionAsync(
        string discussionId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);

        if (moderatorUserId is null)
            return Results.Unauthorized();

        var result = await moderationUseCase.UnlockDiscussionAsync(
            discussionId, moderatorUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }

    // ==================== Moderation Log ====================

    private static async Task<IResult> GetModerationLogAsync(
        string? communityId,
        string? hubId,
        string? spaceId,
        int offset,
        int pageSize,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);

        if (moderatorUserId is null)
            return Results.Unauthorized();

        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        // Permission check is handled inside ModerationUseCase.GetModerationLogAsync
        var result = await moderationUseCase.GetModerationLogAsync(
            communityId, hubId, spaceId, offset, pageSize);

        return TypedResults.Ok(new PagedResponse<object>(
            result.Items,
            result.Offset,
            result.PageSize,
            result.HasMoreItems));
    }

    // ==================== Helpers ====================

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
