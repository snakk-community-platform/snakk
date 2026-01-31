namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;

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
        Application.Repositories.ISearchRepository searchRepo)
    {
        var result = await searchRepo.GetDiscussionsBySpaceAsync(spaceId, offset, pageSize);

        var items = result.Items.Select(d => new
        {
            publicId = d.PublicId,
            spaceId = d.SpacePublicId,
            title = d.Title,
            slug = d.Slug,
            createdAt = d.CreatedAt,
            lastActivityAt = d.LastActivityAt,
            isPinned = d.IsPinned,
            isLocked = d.IsLocked,
            postCount = d.PostCount,
            reactionCount = d.ReactionCount,
            author = new
            {
                publicId = d.AuthorPublicId,
                displayName = d.AuthorDisplayName,
                avatarFileName = d.AuthorAvatarFileName
            },
            tags = d.Tags
        });

        return Results.Ok(new
        {
            items,
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }

    private static async Task<IResult> GetTopActiveSpacesTodayAsync(
        StatisticsUseCase useCase,
        string? hubId = null,
        string? communityId = null)
    {
        var topSpaces = await useCase.GetTopActiveSpacesTodayAsync(hubId, communityId);

        var items = topSpaces.Select(s => new
        {
            publicId = s.PublicId,
            name = s.Name,
            slug = s.Slug,
            postCountToday = s.PostCountToday,
            hub = new
            {
                publicId = s.HubPublicId,
                slug = s.HubSlug,
                name = s.HubName
            }
        });

        return Results.Ok(new { items });
    }
}
