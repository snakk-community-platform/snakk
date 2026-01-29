namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;

public static class SpaceEndpoints
{
    public static void MapSpaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/spaces")
            .WithTags("Spaces");

        group.MapPost("/", CreateSpaceAsync)
            .WithName("CreateSpace");

        group.MapGet("/{publicId}", GetSpaceAsync)
            .WithName("GetSpace");

        group.MapGet("/by-slug/{slug}", GetSpaceBySlugAsync)
            .WithName("GetSpaceBySlug");

        group.MapGet("/{spaceId}/discussions", GetDiscussionsBySpaceAsync)
            .WithName("GetDiscussionsBySpace");

        group.MapGet("/top-active-today", GetTopActiveSpacesTodayAsync)
            .WithName("GetTopActiveSpacesToday");
    }

    private static async Task<IResult> CreateSpaceAsync(
        CreateSpaceRequest request,
        SpaceUseCase useCase)
    {
        var result = await useCase.CreateSpaceAsync(
            HubId.From(request.HubId),
            request.Name,
            request.Slug,
            request.Description);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/spaces/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId.Value,
            hubId = result.Value.HubId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            createdAt = result.Value.CreatedAt
        });
    }

    private static async Task<IResult> GetSpaceAsync(
        string publicId,
        SpaceUseCase useCase)
    {
        var result = await useCase.GetSpaceAsync(SpaceId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            hubId = result.Value.HubId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetSpaceBySlugAsync(
        string slug,
        SpaceUseCase useCase)
    {
        var result = await useCase.GetSpaceBySlugAsync(slug);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            hubId = result.Value.HubId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetDiscussionsBySpaceAsync(
        string spaceId,
        int offset,
        int pageSize,
        SnakkDbContext dbContext)
    {
        // Single query using navigation properties
        var query = dbContext.Discussions.AsNoTracking()
            .Where(d => d.Space.PublicId == spaceId)
            .OrderByDescending(d => d.IsPinned)
            .ThenByDescending(d => d.LastActivityAt);

        var items = await query
            .Skip(offset)
            .Take(pageSize + 1)
            .Select(d => new
            {
                publicId = d.PublicId,
                spaceId = d.Space.PublicId,
                title = d.Title,
                slug = d.Slug,
                createdAt = d.CreatedAt,
                lastActivityAt = d.LastActivityAt,
                isPinned = d.IsPinned,
                isLocked = d.IsLocked,
                postCount = d.Posts.Count(p => !p.IsDeleted),
                reactionCount = d.ReactionCount,
                author = new
                {
                    publicId = d.CreatedByUser.PublicId,
                    displayName = d.CreatedByUser.DisplayName,
                    avatarFileName = d.CreatedByUser.AvatarFileName
                },
                tags = d.Tags
            })
            .ToListAsync();

        var hasMoreItems = items.Count > pageSize;
        var resultItems = hasMoreItems ? items.Take(pageSize) : items;

        return Results.Ok(new
        {
            items = resultItems,
            offset,
            pageSize,
            hasMoreItems
        });
    }

    private static async Task<IResult> GetTopActiveSpacesTodayAsync(
        SnakkDbContext dbContext,
        string? hubId = null,
        string? communityId = null)
    {
        var today = DateTime.UtcNow.Date;

        var postsQuery = dbContext.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.CreatedAt >= today);

        // Filter by community if specified
        if (!string.IsNullOrEmpty(communityId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.Community.PublicId == communityId);
        }

        // Filter by hub if specified
        if (!string.IsNullOrEmpty(hubId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.PublicId == hubId);
        }

        var topSpaces = await postsQuery
            .GroupBy(p => p.Discussion.SpaceId)
            .Select(g => new { SpaceId = g.Key, PostCountToday = g.Count() })
            .OrderByDescending(x => x.PostCountToday)
            .Take(5)
            .Join(
                dbContext.Spaces.AsNoTracking().Where(s => !s.IsDeleted),
                x => x.SpaceId,
                s => s.Id,
                (x, s) => new { Space = s, x.PostCountToday })
            .Select(x => new
            {
                publicId = x.Space.PublicId,
                name = x.Space.Name,
                slug = x.Space.Slug,
                postCountToday = x.PostCountToday,
                hub = new
                {
                    publicId = x.Space.Hub.PublicId,
                    slug = x.Space.Hub.Slug,
                    name = x.Space.Hub.Name
                }
            })
            .ToListAsync();

        return Results.Ok(new { items = topSpaces });
    }
}
