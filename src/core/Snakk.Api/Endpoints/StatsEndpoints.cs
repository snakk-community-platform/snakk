namespace Snakk.Api.Endpoints;

using Snakk.Application.UseCases;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Stats");

        group.MapGet("/platform/stats", GetPlatformStatsAsync)
            .WithName("GetPlatformStats");

        group.MapGet("/hubs/{publicId}/stats", GetHubStatsAsync)
            .WithName("GetHubStats");

        group.MapGet("/spaces/{publicId}/stats", GetSpaceStatsAsync)
            .WithName("GetSpaceStats");

        group.MapGet("/communities/{publicId}/stats", GetCommunityStatsAsync)
            .WithName("GetCommunityStats");

        group.MapGet("/users/{publicId}/stats", GetUserStatsAsync)
            .WithName("GetUserStats");

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
        return Results.Ok(new
        {
            publicId = stats.PublicId,
            name = stats.Name,
            description = stats.Description,
            spaceCount = stats.SpaceCount,
            discussionCount = stats.DiscussionCount,
            replyCount = stats.ReplyCount
        });
    }

    private static async Task<IResult> GetSpaceStatsAsync(string publicId, StatisticsUseCase useCase)
    {
        var result = await useCase.GetSpaceStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;
        return Results.Ok(new
        {
            publicId = stats.PublicId,
            name = stats.Name,
            description = stats.Description,
            discussionCount = stats.DiscussionCount,
            replyCount = stats.ReplyCount,
            followerCount = stats.FollowerCount
        });
    }

    private static async Task<IResult> GetCommunityStatsAsync(string publicId, StatisticsUseCase useCase)
    {
        var result = await useCase.GetCommunityStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;
        return Results.Ok(new
        {
            publicId = stats.PublicId,
            name = stats.Name,
            description = stats.Description,
            hubCount = stats.HubCount,
            spaceCount = stats.SpaceCount,
            discussionCount = stats.DiscussionCount,
            replyCount = stats.ReplyCount
        });
    }

    private static async Task<IResult> GetUserStatsAsync(string publicId, StatisticsUseCase useCase)
    {
        var result = await useCase.GetUserStatsAsync(publicId);

        if (!result.IsSuccess)
            return Results.NotFound();

        var stats = result.Value!;
        return Results.Ok(new
        {
            publicId = stats.PublicId,
            displayName = stats.DisplayName,
            discussionCount = stats.DiscussionCount,
            replyCount = stats.ReplyCount,
            followerCount = stats.FollowerCount
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
