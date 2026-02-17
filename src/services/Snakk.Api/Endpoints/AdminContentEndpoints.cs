namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

public static class AdminContentEndpoints
{
    public static void MapAdminContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/content")
            .WithTags("Admin - Content")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // Overview
        group.MapGet("/overview", GetContentOverviewAsync)
            .WithName("AdminGetContentOverview");

        // Communities
        group.MapGet("/communities", GetCommunitiesAsync)
            .WithName("AdminGetCommunities");

        group.MapGet("/communities/{id}", GetCommunityAsync)
            .WithName("AdminGetCommunity");

        // Hubs
        group.MapGet("/hubs", GetHubsAsync)
            .WithName("AdminGetHubs");

        // Spaces
        group.MapGet("/spaces", GetSpacesAsync)
            .WithName("AdminGetSpaces");

        // Discussions
        group.MapGet("/discussions", GetDiscussionsAsync)
            .WithName("AdminGetDiscussions");

        group.MapGet("/discussions/{id}", GetDiscussionAsync)
            .WithName("AdminGetDiscussion");

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

    private static async Task<IResult> GetContentOverviewAsync(SnakkDbContext context)
    {
        var totalCommunities = await context.Communities.CountAsync(c => !c.IsDeleted);
        var totalHubs = await context.Hubs.CountAsync(h => !h.IsDeleted);
        var totalSpaces = await context.Spaces.CountAsync(s => !s.IsDeleted);
        var totalDiscussions = await context.Discussions.CountAsync(d => !d.IsDeleted);
        var totalPosts = await context.Posts.CountAsync(p => !p.IsDeleted);

        var pinnedDiscussions = await context.Discussions.CountAsync(d => !d.IsDeleted && d.IsPinned);
        var lockedDiscussions = await context.Discussions.CountAsync(d => !d.IsDeleted && d.IsLocked);

        return Results.Ok(new
        {
            totalCommunities,
            totalHubs,
            totalSpaces,
            totalDiscussions,
            totalPosts,
            pinnedDiscussions,
            lockedDiscussions
        });
    }

