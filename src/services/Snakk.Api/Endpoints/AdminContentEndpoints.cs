namespace Snakk.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Snakk.Application.DTOs.Responses;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

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

    private static async Task<IResult> GetContentOverviewAsync(SnakkDbContext context, CancellationToken ct)
    {
        var counts = new ContentOverviewCounts(
            TotalCommunities:  await context.Communities.CountAsync(x => !x.IsDeleted, ct),
            TotalHubs:         await context.Hubs.CountAsync(x => !x.IsDeleted, ct),
            TotalSpaces:       await context.Spaces.CountAsync(x => !x.IsDeleted, ct),
            TotalDiscussions:  await context.Discussions.CountAsync(x => !x.IsDeleted, ct),
            TotalPosts:        await context.Posts.CountAsync(x => !x.IsDeleted, ct),
            PinnedDiscussions: await context.Discussions.CountAsync(x => !x.IsDeleted && x.IsPinned, ct),
            LockedDiscussions: await context.Discussions.CountAsync(x => !x.IsDeleted && x.IsLocked, ct)
        );

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
        SnakkDbContext context,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var query = context.Communities
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(searchLower)
                || c.Slug.ToLower().Contains(searchLower));
        }

        var total = await query.CountAsync(ct);

        var communities = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCommunityItemResponse(
                Id: c.PublicId,
                Name: c.Name,
                Slug: c.Slug,
                Description: c.Description,
                Visibility: ((CommunityVisibilityEnum)c.VisibilityId).ToString(),
                CreatedAt: c.CreatedAt,
                HubCount: c.HubCount,
                MemberCount: 0))
            .ToListAsync(ct);

        return TypedResults.Ok(new AdminCommunityListResponse(
            communities,
            total,
            page,
            pageSize));
    }

    private static async Task<IResult> GetCommunityAsync(
        string id,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var community = await context.Communities
            .Where(c =>
                c.PublicId == id
                && !c.IsDeleted)
            .Select(c => new AdminCommunityDetailResponse(
                Id: c.PublicId,
                Name: c.Name,
                Slug: c.Slug,
                Description: c.Description,
                Visibility: ((CommunityVisibilityEnum)c.VisibilityId).ToString(),
                CreatedAt: c.CreatedAt,
                HubCount: c.HubCount,
                MemberCount: 0,
                Hubs: context.Hubs
                    .Where(h =>
                        h.CommunityId == c.Id
                        && !h.IsDeleted)
                    .Select(h => new AdminHubSummaryResponse(
                        h.PublicId,
                        h.Name,
                        h.Slug,
                        h.SpaceCount))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (community is null)
            return Results.NotFound();

        return TypedResults.Ok(community);
    }

    private static async Task<IResult> GetHubsAsync(
        int page,
        int pageSize,
        string? search,
        string? communityId,
        SnakkDbContext context,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var query = context.Hubs
            .Where(h => !h.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(h =>
                h.Name.ToLower().Contains(searchLower)
                || h.Slug.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(communityId))
            query = query.Where(h => h.Community.PublicId == communityId);

        var total = await query.CountAsync(ct);

        var hubs = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new AdminHubItemResponse(
                Id: h.PublicId,
                Name: h.Name,
                Slug: h.Slug,
                Description: h.Description,
                CommunityId: h.Community.PublicId,
                CommunityName: h.Community.Name,
                CreatedAt: h.CreatedAt,
                SpaceCount: h.SpaceCount))
            .ToListAsync(ct);

        return TypedResults.Ok(new AdminHubListResponse(
            hubs,
            total,
            page,
            pageSize));
    }

    private static async Task<IResult> GetSpacesAsync(
        int page,
        int pageSize,
        string? search,
        string? hubId,
        SnakkDbContext context,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var query = context.Spaces
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchLower)
                || s.Slug.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(hubId))
            query = query.Where(s => s.Hub.PublicId == hubId);

        var total = await query.CountAsync(ct);

        var spaces = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AdminSpaceItemResponse(
                Id: s.PublicId,
                Name: s.Name,
                Slug: s.Slug,
                Description: s.Description,
                HubId: s.Hub.PublicId,
                HubName: s.Hub.Name,
                CommunityId: s.Hub.Community.PublicId,
                CommunityName: s.Hub.Community.Name,
                CreatedAt: s.CreatedAt,
                DiscussionCount: s.DiscussionCount))
            .ToListAsync(ct);

        return TypedResults.Ok(new AdminSpaceListResponse(
            spaces,
            total,
            page,
            pageSize));
    }

    private static async Task<IResult> GetDiscussionsAsync(
        int page,
        int pageSize,
        string? search,
        string? spaceId,
        bool? isPinned,
        bool? isLocked,
        SnakkDbContext context,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);

        var query = context.Discussions
            .Where(d => !d.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(spaceId))
            query = query.Where(d => d.Space.PublicId == spaceId);

        if (isPinned.HasValue)
            query = query.Where(d => d.IsPinned == isPinned.Value);

        if (isLocked.HasValue)
            query = query.Where(d => d.IsLocked == isLocked.Value);

        var total = await query.CountAsync(ct);

        var discussions = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new AdminDiscussionItemResponse(
                Id: d.PublicId,
                Title: d.Title,
                Slug: d.Slug,
                SpaceId: d.Space.PublicId,
                SpaceName: d.Space.Name,
                HubId: d.Space.Hub.PublicId,
                HubName: d.Space.Hub.Name,
                CommunityId: d.Space.Hub.Community.PublicId,
                CommunityName: d.Space.Hub.Community.Name,
                AuthorId: d.CreatedByUser.PublicId,
                AuthorName: d.CreatedByUser.DisplayName ?? "",
                PostCount: d.PostCount,
                IsPinned: d.IsPinned,
                IsLocked: d.IsLocked,
                CreatedAt: d.CreatedAt,
                LastActivityAt: d.LastActivityAt))
            .ToListAsync(ct);

        return TypedResults.Ok(new AdminDiscussionListResponse(
            discussions,
            total,
            page,
            pageSize));
    }

    private static async Task<IResult> GetDiscussionAsync(
        string id,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var discussion = await context.Discussions
            .Where(d =>
                d.PublicId == id
                && !d.IsDeleted)
            .Select(d => new AdminDiscussionDetailResponse(
                Id: d.PublicId,
                Title: d.Title,
                Slug: d.Slug,
                SpaceId: d.Space.PublicId,
                SpaceName: d.Space.Name,
                HubId: d.Space.Hub.PublicId,
                HubName: d.Space.Hub.Name,
                CommunityId: d.Space.Hub.Community.PublicId,
                CommunityName: d.Space.Hub.Community.Name,
                AuthorId: d.CreatedByUser.PublicId,
                AuthorName: d.CreatedByUser.DisplayName ?? "",
                PostCount: d.PostCount,
                ReactionCount: d.ReactionCount,
                IsPinned: d.IsPinned,
                IsLocked: d.IsLocked,
                Tags: d.Tags,
                CreatedAt: d.CreatedAt,
                LastActivityAt: d.LastActivityAt))
            .FirstOrDefaultAsync(ct);

        if (discussion is null)
            return Results.NotFound();

        return TypedResults.Ok(discussion);
    }

    private static async Task<IResult> PinDiscussionAsync(
        string id,
        HttpContext httpContext,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d =>
                d.PublicId == id
                && !d.IsDeleted, ct);

        if (discussion is null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (discussion.IsPinned)
            return Results.BadRequest(new { error = "Discussion is already pinned" });

        discussion.IsPinned = true;
        await AppendModerationLogAsync(context, actorPublicId, ModerationActionEnum.PinDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> UnpinDiscussionAsync(
        string id,
        HttpContext httpContext,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d =>
                d.PublicId == id
                && !d.IsDeleted, ct);

        if (discussion is null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (!discussion.IsPinned)
            return Results.BadRequest(new { error = "Discussion is not pinned" });

        discussion.IsPinned = false;
        await AppendModerationLogAsync(context, actorPublicId, ModerationActionEnum.UnpinDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> LockDiscussionAsync(
        string id,
        HttpContext httpContext,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d =>
                d.PublicId == id
                && !d.IsDeleted, ct);

        if (discussion is null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (discussion.IsLocked)
            return Results.BadRequest(new { error = "Discussion is already locked" });

        discussion.IsLocked = true;
        await AppendModerationLogAsync(context, actorPublicId, ModerationActionEnum.LockDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> UnlockDiscussionAsync(
        string id,
        HttpContext httpContext,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d =>
                d.PublicId == id
                && !d.IsDeleted, ct);

        if (discussion is null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (!discussion.IsLocked)
            return Results.BadRequest(new { error = "Discussion is not locked" });

        discussion.IsLocked = false;
        await AppendModerationLogAsync(context, actorPublicId, ModerationActionEnum.UnlockDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDiscussionAsync(
        string id,
        HttpContext httpContext,
        SnakkDbContext context,
        CancellationToken ct)
    {
        var actorPublicId = GetActorPublicId(httpContext);
        if (actorPublicId is null)
            return Results.Unauthorized();

        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d =>
                d.PublicId == id
                && !d.IsDeleted, ct);

        if (discussion is null)
            return Results.NotFound(new { error = "Discussion not found" });

        discussion.IsDeleted = true;
        discussion.DeletedAt = DateTime.UtcNow;
        await AppendModerationLogAsync(context, actorPublicId, ModerationActionEnum.DeleteDiscussion, discussion, ct);
        await context.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static string? GetActorPublicId(HttpContext httpContext)
    {
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return null;

        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static async Task AppendModerationLogAsync(
        SnakkDbContext context,
        string actorPublicId,
        ModerationActionEnum action,
        DiscussionDatabaseEntity discussion,
        CancellationToken ct = default)
    {
        var actorId = await context.Users
            .Where(u => u.PublicId == actorPublicId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (actorId is null)
            return;

        context.ModerationLogs.Add(new ModerationLogDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            ActorUserId = actorId.Value,
            ActionId = (int)action,
            TargetDiscussionId = discussion.Id,
            CommunityId = discussion.CommunityId,
            HubId = discussion.HubId,
            SpaceId = discussion.SpaceId,
            CreatedAt = DateTime.UtcNow
        });
    }
}

file record ContentOverviewCounts(
    int TotalCommunities, int TotalHubs, int TotalSpaces,
    int TotalDiscussions, int TotalPosts,
    int PinnedDiscussions, int LockedDiscussions);
