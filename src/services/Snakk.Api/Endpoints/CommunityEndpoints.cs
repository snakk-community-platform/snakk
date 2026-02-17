namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;

public static class CommunityEndpoints
{
    public static void MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/communities")
            .WithTags("Communities");

        group.MapPost("/", CreateCommunityAsync)
            .WithName("CreateCommunity");

        group.MapGet("/", GetCommunitiesAsync)
            .WithName("GetCommunities");

        group.MapGet("/{publicId}", GetCommunityAsync)
            .WithName("GetCommunity");

        group.MapGet("/by-slug/{slug}", GetCommunityBySlugAsync)
            .WithName("GetCommunityBySlug");

        group.MapGet("/by-domain/{domain}", GetCommunityByDomainAsync)
            .WithName("GetCommunityByDomain");

        group.MapGet("/{communityId}/hubs", GetHubsByCommunityAsync)
            .WithName("GetHubsByCommunity");
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

        return Results.Created($"/communities/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            visibility = result.Value.Visibility.ToString(),
            exposeToPlatformFeed = result.Value.ExposeToPlatformFeed,
            createdAt = result.Value.CreatedAt
        });
    }

    private static async Task<IResult> GetCommunitiesAsync(
        int offset,
        int pageSize,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetPublicCommunitiesAsync(offset, pageSize);

        return Results.Ok(new
        {
            items = result.Items.Select(c => new
            {
                publicId = c.PublicId.Value,
                name = c.Name,
                slug = c.Slug,
                description = c.Description,
                visibility = c.Visibility.ToString(),
                exposeToPlatformFeed = c.ExposeToPlatformFeed,
                createdAt = c.CreatedAt
            }),
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }

    private static async Task<IResult> GetCommunityAsync(
        string publicId,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetCommunityAsync(CommunityId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            visibility = result.Value.Visibility.ToString(),
            exposeToPlatformFeed = result.Value.ExposeToPlatformFeed,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetCommunityBySlugAsync(
        string slug,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetCommunityBySlugAsync(slug);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            visibility = result.Value.Visibility.ToString(),
            exposeToPlatformFeed = result.Value.ExposeToPlatformFeed,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetCommunityByDomainAsync(
        string domain,
        CommunityUseCase useCase)
    {
        var result = await useCase.GetCommunityByDomainAsync(domain);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return Results.Ok(new
        {
            publicId = result.Value!.PublicId.Value,
            name = result.Value.Name,
            slug = result.Value.Slug,
            description = result.Value.Description,
            visibility = result.Value.Visibility.ToString(),
            exposeToPlatformFeed = result.Value.ExposeToPlatformFeed,
            createdAt = result.Value.CreatedAt,
            lastModifiedAt = result.Value.LastModifiedAt
        });
    }

    private static async Task<IResult> GetHubsByCommunityAsync(
        string communityId,
        int offset,
        int pageSize,
        HubUseCase useCase)
    {
        var result = await useCase.GetHubsByCommunityAsync(CommunityId.From(communityId), offset, pageSize);

        return Results.Ok(new
        {
            items = result.Items.Select(h => new
            {
                publicId = h.PublicId.Value,
                communityId = h.CommunityId.Value,
                name = h.Name,
                slug = h.Slug,
                description = h.Description,
                createdAt = h.CreatedAt
            }),
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems
        });
    }
}
