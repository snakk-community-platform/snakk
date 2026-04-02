namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Api.Models;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.DTOs.Stats;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Helpers;

public static class CommunityEndpoints
{
    public static void MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/communities")
            .WithTags("Communities");

        group.MapPost("/", CreateCommunityAsync)
            .WithName("CreateCommunity")
            .Produces<CommunityResponse>(StatusCodes.Status201Created);

        group.MapGet("/", GetCommunitiesAsync)
            .WithName("GetCommunities")
            .Produces<PagedResponse<CommunityResponse>>();

        group.MapGet("/{publicId}", GetCommunityAsync)
            .WithName("GetCommunity")
            .Produces<CommunityResponse>();

        group.MapGet("/by-slug/{slug}", GetCommunityBySlugAsync)
            .WithName("GetCommunityBySlug")
            .Produces<CommunityResponse>();

        group.MapGet("/by-domain/{domain}", GetCommunityByDomainAsync)
            .WithName("GetCommunityByDomain")
            .Produces<CommunityResponse>();

        group.MapGet("/{communityId}/hubs", GetHubsByCommunityAsync)
            .WithName("GetHubsByCommunity")
            .Produces<PagedResponse<HubResponse>>();

        group.MapGet("/{publicId}/stats", GetCommunityStatsAsync)
            .WithName("GetCommunityStats")
            .Produces<CommunityStatsResponse>();
    }

    private static async Task<IResult> CreateCommunityAsync(
        CreateCommunityRequest request,
        CommunityUseCase useCase)
    {
        var result = await useCase.CreateCommunityAsync(
            request.Name,
            request.Slug,
            request.Description,
            request.Visibility ?? CommunityVisibility.PublicListed,
            request.ExposeToPlatformFeed ?? true);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        var c = result.Value!;

        return TypedResults.Created($"/communities/{c.PublicId}", new CommunityResponse(
            PublicId: c.PublicId.Value,
            Name: c.Name,
            Slug: c.Slug,
            Description: c.Description,
            Visibility: c.Visibility.ToString(),
            ExposeToPlatformFeed: c.ExposeToPlatformFeed,
            CreatedAt: c.CreatedAt));
    }

    private static async Task<IResult> GetCommunitiesAsync(
        int offset,
        int pageSize,
        CommunityUseCase useCase)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        var result = await useCase.GetPublicCommunitiesAsync(offset, pageSize);

        return TypedResults.Ok(new PagedResponse<CommunityResponse>(
            Items: result.Items.Select(c => new CommunityResponse(
                PublicId: c.PublicId.Value,
                Name: c.Name,
                Slug: c.Slug,
                Description: c.Description,
                Visibility: c.Visibility.ToString(),
                ExposeToPlatformFeed: c.ExposeToPlatformFeed,
                CreatedAt: c.CreatedAt)),
            Offset: result.Offset,
            PageSize: result.PageSize,
            HasMoreItems: result.HasMoreItems));
    }

    private static async Task<IResult> GetCommunityAsync(
        string publicId,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetCommunityAsync(CommunityId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        var c = result.Value!;

        return TypedResults.Ok(new CommunityResponse(
            PublicId: c.PublicId.Value,
            Name: c.Name,
            Slug: c.Slug,
            Description: c.Description,
            Visibility: c.Visibility.ToString(),
            ExposeToPlatformFeed: c.ExposeToPlatformFeed,
            CreatedAt: c.CreatedAt,
            LastModifiedAt: c.LastModifiedAt));
    }

    private static async Task<IResult> GetCommunityBySlugAsync(
        string slug,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetCommunityBySlugAsync(slug);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        var c = result.Value!;

        return TypedResults.Ok(new CommunityResponse(
            PublicId: c.PublicId.Value,
            Name: c.Name,
            Slug: c.Slug,
            Description: c.Description,
            Visibility: c.Visibility.ToString(),
            ExposeToPlatformFeed: c.ExposeToPlatformFeed,
            CreatedAt: c.CreatedAt,
            LastModifiedAt: c.LastModifiedAt));
    }

    private static async Task<IResult> GetCommunityByDomainAsync(
        string domain,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetCommunityByDomainAsync(domain);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        var c = result.Value!;

        return TypedResults.Ok(new CommunityResponse(
            PublicId: c.PublicId.Value,
            Name: c.Name,
            Slug: c.Slug,
            Description: c.Description,
            Visibility: c.Visibility.ToString(),
            ExposeToPlatformFeed: c.ExposeToPlatformFeed,
            CreatedAt: c.CreatedAt,
            LastModifiedAt: c.LastModifiedAt));
    }

    private static async Task<IResult> GetHubsByCommunityAsync(
        string communityId,
        int offset,
        int pageSize,
        HubUseCase useCase)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        var result = await useCase.GetHubsByCommunityAsync(CommunityId.From(communityId), offset, pageSize);

        return TypedResults.Ok(new PagedResponse<HubResponse>(
            Items: result.Items.Select(h => new HubResponse(
                PublicId: h.PublicId.Value,
                CommunityId: h.CommunityId.Value,
                Name: h.Name,
                Slug: h.Slug,
                Description: h.Description,
                CreatedAt: h.CreatedAt,
                SpaceCount: 0,
                DiscussionCount: 0,
                ReplyCount: 0)),
            Offset: result.Offset,
            PageSize: result.PageSize,
            HasMoreItems: result.HasMoreItems));
    }

    private static async Task<IResult> GetCommunityStatsAsync(
        string publicId,
        StatisticsUseCase useCase)
    {
        var result = await useCase.GetCommunityStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;

        return TypedResults.Ok(new CommunityStatsResponse
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description,
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Community, 0, stats.AvatarFileName),
            HubCount = stats.HubCount,
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        });
    }
}
