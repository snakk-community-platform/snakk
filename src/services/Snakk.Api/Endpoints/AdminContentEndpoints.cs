namespace Snakk.Api.Endpoints;

using System.Security.Claims;
using Snakk.Application.DTOs.Admin;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.Services;

public static class AdminContentEndpoints
{
    public static void MapAdminContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/content")
            .WithTags("Admin - Content")
            .RequireAuthorization(policy => policy.RequireRole("GlobalAdmin"));

        // Overview
        group.MapGet("/overview", GetContentOverviewAsync)
            .WithName("AdminGetContentOverview")
            .Produces<ContentOverviewResponse>();

        // Communities
        group.MapGet("/communities", GetCommunitiesAsync)
            .WithName("AdminGetCommunities")
            .Produces<AdminCommunityListResponse>();

        group.MapGet("/communities/{id}", GetCommunityAsync)
            .WithName("AdminGetCommunity")
            .Produces<AdminCommunityDetailResponse>();

        // Hubs
        group.MapGet("/hubs", GetHubsAsync)
            .WithName("AdminGetHubs")
            .Produces<AdminHubListResponse>();

        // Spaces
        group.MapGet("/spaces", GetSpacesAsync)
            .WithName("AdminGetSpaces")
            .Produces<AdminSpaceListResponse>();

        // Discussions
        group.MapGet("/discussions", GetDiscussionsAsync)
            .WithName("AdminGetDiscussions")
            .Produces<AdminDiscussionListResponse>();

        group.MapGet("/discussions/{id}", GetDiscussionAsync)
            .WithName("AdminGetDiscussion")
            .Produces<AdminDiscussionDetailResponse>();

        group.MapPost("/discussions/{id}/pin", PinDiscussionAsync)
            .WithName("AdminPinDiscussion");

        group.MapDelete("/discussions/{id}/pin", UnpinDiscussionAsync)
            .WithName("AdminUnpinDiscussion");

        group.MapPost("/discussions/{id}/lock", LockDiscussionAsync)
            .WithName("AdminLockDiscussion");

        group.MapDelete("/discussions/{id}/lock", UnlockDiscussionAsync)
            .WithName("AdminUnlockDiscussion");

