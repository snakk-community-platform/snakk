namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.DTOs.Management;
using Snakk.Application.DTOs.Responses;
using Snakk.Application.DTOs.Stats;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Helpers;

public static class SpaceEndpoints
{
    public static void MapSpaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/spaces")
            .WithTags("Spaces");

        group.MapPost("/", CreateSpaceAsync)
            .WithName("CreateSpace")
            .Produces<SpaceResponse>(StatusCodes.Status201Created);

        group.MapGet("/{publicId}", GetSpaceAsync)
            .WithName("GetSpace")
            .Produces<SpaceResponse>();

        group.MapGet("/by-slug/{slug}", GetSpaceBySlugAsync)
            .WithName("GetSpaceBySlug")
            .Produces<SpaceResponse>();

        group.MapGet("/{spaceId}/discussions", GetDiscussionsBySpaceAsync)
            .WithName("GetDiscussionsBySpace")
            .Produces<PagedResponse<DiscussionBySpaceResponse>>();

        group.MapGet("/top-active-today", GetTopActiveSpacesTodayAsync)
            .WithName("GetTopActiveSpacesToday")
            .Produces<TopActiveSpacesResponse>();

        group.MapGet("/{spaceId}/rules", GetSpaceRulesAsync)
            .WithName("GetSpaceRules")
            .Produces<RulesDto>();

        group.MapGet("/{publicId}/stats", GetSpaceStatsAsync)
            .WithName("GetSpaceStats")
            .Produces<SpaceStatsResponse>();
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

        return TypedResults.Created($"/spaces/{result.Value!.PublicId}", new SpaceResponse(
            PublicId: result.Value.PublicId.Value,
            HubId: result.Value.HubId.Value,
            Name: result.Value.Name,
            Slug: result.Value.Slug,
            Description: result.Value.Description,
            CreatedAt: result.Value.CreatedAt,
            DiscussionCount: 0,
            ReplyCount: 0));
    }

    private static async Task<IResult> GetSpaceAsync(
        string publicId,
        SpaceUseCase useCase)
    {
        var result = await useCase.GetSpaceAsync(SpaceId.From(publicId));

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new SpaceResponse(
            PublicId: result.Value!.PublicId.Value,
            HubId: result.Value.HubId.Value,
            Name: result.Value.Name,
            Slug: result.Value.Slug,
            Description: result.Value.Description,
            CreatedAt: result.Value.CreatedAt,
            DiscussionCount: 0,
            ReplyCount: 0,
            LastModifiedAt: result.Value.LastModifiedAt));
    }

    private static async Task<IResult> GetSpaceBySlugAsync(
        string slug,
        string hubSlug,
        SpaceUseCase useCase)
    {
        var result = await useCase.GetSpaceBySlugAsync(slug, hubSlug);

        if (!result.IsSuccess)
            return Results.NotFound(new { error = result.Error });

        return TypedResults.Ok(new SpaceResponse(
            PublicId: result.Value!.PublicId.Value,
            HubId: result.Value.HubId.Value,
            Name: result.Value.Name,
            Slug: result.Value.Slug,
            Description: result.Value.Description,
            CreatedAt: result.Value.CreatedAt,
            DiscussionCount: 0,
            ReplyCount: 0,
            LastModifiedAt: result.Value.LastModifiedAt));
    }

    private static async Task<IResult> GetDiscussionsBySpaceAsync(
        string spaceId,
        int offset,
        int pageSize,
        int? typeFilter,
        Application.Repositories.ISearchRepository searchRepo)
    {
        // Clamp pagination parameters
        pageSize = Math.Clamp(pageSize > 0 ? pageSize : 20, 1, 100);
        offset = Math.Max(0, offset);

        var result = await searchRepo.GetDiscussionsBySpaceAsync(spaceId, offset, pageSize, typeFilter);

        var items = result.Items.Select(d => new DiscussionBySpaceResponse(
            PublicId: d.PublicId,
            SpaceId: d.SpacePublicId,
            Title: d.Title,
            Slug: d.Slug,
            Type: ((Snakk.Shared.Enums.DiscussionTypeEnum)d.Type).ToString(),
            CreatedAt: d.CreatedAt,
            LastActivityAt: d.LastActivityAt ?? d.CreatedAt,
            IsPinned: d.IsPinned,
            IsLocked: d.IsLocked,
            PostCount: d.PostCount,
            ReactionCount: d.ReactionCount,
            Author: new AuthorRef(
                PublicId: d.AuthorPublicId,
                DisplayName: d.AuthorDisplayName,
                AvatarUrl: d.AuthorAvatarFileName),
            Tags: d.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

        return TypedResults.Ok(new PagedResponse<DiscussionBySpaceResponse>(
            Items: items,
            Offset: result.Offset,
            PageSize: result.PageSize,
            HasMoreItems: result.HasMoreItems));
    }

    private static async Task<IResult> GetTopActiveSpacesTodayAsync(
        StatisticsUseCase useCase,
        IConfiguration configuration,
        string? hubId = null,
        string? communityId = null)
    {
        var since = DateTime.UtcNow.AddHours(-configuration.GetValue("Trending:LookbackHours", 24));
        var topSpaces = await useCase.GetTopActiveSpacesTodayAsync(since, hubId, communityId);

        var items = topSpaces.Select(s => new TopActiveSpaceResponse(
            PublicId: s.PublicId,
            Name: s.Name,
            Slug: s.Slug,
            PostCountToday: s.PostCountToday,
            Hub: new EntityRef(
                PublicId: s.HubPublicId,
                Slug: s.HubSlug,
                Name: s.HubName)));

        return TypedResults.Ok(new TopActiveSpacesResponse(items));
    }

    private static async Task<IResult> GetSpaceRulesAsync(
        string spaceId,
        IRuleService ruleService,
        CancellationToken cancellationToken)
    {
        var rules = await ruleService.GetRulesAsync("Space", spaceId, cancellationToken);
        return Results.Ok(rules);
    }

    private static async Task<IResult> GetSpaceStatsAsync(
        string publicId,
        StatisticsUseCase useCase)
    {
        var result = await useCase.GetSpaceStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;

        return TypedResults.Ok(new SpaceStatsResponse
        {
            PublicId = stats.PublicId,
            Name = stats.Name,
            Description = stats.Description,
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Space, 0),
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount
        });
    }
}
