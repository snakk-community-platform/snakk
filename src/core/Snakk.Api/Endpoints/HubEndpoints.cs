namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;

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
        Application.Repositories.ISearchRepository searchRepo)
    {
        var result = await searchRepo.GetHubsAsync(offset, pageSize);

        var items = result.Items.Select(h => new
        {
            publicId = h.PublicId,
            communityId = h.CommunityPublicId,
            name = h.Name,
            slug = h.Slug,
            description = h.Description,
            createdAt = h.CreatedAt,
            spaceCount = h.SpaceCount,
            discussionCount = h.DiscussionCount,
            replyCount = h.ReplyCount
        });

        return Results.Ok(new
        {
            items,
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
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
        Application.Repositories.ISearchRepository searchRepo)
    {
        var result = await searchRepo.GetSpacesByHubAsync(hubId, offset, pageSize);

        var items = result.Items.Select(s =>
        {
            object? latestDiscussion = null;
            if (s.LatestDiscussion != null)
            {
                latestDiscussion = new
                {
                    publicId = s.LatestDiscussion.PublicId,
                    title = s.LatestDiscussion.Title,
                    slug = s.LatestDiscussion.Slug,
                    lastActivityAt = s.LatestDiscussion.LastActivityAt,
                    authorPublicId = s.LatestDiscussion.AuthorPublicId,
                    authorDisplayName = s.LatestDiscussion.AuthorDisplayName,
                    authorAvatarFileName = s.LatestDiscussion.AuthorAvatarFileName,
                    postCount = s.LatestDiscussion.PostCount
                };
            }

            return new
            {
                publicId = s.PublicId,
                hubPublicId = s.HubPublicId,
                name = s.Name,
                slug = s.Slug,
                description = s.Description,
                createdAt = s.CreatedAt,
                discussionCount = s.DiscussionCount,
                replyCount = s.ReplyCount,
                latestDiscussion
            };
        });

        return Results.Ok(new
        {
            items,
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }
}
