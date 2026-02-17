namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Stats;
using Snakk.Application.UseCases;
using Snakk.Shared.Helpers;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Stats");

        group.MapGet("/platform/stats", GetPlatformStatsAsync)
            .WithName("GetPlatformStats");

        group.MapGet("/hubs/{publicId}/stats", GetHubStatsAsync)
            .WithName("GetHubStats")
            .Produces<HubStatsResponse>();

        group.MapGet("/spaces/{publicId}/stats", GetSpaceStatsAsync)
            .WithName("GetSpaceStats")
            .Produces<SpaceStatsResponse>();

        group.MapGet("/communities/{publicId}/stats", GetCommunityStatsAsync)
            .WithName("GetCommunityStats")
            .Produces<CommunityStatsResponse>();

        group.MapGet("/users/{publicId}/stats", GetUserStatsAsync)
            .WithName("GetUserStats")
            .Produces<UserStatsResponse>();

        group.MapGet("/discussions/{publicId}/stats", GetDiscussionStatsAsync)
            .WithName("GetDiscussionStats");
    }

    private static async Task<IResult> GetPlatformStatsAsync(StatisticsUseCase useCase)
    {
        var stats = await useCase.GetPlatformStatsAsync();

        return Results.Ok(new
        {
            hubCount = stats.HubCount,
            spaceCount = stats.SpaceCount,
            discussionCount = stats.DiscussionCount,
            replyCount = stats.ReplyCount
        });
    }

    private static async Task<IResult> GetHubStatsAsync(string publicId, StatisticsUseCase useCase)
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
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Hub, 0),
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        });
    }

    private static async Task<IResult> GetSpaceStatsAsync(string publicId, StatisticsUseCase useCase)
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

    private static async Task<IResult> GetCommunityStatsAsync(string publicId, StatisticsUseCase useCase)
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
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.Community, 0),
            HubCount = stats.HubCount,
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        });
    }

    private static async Task<IResult> GetUserStatsAsync(string publicId, StatisticsUseCase useCase)
    {
        var result = await useCase.GetUserStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;

        return TypedResults.Ok(new UserStatsResponse
        {
            PublicId = stats.PublicId,
            DisplayName = stats.DisplayName,
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.User, 0),
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount,
            FollowingCount = 0 // TODO: Add to domain stats
        });
    }

    private static async Task<IResult> GetDiscussionStatsAsync(string publicId, StatisticsUseCase useCase)
    {
        var result = await useCase.GetDiscussionStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;
        return Results.Ok(new
        {
            publicId = stats.PublicId,
            title = stats.Title,
            replyCount = stats.ReplyCount,
            followerCount = stats.FollowerCount
        });
    }
}
