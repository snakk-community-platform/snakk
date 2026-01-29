namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using System.Security.Claims;

public static class ModerationEndpoints
{
    public static void MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/moderation")
            .WithTags("Moderation")
            .RequireAuthorization();

        // Role management
        group.MapPost("/roles", AssignRoleAsync)
            .WithName("AssignRole");

        group.MapDelete("/roles/{roleId}", RevokeRoleAsync)
            .WithName("RevokeRole");

        group.MapGet("/users/{userId}/roles", GetUserRolesAsync)
            .WithName("GetUserRoles");

        // Ban management
        group.MapPost("/bans", BanUserAsync)
            .WithName("BanUser");

        group.MapDelete("/bans/{banId}", UnbanUserAsync)
            .WithName("UnbanUser");

        group.MapGet("/users/{userId}/banned", CheckUserBannedAsync)
            .WithName("CheckUserBanned")
            .AllowAnonymous();

        // Reports
        group.MapPost("/reports", CreateReportAsync)
            .WithName("CreateReport");

        group.MapGet("/reports", GetReportsAsync)
            .WithName("GetReports");

        group.MapGet("/reports/{reportId}", GetReportAsync)
            .WithName("GetReport");

        group.MapPost("/reports/{reportId}/resolve", ResolveReportAsync)
            .WithName("ResolveReport");

        group.MapPost("/reports/{reportId}/dismiss", DismissReportAsync)
            .WithName("DismissReport");

        group.MapPost("/reports/{reportId}/comments", AddReportCommentAsync)
            .WithName("AddReportComment");

        group.MapGet("/reports/reasons", GetReportReasonsAsync)
            .WithName("GetReportReasons")
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
            .WithName("GetModerationLog");
    }

    // ==================== Role Management ====================

    private static async Task<IResult> AssignRoleAsync(
        AssignRoleRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var assignerUserId = GetUserId(httpContext);
        if (assignerUserId == null)
            return Results.Unauthorized();

        if (!Enum.TryParse<UserRoleType>(request.RoleType, out var roleType))
            return Results.BadRequest(new { error = "Invalid role type" });

        var result = await moderationUseCase.AssignRoleAsync(
            request.TargetUserId,
            roleType,
            request.CommunityId,
            request.HubId,
            request.SpaceId,
            assignerUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/api/moderation/roles/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId,
            role = result.Value.Role,
            assignedAt = result.Value.AssignedAt
        });
    }

    private static async Task<IResult> RevokeRoleAsync(
        string roleId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var revokerUserId = GetUserId(httpContext);
        if (revokerUserId == null)
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

        return Results.Ok(new
        {
            items = roles.Select(r => new
            {
                publicId = r.PublicId,
                role = r.Role,
                communityId = r.CommunityPublicId,
                communityName = r.CommunityName,
                hubId = r.HubPublicId,
                hubName = r.HubName,
                spaceId = r.SpacePublicId,
                spaceName = r.SpaceName,
                assignedAt = r.AssignedAt
            })
        });
    }

    // ==================== Ban Management ====================

    private static async Task<IResult> BanUserAsync(
        BanUserRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var bannerUserId = GetUserId(httpContext);
        if (bannerUserId == null)
            return Results.Unauthorized();

        if (!Enum.TryParse<BanType>(request.BanType, out var banType))
            return Results.BadRequest(new { error = "Invalid ban type" });

        var result = await moderationUseCase.BanUserAsync(
            request.TargetUserId,
            banType,
            request.CommunityId,
            request.HubId,
            request.SpaceId,
            request.Reason,
            request.ExpiresAt,
            bannerUserId);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/api/moderation/bans/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId,
            banType = result.Value.BanType,
            bannedAt = result.Value.BannedAt,
            expiresAt = result.Value.ExpiresAt
        });
    }

    private static async Task<IResult> UnbanUserAsync(
        string banId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var unbannerUserId = GetUserId(httpContext);
        if (unbannerUserId == null)
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
        return Results.Ok(new { isBanned });
    }

    // ==================== Report Management ====================

    private static async Task<IResult> CreateReportAsync(
        CreateReportRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var reporterUserId = GetUserId(httpContext);
        if (reporterUserId == null)
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

        return Results.Created($"/api/moderation/reports/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId,
            status = result.Value.Status,
            createdAt = result.Value.CreatedAt
        });
    }

    private static async Task<IResult> GetReportsAsync(
        string? status,
        int offset,
        int pageSize,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);
        if (moderatorUserId == null)
            return Results.Unauthorized();

        var result = await moderationUseCase.GetReportsForModeratorAsync(
            moderatorUserId, status, offset, pageSize);

        return Results.Ok(new
        {
            items = result.Items,
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }

    private static async Task<IResult> GetReportAsync(
        string reportId,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        // TODO: Implement GetReportByIdAsync in use case
        return Results.NotFound();
    }

    private static async Task<IResult> ResolveReportAsync(
        string reportId,
        ResolveReportRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var resolverUserId = GetUserId(httpContext);
        if (resolverUserId == null)
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
        if (resolverUserId == null)
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
        if (authorUserId == null)
            return Results.Unauthorized();

        var result = await moderationUseCase.AddReportCommentAsync(
            reportId, authorUserId, request.Content);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/api/moderation/reports/{reportId}/comments/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId,
            content = result.Value.Content,
            createdAt = result.Value.CreatedAt
        });
    }

    private static async Task<IResult> GetReportReasonsAsync(
        string? spaceId,
        ModerationUseCase moderationUseCase)
    {
        var reasons = await moderationUseCase.GetReportReasonsAsync(spaceId);

        return Results.Ok(new
        {
            items = reasons.Select(r => new
            {
                publicId = r.PublicId,
                name = r.Name,
                description = r.Description
            })
        });
    }

    // ==================== Content Moderation ====================

    private static async Task<IResult> ModeratorDeletePostAsync(
        string postId,
        ModerationActionRequest request,
        HttpContext httpContext,
        ModerationUseCase moderationUseCase)
    {
        var moderatorUserId = GetUserId(httpContext);
        if (moderatorUserId == null)
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
        if (moderatorUserId == null)
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
        if (moderatorUserId == null)
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
        if (moderatorUserId == null)
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
        if (moderatorUserId == null)
            return Results.Unauthorized();

        // TODO: Add permission check for viewing logs

        var result = await moderationUseCase.GetModerationLogAsync(
            communityId, hubId, spaceId, offset, pageSize);

        return Results.Ok(new
        {
            items = result.Items,
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }

    // ==================== Helpers ====================

    private static string? GetUserId(HttpContext httpContext)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return null;

        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
