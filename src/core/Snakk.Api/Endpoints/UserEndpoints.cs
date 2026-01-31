namespace Snakk.Api.Endpoints;

using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapGet("/search", SearchUsersAsync)
            .WithName("SearchUsers");

        group.MapGet("/top-contributors-today", GetTopContributorsTodayAsync)
            .WithName("GetTopContributorsToday");

        group.MapGet("/{publicId}/profile", GetUserProfileAsync)
            .WithName("GetUserProfile");

        group.MapGet("/{publicId}/activity-history", GetUserActivityHistoryAsync)
            .WithName("GetUserActivityHistory");
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

        return Results.Ok(users.Select(u => new
        {
            publicId = u.PublicId.Value,
            displayName = u.DisplayName,
            avatarUrl = $"/avatars/{u.PublicId.Value}"
        }));
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

        return Results.Ok(new
        {
            items = result.Value!.Items.Select(c => new
            {
                userId = c.UserId,
                displayName = c.DisplayName,
                avatarFileName = c.AvatarFileName,
                postCountToday = c.PostCountToday
            })
        });
    }

    private static async Task<IResult> GetUserActivityHistoryAsync(
        string publicId,
        int days,
        StatisticsUseCase statisticsUseCase)
    {
        var result = await statisticsUseCase.GetUserActivityHistoryAsync(publicId, days);

        if (!result.IsSuccess)
            return Results.NotFound();

        var response = new
        {
            days = result.Value!.Days,
            data = result.Value.Data.Select(d => new
            {
                date = d.Date.ToString("yyyy-MM-dd"),
                discussions = d.Discussions,
                posts = d.Posts,
                total = d.Total
            })
        };

        return Results.Ok(response);
    }
}
