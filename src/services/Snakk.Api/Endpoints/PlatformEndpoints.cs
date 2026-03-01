namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.UseCases;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/platform")
            .WithTags("Platform");

        group.MapGet("/stats", GetPlatformStatsAsync)
            .WithName("GetPlatformStats")
            .Produces<PlatformStatsResponse>();
    }

    private static async Task<IResult> GetPlatformStatsAsync(StatisticsUseCase useCase)
    {
        var stats = await useCase.GetPlatformStatsAsync();

        return TypedResults.Ok(new PlatformStatsResponse(
            HubCount: stats.HubCount,
            SpaceCount: stats.SpaceCount,
            DiscussionCount: stats.DiscussionCount,
            ReplyCount: stats.ReplyCount));
    }
}
