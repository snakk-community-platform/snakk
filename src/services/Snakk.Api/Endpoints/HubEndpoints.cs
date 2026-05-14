namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.DTOs.Stats;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Helpers;

public static class HubEndpoints
{
    public static void MapHubEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/hubs")
            .WithTags("Hubs");

        group.MapPost("/", CreateHubAsync)
            .WithName("CreateHub")
            .RequireAuthorization()
            .Produces<HubResponse>(StatusCodes.Status201Created);

        group.MapGet("/", GetHubsAsync)
            .WithName("GetHubs")
            .Produces<PagedResponse<HubResponse>>();

        group.MapGet("/{publicId}", GetHubAsync)
            .WithName("GetHub")
            .Produces<HubResponse>();

        group.MapGet("/by-slug/{slug}", GetHubBySlugAsync)
            .WithName("GetHubBySlug")
            .Produces<HubResponse>();

        group.MapGet("/{hubId}/spaces", GetSpacesByHubAsync)
            .WithName("GetSpacesByHub")
            .Produces<PagedResponse<SpaceByHubResponse>>();

        group.MapGet("/{publicId}/stats", GetHubStatsAsync)
            .WithName("GetHubStats")
            .Produces<HubStatsResponse>();
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

        return TypedResults.Created($"/hubs/{result.Value!.PublicId}", new HubResponse(
            PublicId: result.Value.PublicId.Value,
            CommunityId: result.Value.CommunityId.Value,
            Name: result.Value.Name,
            Slug: result.Value.Slug,
            Description: result.Value.Description,
            CreatedAt: result.Value.CreatedAt,
            SpaceCount: 0,
            DiscussionCount: 0,
            ReplyCount: 0));
    }

    private static async Task<IResult> GetHubsAsync(
        int offset,
        int pageSize,
        Application.Repositories.ISearchRepository searchRepo)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        var result = await searchRepo.GetHubsAsync(offset, pageSize);

        var items = result.Items.Select(h => new HubResponse(
            PublicId: h.PublicId,
            CommunityId: h.CommunityPublicId,
            Name: h.Name,
            Slug: h.Slug,
            Description: h.Description,
            CreatedAt: h.CreatedAt,
            SpaceCount: h.SpaceCount,
            DiscussionCount: h.DiscussionCount,
            ReplyCount: h.ReplyCount));

        return TypedResults.Ok(new PagedResponse<HubResponse>(
            Items: items,
            Offset: result.Offset,
            PageSize: result.PageSize,
            HasMoreItems: result.HasMoreItems));
    }

    private static async Task<IResult> GetHubAsync(
        string publicId,
        HubUseCase useCase)
    {
        var result = await useCase.GetHubAsync(HubId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new HubResponse(
            PublicId: result.Value!.PublicId.Value,
            CommunityId: result.Value.CommunityId.Value,
            Name: result.Value.Name,
            Slug: result.Value.Slug,
            Description: result.Value.Description,
            CreatedAt: result.Value.CreatedAt,
            SpaceCount: 0,
            DiscussionCount: 0,
            ReplyCount: 0,
            LastModifiedAt: result.Value.LastModifiedAt));
    }

    private static async Task<IResult> GetHubBySlugAsync(
        string slug,
        string communitySlug,
        HubUseCase useCase)
    {
        var result = await useCase.GetHubBySlugAsync(slug, communitySlug);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new HubResponse(
            PublicId: result.Value!.PublicId.Value,
            CommunityId: result.Value.CommunityId.Value,
            Name: result.Value.Name,
            Slug: result.Value.Slug,
            Description: result.Value.Description,
            CreatedAt: result.Value.CreatedAt,
            SpaceCount: 0,
            DiscussionCount: 0,
            ReplyCount: 0,
            LastModifiedAt: result.Value.LastModifiedAt));
    }

    private static async Task<IResult> GetSpacesByHubAsync(
        string hubId,
        int offset,
        int pageSize,
        Application.Repositories.ISearchRepository searchRepo)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        var result = await searchRepo.GetSpacesByHubAsync(hubId, offset, pageSize);

        var items = result.Items.Select(s =>
        {
            LatestDiscussionRef? latestDiscussion = null;

            if (s.LatestDiscussion is not null)
            {
                latestDiscussion = new LatestDiscussionRef(
                    PublicId: s.LatestDiscussion.PublicId,
                    Title: s.LatestDiscussion.Title,
                    Slug: s.LatestDiscussion.Slug,
                    LastActivityAt: s.LatestDiscussion.LastActivityAt,
                    AuthorPublicId: s.LatestDiscussion.AuthorPublicId,
                    AuthorDisplayName: s.LatestDiscussion.AuthorDisplayName,
                    AuthorAvatarFileName: s.LatestDiscussion.AuthorAvatarFileName,
                    PostCount: s.LatestDiscussion.PostCount);
            }

            return new SpaceByHubResponse(
                PublicId: s.PublicId,
                HubPublicId: s.HubPublicId,
                Name: s.Name,
                Slug: s.Slug,
                Description: s.Description,
                CreatedAt: s.CreatedAt,
                DiscussionCount: s.DiscussionCount,
                ReplyCount: s.ReplyCount,
                LatestDiscussion: latestDiscussion);
        });

        return TypedResults.Ok(new PagedResponse<SpaceByHubResponse>(
            Items: items,
            Offset: result.Offset,
            PageSize: result.PageSize,
            HasMoreItems: result.HasMoreItems));
    }

    private static async Task<IResult> GetHubStatsAsync(
        string publicId,
        StatisticsUseCase useCase)
    {
        var result = await useCase.GetHubStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;

        return TypedResults.Ok(new HubStatsResponse
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description,
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Hub, 0, stats.AvatarFileName),
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        });
    }
}