        group.MapDelete("/discussions/{id}", DeleteDiscussionAsync)
            .WithName("AdminDeleteDiscussion");
    }

    private static async Task<IResult> GetContentOverviewAsync(
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var counts = await adminContentService.GetContentOverviewExtendedAsync(ct);

        return TypedResults.Ok(new ContentOverviewResponse(
            counts.TotalCommunities,
            counts.TotalHubs,
            counts.TotalSpaces,
            counts.TotalDiscussions,
            counts.TotalPosts,
            counts.PinnedDiscussions,
            counts.LockedDiscussions));
    }

    private static async Task<IResult> GetCommunitiesAsync(
        int page,
        int pageSize,
        string? search,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var result = await adminContentService.GetCommunitiesPagedByPublicIdAsync(page, pageSize, search, ct);

        return TypedResults.Ok(new AdminCommunityListResponse(
            result.Items.Select(c => new AdminCommunityItemResponse(
                Id: c.PublicId,
                Name: c.Name,
                Slug: c.Slug,
                Description: c.Description,
                Visibility: c.Visibility,
                CreatedAt: c.CreatedAt,
                HubCount: c.HubCount,
                MemberCount: c.MemberCount)),
            result.Total,
            result.Page,
            result.PageSize));
    }

    private static async Task<IResult> GetCommunityAsync(
        string id,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var community = await adminContentService.GetCommunityAsync(id, ct);

        if (community is null)
            return Results.NotFound();

        return TypedResults.Ok(new AdminCommunityDetailResponse(
            Id: community.PublicId,
            Name: community.Name,
            Slug: community.Slug,
            Description: community.Description,
            Visibility: community.Visibility,
            CreatedAt: community.CreatedAt,
            HubCount: community.HubCount,
            MemberCount: community.MemberCount,
            Hubs: community.Hubs.Select(h => new AdminHubSummaryResponse(
                h.PublicId, h.Name, h.Slug, h.SpaceCount))));
    }

    private static async Task<IResult> GetHubsAsync(
        int page,
        int pageSize,
        string? search,
        string? communityId,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var result = await adminContentService.GetHubsPagedByPublicIdAsync(page, pageSize, search, communityId, ct);

        return TypedResults.Ok(new AdminHubListResponse(
            result.Items.Select(h => new AdminHubItemResponse(
                Id: h.PublicId,
                Name: h.Name,
                Slug: h.Slug,
                Description: h.Description,
                CommunityId: h.CommunityPublicId,
                CommunityName: h.CommunityName,
                CreatedAt: h.CreatedAt,
                SpaceCount: h.SpaceCount)),
            result.Total,
            result.Page,
            result.PageSize));
    }

    private static async Task<IResult> GetSpacesAsync(
        int page,
        int pageSize,
        string? search,
        string? hubId,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var result = await adminContentService.GetSpacesPagedByPublicIdAsync(page, pageSize, search, hubId, ct);

        return TypedResults.Ok(new AdminSpaceListResponse(
            result.Items.Select(s => new AdminSpaceItemResponse(
                Id: s.PublicId,
                Name: s.Name,
                Slug: s.Slug,
                Description: s.Description,
                HubId: s.HubPublicId,
                HubName: s.HubName,
                CommunityId: s.CommunityPublicId,
                CommunityName: s.CommunityName,
                CreatedAt: s.CreatedAt,
                DiscussionCount: s.DiscussionCount)),
            result.Total,
            result.Page,
            result.PageSize));
    }

    private static async Task<IResult> GetDiscussionsAsync(
        int page,
        int pageSize,
        string? search,
        string? spaceId,
        bool? isPinned,
        bool? isLocked,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var result = await adminContentService.GetDiscussionsPagedByPublicIdAsync(
            page, pageSize, search, spaceId, isPinned, isLocked, ct);

        return TypedResults.Ok(new AdminDiscussionListResponse(
            result.Items.Select(d => new AdminDiscussionItemResponse(
                Id: d.PublicId,
                Title: d.Title,
                Slug: d.Slug,
                SpaceId: d.SpacePublicId,
                SpaceName: d.SpaceName,
                HubId: d.HubPublicId,
                HubName: d.HubName,
                CommunityId: d.CommunityPublicId,
                CommunityName: d.CommunityName,
                AuthorId: d.AuthorPublicId,
                AuthorName: d.AuthorDisplayName,
                PostCount: d.PostCount,
                IsPinned: d.IsPinned,
                IsLocked: d.IsLocked,
                CreatedAt: d.CreatedAt,
                LastActivityAt: d.LastActivityAt)),
            result.Total,
            result.Page,
            result.PageSize));
    }

    private static async Task<IResult> GetDiscussionAsync(
        string id,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var discussion = await adminContentService.GetDiscussionAsync(id, ct);

        if (discussion is null)
            return Results.NotFound();

        return TypedResults.Ok(new AdminDiscussionDetailResponse(
            Id: discussion.PublicId,
            Title: discussion.Title,
            Slug: discussion.Slug,
            SpaceId: discussion.SpacePublicId,
            SpaceName: discussion.SpaceName,
            HubId: discussion.HubPublicId,
            HubName: discussion.HubName,
            CommunityId: discussion.CommunityPublicId,
            CommunityName: discussion.CommunityName,
            AuthorId: discussion.AuthorPublicId,
            AuthorName: discussion.AuthorDisplayName,
            PostCount: discussion.PostCount,
            ReactionCount: discussion.ReactionCount,
            IsPinned: discussion.IsPinned,
            IsLocked: discussion.IsLocked,
            Tags: discussion.Tags,
            CreatedAt: discussion.CreatedAt,
            LastActivityAt: discussion.LastActivityAt));
    }

    private static async Task<IResult> PinDiscussionAsync(
        string id,
        HttpContext httpContext,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var result = await adminContentService.PinDiscussionByPublicIdAsync(id, actorPublicId, ct);
        return MapMutationResult(result);
    }

    private static async Task<IResult> UnpinDiscussionAsync(
        string id,
        HttpContext httpContext,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var result = await adminContentService.UnpinDiscussionByPublicIdAsync(id, actorPublicId, ct);
        return MapMutationResult(result);
    }

    private static async Task<IResult> LockDiscussionAsync(
        string id,
        HttpContext httpContext,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var result = await adminContentService.LockDiscussionByPublicIdAsync(id, actorPublicId, ct);
        return MapMutationResult(result);
    }

    private static async Task<IResult> UnlockDiscussionAsync(
        string id,
        HttpContext httpContext,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var result = await adminContentService.UnlockDiscussionByPublicIdAsync(id, actorPublicId, ct);
        return MapMutationResult(result);
    }

    private static async Task<IResult> DeleteDiscussionAsync(
        string id,
        HttpContext httpContext,
        IAdminContentService adminContentService,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var result = await adminContentService.SoftDeleteDiscussionByPublicIdAsync(id, actorPublicId, ct);
        return MapMutationResult(result);
    }

    private static string? GetActorPublicId(HttpContext httpContext)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return null;

        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static IResult MapMutationResult(AdminDiscussionMutationResult result) =>
        result.Status switch
        {
            AdminDiscussionMutationStatus.Success => Results.NoContent(),
            AdminDiscussionMutationStatus.NotFound => Results.NotFound(new { error = "Discussion not found" }),
            AdminDiscussionMutationStatus.AlreadyInState => Results.BadRequest(new { error = result.ErrorMessage }),
            _ => Results.Problem("Unexpected error")
        };
}
