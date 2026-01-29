namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;

public static class HubEndpoints
{
    public static void MapHubEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/hubs")
            .WithTags("Hubs");

        group.MapPost("/", CreateHubAsync)
            .WithName("CreateHub");

        group.MapGet("/", GetHubsAsync)
            .WithName("GetHubs");

        group.MapGet("/{publicId}", GetHubAsync)
            .WithName("GetHub");

        group.MapGet("/by-slug/{slug}", GetHubBySlugAsync)
            .WithName("GetHubBySlug");

        group.MapGet("/{hubId}/spaces", GetSpacesByHubAsync)
            .WithName("GetSpacesByHub");
    }

    private static async Task<IResult> CreateHubAsync(
        CreateHubRequest request,
        HubUseCase useCase)
    {
        var result = await useCase.CreateHubAsync(
            CommunityId.From(request.CommunityId),
            request.Name,
            request.Slug,
            request.Description);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/hubs/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId.Value,
            communityId = result.Value.CommunityId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            createdAt = result.Value.CreatedAt
        });
    }

    private static async Task<IResult> GetHubsAsync(
        int offset,
        int pageSize,
        SnakkDbContext dbContext)
    {
        // Single query that gets hubs with their stats using navigation properties
        var query = dbContext.Hubs.AsNoTracking()
            .OrderBy(h => h.Name);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(offset)
            .Take(pageSize)
            .Select(h => new
            {
                publicId = h.PublicId,
                communityId = h.Community.PublicId,
                name = h.Name,
                slug = h.Slug,
                description = h.Description,
                createdAt = h.CreatedAt,
                spaceCount = h.Spaces.Count(),
                discussionCount = h.Spaces.SelectMany(s => s.Discussions).Count(d => !d.IsDeleted),
                replyCount = h.Spaces
                    .SelectMany(s => s.Discussions.Where(d => !d.IsDeleted))
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost && !p.IsDeleted)
            })
            .ToListAsync();

        return Results.Ok(new
        {
            items,
            offset,
            pageSize,
            hasMoreItems = offset + items.Count < totalCount
        });
    }

    private static async Task<IResult> GetHubAsync(
        string publicId,
        HubUseCase useCase)
    {
        var result = await useCase.GetHubAsync(HubId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            communityId = result.Value.CommunityId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetHubBySlugAsync(
        string slug,
        HubUseCase useCase)
    {
        var result = await useCase.GetHubBySlugAsync(slug);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            communityId = result.Value.CommunityId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetSpacesByHubAsync(
        string hubId,
        int offset,
        int pageSize,
        HttpContext httpContext,
        SnakkDbContext dbContext)
    {
        // Single query that gets spaces with their stats using navigation properties
        var query = dbContext.Spaces.AsNoTracking()
            .Where(s => s.Hub.PublicId == hubId)
            .OrderBy(s => s.Name);

        var totalCount = await query.CountAsync();

        var spaces = await query
            .Skip(offset)
            .Take(pageSize)
            .Select(s => new
            {
                publicId = s.PublicId,
                hubPublicId = s.Hub.PublicId,
                name = s.Name,
                slug = s.Slug,
                description = s.Description,
                createdAt = s.CreatedAt,
                discussionCount = s.Discussions.Count(d => !d.IsDeleted),
                replyCount = s.Discussions
                    .Where(d => !d.IsDeleted)
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost && !p.IsDeleted),
                latestDiscussion = s.Discussions
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.LastActivityAt ?? d.CreatedAt)
                    .Select(d => new
                    {
                        publicId = d.PublicId,
                        title = d.Title,
                        slug = d.Slug,
                        lastActivityAt = d.LastActivityAt ?? d.CreatedAt,
                        authorPublicId = d.CreatedByUser.PublicId,
                        authorDisplayName = d.CreatedByUser.DisplayName,
                        authorAvatarFileName = d.CreatedByUser.AvatarFileName,
                        postCount = d.Posts.Count(p => !p.IsDeleted)
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var items = spaces.Select(s =>
        {
            object? latestDiscussion = null;
            if (s.latestDiscussion != null)
            {
                latestDiscussion = new
                {
                    publicId = s.latestDiscussion.publicId,
                    title = s.latestDiscussion.title,
                    slug = s.latestDiscussion.slug,
                    lastActivityAt = s.latestDiscussion.lastActivityAt,
                    authorPublicId = s.latestDiscussion.authorPublicId,
                    authorDisplayName = s.latestDiscussion.authorDisplayName,
                    authorAvatarFileName = s.latestDiscussion.authorAvatarFileName,
                    postCount = s.latestDiscussion.postCount
                };
            }

            return new
            {
                publicId = s.publicId,
                hubPublicId = s.hubPublicId,
                name = s.name,
                slug = s.slug,
                description = s.description,
                createdAt = s.createdAt,
                discussionCount = s.discussionCount,
                replyCount = s.replyCount,
                latestDiscussion
            };
        }).ToList();

        return Results.Ok(new
        {
            items,
            offset,
            pageSize,
            hasMoreItems = offset + items.Count < totalCount
        });
    }
}