    private static async Task<IResult> GetCommunitiesAsync(
        int page,
        int pageSize,
        string? search,
        SnakkDbContext context)
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
                c.Name.ToLower().Contains(searchLower) ||
                c.Slug.ToLower().Contains(searchLower));
        }

        var total = await query.CountAsync();

        var communities = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                id = c.PublicId,
                name = c.Name,
                slug = c.Slug,
                description = c.Description,
                visibility = ((CommunityVisibilityEnum)c.VisibilityId).ToString(),
                createdAt = c.CreatedAt,
                hubCount = context.Hubs.Count(h => h.CommunityId == c.Id && !h.IsDeleted),
                memberCount = 0 // TODO: Implement community membership tracking
            })
            .ToListAsync();

        return Results.Ok(new
        {
            communities,
            total,
            page,
            pageSize
        });
    }

    private static async Task<IResult> GetCommunityAsync(
        string id,
        SnakkDbContext context)
    {
        var community = await context.Communities
            .Where(c => c.PublicId == id && !c.IsDeleted)
            .Select(c => new
            {
                id = c.PublicId,
                name = c.Name,
                slug = c.Slug,
                description = c.Description,
                visibility = ((CommunityVisibilityEnum)c.VisibilityId).ToString(),
                createdAt = c.CreatedAt,
                hubCount = context.Hubs.Count(h => h.CommunityId == c.Id && !h.IsDeleted),
                memberCount = 0, // TODO: Implement community membership tracking
                hubs = context.Hubs
                    .Where(h => h.CommunityId == c.Id && !h.IsDeleted)
                    .Select(h => new
                    {
                        id = h.PublicId,
                        name = h.Name,
                        slug = h.Slug,
                        spaceCount = context.Spaces.Count(s => s.HubId == h.Id && !s.IsDeleted)
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (community == null)
            return Results.NotFound();

        return Results.Ok(community);
    }

    private static async Task<IResult> GetHubsAsync(
        int page,
        int pageSize,
        string? search,
        string? communityId,
        SnakkDbContext context)
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
                h.Name.ToLower().Contains(searchLower) ||
                h.Slug.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(communityId))
        {
            query = query.Where(h => h.Community.PublicId == communityId);
        }

        var total = await query.CountAsync();

        var hubs = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new
            {
                id = h.PublicId,
                name = h.Name,
                slug = h.Slug,
                description = h.Description,
                communityId = h.Community.PublicId,
                communityName = h.Community.Name,
                createdAt = h.CreatedAt,
                spaceCount = context.Spaces.Count(s => s.HubId == h.Id && !s.IsDeleted)
            })
            .ToListAsync();

        return Results.Ok(new
        {
            hubs,
            total,
            page,
            pageSize
        });
    }

    private static async Task<IResult> GetSpacesAsync(
        int page,
        int pageSize,
        string? search,
        string? hubId,
        SnakkDbContext context)
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
                s.Name.ToLower().Contains(searchLower) ||
                s.Slug.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(hubId))
        {
            query = query.Where(s => s.Hub.PublicId == hubId);
        }

        var total = await query.CountAsync();

        var spaces = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                id = s.PublicId,
                name = s.Name,
                slug = s.Slug,
                description = s.Description,
                hubId = s.Hub.PublicId,
                hubName = s.Hub.Name,
                communityId = s.Hub.Community.PublicId,
                communityName = s.Hub.Community.Name,
                createdAt = s.CreatedAt,
                discussionCount = context.Discussions.Count(d => d.SpaceId == s.Id && !d.IsDeleted)
            })
            .ToListAsync();

        return Results.Ok(new
        {
            spaces,
            total,
            page,
            pageSize
        });
    }

    private static async Task<IResult> GetDiscussionsAsync(
        int page,
        int pageSize,
        string? search,
        string? spaceId,
        bool? isPinned,
        bool? isLocked,
        SnakkDbContext context)
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
        {
            query = query.Where(d => d.Space.PublicId == spaceId);
        }

        if (isPinned.HasValue)
        {
            query = query.Where(d => d.IsPinned == isPinned.Value);
        }

        if (isLocked.HasValue)
        {
            query = query.Where(d => d.IsLocked == isLocked.Value);
        }

        var total = await query.CountAsync();

        var discussions = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                id = d.PublicId,
                title = d.Title,
                slug = d.Slug,
                spaceId = d.Space.PublicId,
                spaceName = d.Space.Name,
                hubId = d.Space.Hub.PublicId,
                hubName = d.Space.Hub.Name,
                communityId = d.Space.Hub.Community.PublicId,
                communityName = d.Space.Hub.Community.Name,
                authorId = d.CreatedByUser.PublicId,
                authorName = d.CreatedByUser.DisplayName,
                postCount = d.PostCount,
                isPinned = d.IsPinned,
                isLocked = d.IsLocked,
                createdAt = d.CreatedAt,
                lastActivityAt = d.LastActivityAt
            })
            .ToListAsync();

        return Results.Ok(new
        {
            discussions,
            total,
            page,
            pageSize
        });
    }

    private static async Task<IResult> GetDiscussionAsync(
        string id,
        SnakkDbContext context)
    {
        var discussion = await context.Discussions
            .Where(d => d.PublicId == id && !d.IsDeleted)
            .Select(d => new
            {
                id = d.PublicId,
                title = d.Title,
                slug = d.Slug,
                spaceId = d.Space.PublicId,
                spaceName = d.Space.Name,
                hubId = d.Space.Hub.PublicId,
                hubName = d.Space.Hub.Name,
                communityId = d.Space.Hub.Community.PublicId,
                communityName = d.Space.Hub.Community.Name,
                authorId = d.CreatedByUser.PublicId,
                authorName = d.CreatedByUser.DisplayName,
                postCount = d.PostCount,
                reactionCount = d.ReactionCount,
                isPinned = d.IsPinned,
                isLocked = d.IsLocked,
                tags = d.Tags,
                createdAt = d.CreatedAt,
                lastActivityAt = d.LastActivityAt
            })
            .FirstOrDefaultAsync();

        if (discussion == null)
            return Results.NotFound();

        return Results.Ok(discussion);
    }

    private static async Task<IResult> PinDiscussionAsync(
        string id,
        SnakkDbContext context)
    {
        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == id && !d.IsDeleted);

        if (discussion == null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (discussion.IsPinned)
            return Results.BadRequest(new { error = "Discussion is already pinned" });

        discussion.IsPinned = true;
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> UnpinDiscussionAsync(
        string id,
        SnakkDbContext context)
    {
        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == id && !d.IsDeleted);

        if (discussion == null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (!discussion.IsPinned)
            return Results.BadRequest(new { error = "Discussion is not pinned" });

        discussion.IsPinned = false;
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> LockDiscussionAsync(
        string id,
        SnakkDbContext context)
    {
        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == id && !d.IsDeleted);

        if (discussion == null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (discussion.IsLocked)
            return Results.BadRequest(new { error = "Discussion is already locked" });

        discussion.IsLocked = true;
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> UnlockDiscussionAsync(
        string id,
        SnakkDbContext context)
    {
        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == id && !d.IsDeleted);

        if (discussion == null)
            return Results.NotFound(new { error = "Discussion not found" });

        if (!discussion.IsLocked)
            return Results.BadRequest(new { error = "Discussion is not locked" });

        discussion.IsLocked = false;
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDiscussionAsync(
        string id,
        SnakkDbContext context)
    {
        var discussion = await context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == id && !d.IsDeleted);

        if (discussion == null)
            return Results.NotFound(new { error = "Discussion not found" });

        discussion.IsDeleted = true;
        discussion.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Results.NoContent();
    }
}
