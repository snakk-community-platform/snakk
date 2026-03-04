using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Application.UseCases;
using Snakk.Protos;
using Snakk.Protos.Statistics;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class StatisticsGrpcService(
    StatisticsUseCase statisticsUseCase,
    IConfiguration configuration) : StatisticsService.StatisticsServiceBase
{
    private DateTime GetTrendingSince() =>
        DateTime.UtcNow.AddHours(-configuration.GetValue("Trending:LookbackHours", 24));

    public override async Task<PlatformStats> GetPlatformStats(GetPlatformStatsRequest request, ServerCallContext context)
    {
        var stats = await statisticsUseCase.GetPlatformStatsAsync();

        return new PlatformStats
        {
            HubCount = stats.HubCount,
            SpaceCount = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount
        };
    }

    public override async Task<TopActiveDiscussionsList> GetTopActiveDiscussionsToday(GetTopActiveDiscussionsTodayRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetTopActiveDiscussionsTodayAsync(
            GetTrendingSince(),
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);

        if (!result.IsSuccess || result.Value is null)
            return new TopActiveDiscussionsList();

        var response = new TopActiveDiscussionsList();

        foreach (var d in result.Value.Items)
        {
            response.Items.Add(new TopActiveDiscussionInfo
            {
                PublicId = d.DiscussionId,
                Title = d.Title,
                Slug = d.Slug,
                PostCountToday = d.PostCountToday,

                Space = new EntityRef
                {
                    PublicId = d.SpacePublicId,
                    Slug = d.SpaceSlug,
                    Name = d.SpaceName
                },
                Hub = new EntityRef
                {
                    PublicId = d.HubPublicId,
                    Slug = d.HubSlug,
                    Name = d.HubName
                },
                Author = new AuthorRef
                {
                    PublicId = d.AuthorPublicId,
                    DisplayName = d.AuthorDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.AuthorPublicId, AvatarEntityType.User, 0)
                }
            });
        }

        return response;
    }

    public override async Task<TopActiveSpacesList> GetTopActiveSpacesToday(GetTopActiveSpacesTodayRequest request, ServerCallContext context)
    {
        var spaces = await statisticsUseCase.GetTopActiveSpacesTodayAsync(
            GetTrendingSince(),
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);

        var response = new TopActiveSpacesList();

        foreach (var s in spaces)
        {
            response.Items.Add(new TopActiveSpaceInfo
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Slug = s.Slug,
                PostCountToday = s.PostCountToday,
                Hub = new EntityRef
                {
                    PublicId = s.HubPublicId,
                    Slug = s.HubSlug,
                    Name = s.HubName
                }
            });
        }

        return response;
    }

    public override async Task<TopContributorsList> GetTopContributorsToday(GetTopContributorsTodayRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetTopContributorsTodayAsync(
            GetTrendingSince(),
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);

        if (!result.IsSuccess || result.Value is null)
            return new TopContributorsList();

        var response = new TopContributorsList();

        foreach (var c in result.Value.Items)
        {
            response.Items.Add(new TopContributorInfo
            {
                PublicId = c.UserId,
                DisplayName = c.DisplayName,
                PostCountToday = c.PostCountToday,

                AvatarUrl = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, 0)
            });
        }

        return response;
    }

    public override async Task<UserActivityHistory> GetUserActivityHistory(GetUserActivityHistoryRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetUserActivityHistoryAsync(request.PublicId, request.Days);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var history = result.Value;
        var response = new UserActivityHistory
        {
            Days = history.Days
        };

        foreach (var day in history.Data)
        {
            response.Data.Add(new ActivityDay
            {
                Date = day.Date.ToString("yyyy-MM-dd"),
                Discussions = day.Discussions,
                Posts = day.Posts,
                Total = day.Total
            });
        }

        return response;
    }

    public override async Task<UserStats> GetUserStats(GetUserStatsRequest request, ServerCallContext context)
    {
        var result = await statisticsUseCase.GetUserStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var stats = result.Value;

        return new UserStats
        {
            PublicId = stats.PublicId,
            DisplayName = stats.DisplayName,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount,

            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.User, 0)
        };
    }
}
