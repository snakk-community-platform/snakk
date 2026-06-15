namespace Snakk.Api.Endpoints;

using Snakk.Application.DTOs.Responses;
using Snakk.Application.DTOs.Stats;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
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

        group.MapGet("/{publicId}/mod-roles", GetUserModRolesAsync)
            .WithName("GetUserModRoles")
            .Produces<UserModRolesResponse>();
    }

    private static async Task<IResult> GetUserProfileAsync(
        string publicId,
        UserProfileUseCase userProfileUseCase)
    {
        var profile = await userProfileUseCase.GetUserProfileAsync(publicId);

        if (profile is null)
            return Results.NotFound();

        return Results.Ok(profile);
    }

    private static async Task<IResult> SearchUsersAsync(
        string query,
        int? limit,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        var users = await userRepository.SearchByDisplayNameAsync(query, limit ?? 5, ct);

        var items = users.Select(u => new UserSearchResult(
            PublicId: u.PublicId.Value,
            DisplayName: u.DisplayName ?? "",
            AvatarUrl: AvatarHelper.GetAvatarUrl(u.PublicId.Value, AvatarEntityType.User, u.AvatarRevision)
        )).ToList();

        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetTopContributorsTodayAsync(
        StatisticsUseCase useCase,
        IConfiguration configuration,
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        var since = DateTime.UtcNow.AddHours(-configuration.GetValue("Trending:LookbackHours", 24));
        var result = await useCase.GetTopContributorsTodayAsync(
            since,
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

    private static async Task<IResult> GetUserModRolesAsync(
        string publicId,
        IModerationRepository moderationRepo,
        ISpaceDataService spaceData,
        IHubDataService hubData,
        ICommunityDataService communityData,
        CancellationToken ct)
    {
        var roles = await moderationRepo.GetCachedModRolesForUserAsync(publicId, ct);

        var items = new List<UserModRoleResponse>();
        foreach (var role in roles)
        {
            string? accessLevel = null;
            if (role.EntityType == "Space" && role.EntityId is not null)
            {
                var meta = await spaceData.GetSpaceMetaAsync(role.EntityId, ct);
                if (meta is not null)
                    accessLevel = meta.IsRestricted ? "members"
                        : !meta.AllowAnonymousReading ? "registered"
                        : "public";
            }
            else if (role.EntityType == "Hub" && role.EntityId is not null)
            {
                var meta = await hubData.GetHubMetaAsync(role.EntityId, ct);
                if (meta is not null)
                    accessLevel = meta.IsRestricted ? "members"
                        : !meta.AllowAnonymousReading ? "registered"
                        : "public";
            }
            else if (role.EntityType == "Community" && role.EntityId is not null)
            {
                var meta = await communityData.GetCommunityMetaAsync(role.EntityId, ct);
                if (meta is not null)
                    accessLevel = meta.IsRestricted ? "members"
                        : meta.VisibilityId == 2 ? "unlisted"
                        : "public";
            }

            items.Add(new UserModRoleResponse(role.Role, role.EntityType, role.EntityId, role.EntityName, accessLevel));
        }

        return TypedResults.Ok(new UserModRolesResponse(items));
    }
}
