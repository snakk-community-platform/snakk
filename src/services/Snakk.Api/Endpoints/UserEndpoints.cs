namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.DTOs.Stats;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Shared.Helpers;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users");

        group.MapGet("/search", SearchUsersAsync)
            .WithName("SearchUsers")
            .Produces<List<UserSearchResult>>();

        group.MapGet("/top-contributors-today", GetTopContributorsTodayAsync)
            .WithName("GetTopContributorsToday")
            .Produces<TopContributorsResponse>();

        group.MapGet("/{publicId}/profile", GetUserProfileAsync)
            .WithName("GetUserProfile")
            .Produces<UserProfileDto>();

        group.MapGet("/{publicId}/activity-history", GetUserActivityHistoryAsync)
            .WithName("GetUserActivityHistory")
            .Produces<ActivityHistoryResponse>();

        group.MapGet("/{publicId}/stats", GetUserStatsAsync)
            .WithName("GetUserStats")
            .Produces<UserStatsResponse>();
    }

    private static async Task<IResult> GetUserProfileAsync(
        string publicId,
        UserProfileUseCase userProfileUseCase)
    {
        var profile = await userProfileUseCase.GetUserProfileAsync(publicId);
        if (profile == null)
            return Results.NotFound();

        return Results.Ok(profile);
    }

    private static async Task<IResult> SearchUsersAsync(
        string query,
        int? limit,
        IUserRepository userRepository)
    {
        var users = await userRepository.SearchByDisplayNameAsync(query, limit ?? 5);

        var items = users.Select(u => new UserSearchResult(
            PublicId: u.PublicId.Value,
            DisplayName: u.DisplayName,
            AvatarUrl: AvatarHelper.GetAvatarUrl(u.PublicId.Value, AvatarEntityType.User, u.AvatarRevision)
        )).ToList();

        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetTopContributorsTodayAsync(
        StatisticsUseCase useCase,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        var result = await useCase.GetTopContributorsTodayAsync(
            hubId,
            spaceId,
            communityId,
            limit: 5);

        if (!result.IsSuccess)
            return Results.Problem(result.Error);

        var items = result.Value!.Items.Select(c => new TopContributorResponse(
            PublicId: c.UserId,
            DisplayName: c.DisplayName,
            AvatarUrl: AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, 0),
            PostCountToday: c.PostCountToday
        ));

        return TypedResults.Ok(new TopContributorsResponse(Items: items));
    }

    private static async Task<IResult> GetUserActivityHistoryAsync(
        string publicId,
        int days,
        StatisticsUseCase statisticsUseCase)
    {
        var result = await statisticsUseCase.GetUserActivityHistoryAsync(publicId, days);

        if (!result.IsSuccess)
            return Results.NotFound();

        var data = result.Value!.Data.Select(d => new ActivityDayResponse(
            Date: d.Date.ToString("yyyy-MM-dd"),
            Discussions: d.Discussions,
            Posts: d.Posts,
            Total: d.Total
        ));

        return TypedResults.Ok(new ActivityHistoryResponse(
            Days: result.Value.Days,
            Data: data));
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
}
