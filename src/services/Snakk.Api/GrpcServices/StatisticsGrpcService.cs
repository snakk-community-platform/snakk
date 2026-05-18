using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Protos;
using Snakk.Protos.Statistics;
using Snakk.Application.Repositories;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class StatisticsGrpcService(
    StatisticsUseCase statisticsUseCase,
    IActivitySnapshotRepository activitySnapshotRepository,
    IConfiguration configuration,
    ICurrentUserService currentUser) : StatisticsService.StatisticsServiceBase
{
    private DateTime GetTrendingSince() =>
        DateTime.UtcNow.AddHours(-configuration.GetValue("Trending:LookbackHours", 24));

    public override async Task<PlatformStats> GetPlatformStats(GetPlatformStatsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
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
        var ct = context.CancellationToken;
        var result = await statisticsUseCase.GetTopActiveDiscussionsTodayAsync(
            GetTrendingSince(),
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit,
            currentUser.GetCurrentUserId(),
            request.ViewerAllowsAdult);

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
                CommunitySlug = d.CommunitySlug,
                Author = new AuthorRef
                {
                    PublicId = d.AuthorPublicId,
                    DisplayName = d.AuthorDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.AuthorPublicId, AvatarEntityType.User, 0, d.AuthorAvatarFileName),
                    AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(d.AuthorPublicId, AvatarEntityType.User, 0, d.AuthorAvatarFileName, d.AuthorAvatarThumbnailFileName),
                    AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(d.AuthorPublicId, AvatarEntityType.User, 0, d.AuthorAvatarFileName)
                }
            });
        }

        return response;
    }

    public override async Task<TopActiveSpacesList> GetTopActiveSpacesToday(GetTopActiveSpacesTodayRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
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
                CommunitySlug = s.CommunitySlug,
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
        var ct = context.CancellationToken;
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

                AvatarUrl = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarThumbnailFileName),
                AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarMicroFileName)
            });
        }

        return response;
    }

    private static DateTime GetPeriodSince(string period) => period switch
    {
        "day"      => DateTime.UtcNow.AddDays(-1),
        "week"     => DateTime.UtcNow.AddDays(-7),
        "month"    => DateTime.UtcNow.AddMonths(-1),
        "year"     => DateTime.UtcNow.AddYears(-1),
        "all_time" => DateTime.MinValue,
        _          => DateTime.UtcNow.AddDays(-7)
    };

    private static TopActiveSpacesList MapSpaces(IEnumerable<TopActiveSpaceDto> spaces)
    {
        var response = new TopActiveSpacesList();
        foreach (var s in spaces)
        {
            response.Items.Add(new TopActiveSpaceInfo
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Slug = s.Slug,
                PostCountToday = s.PostCountToday,
                CommunitySlug = s.CommunitySlug,
                Hub = new EntityRef { PublicId = s.HubPublicId, Slug = s.HubSlug, Name = s.HubName }
            });
        }
        return response;
    }

    private TopContributorsList MapContributors(IEnumerable<TopContributorResult> contributors)
    {
        var response = new TopContributorsList();
        foreach (var c in contributors)
        {
            response.Items.Add(new TopContributorInfo
            {
                PublicId = c.UserId,
                DisplayName = c.DisplayName,
                PostCountToday = c.PostCountToday,
                AvatarUrl = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarThumbnailFileName),
                AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarMicroFileName)
            });
        }
        return response;
    }

    public override async Task<TopActiveSpacesList> GetTrendingSpaces(GetTrendingSpacesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var since = DateTime.UtcNow.AddDays(-configuration.GetValue("Trending:SpacesLookbackDays", 7));
        var spaces = await statisticsUseCase.GetTrendingSpacesAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return MapSpaces(spaces);
    }

    public override async Task<TopContributorsList> GetTrendingContributors(GetTrendingContributorsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var since = DateTime.UtcNow.AddDays(-configuration.GetValue("Trending:ContributorsLookbackDays", 7));
        var result = await statisticsUseCase.GetTrendingContributorsAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return result.IsSuccess && result.Value is not null ? MapContributors(result.Value.Items) : new TopContributorsList();
    }

    public override async Task<TopActiveSpacesList> GetTopSpacesByPeriod(GetTopSpacesByPeriodRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var since = GetPeriodSince(request.TimePeriod);
        var spaces = await statisticsUseCase.GetTopSpacesByPeriodAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return MapSpaces(spaces);
    }

    public override async Task<TopContributorsList> GetTopContributorsByPeriod(GetTopContributorsByPeriodRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var since = GetPeriodSince(request.TimePeriod);
        var result = await statisticsUseCase.GetTopContributorsByPeriodAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return result.IsSuccess && result.Value is not null ? MapContributors(result.Value.Items) : new TopContributorsList();
    }

    public override async Task<LatestSpacesList> GetLatestActiveSpaces(GetLatestActiveSpacesRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var spaces = await statisticsUseCase.GetLatestActiveSpacesAsync(
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        var response = new LatestSpacesList();
        foreach (var s in spaces)
        {
            response.Items.Add(new LatestSpaceInfo
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Slug = s.Slug,
                LastPostAt = s.LastPostAt.ToString("o"),
                CommunitySlug = s.CommunitySlug,
                Hub = new EntityRef { PublicId = s.HubPublicId, Slug = s.HubSlug, Name = s.HubName }
            });
        }
        return response;
    }

    public override async Task<LatestContributorsList> GetLatestContributors(GetLatestContributorsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var result = await statisticsUseCase.GetLatestContributorsAsync(
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        if (!result.IsSuccess || result.Value is null)
            return new LatestContributorsList();
        var response = new LatestContributorsList();
        foreach (var c in result.Value.Items)
        {
            response.Items.Add(new LatestContributorInfo
            {
                PublicId = c.UserId,
                DisplayName = c.DisplayName,
                LastPostAt = c.LastPostAt.ToString("o"),
                AvatarUrl = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarThumbnailFileName),
                AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarMicroFileName)
            });
        }
        return response;
    }

    public override async Task<UserActivityHistory> GetUserActivityHistory(GetUserActivityHistoryRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
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
        var ct = context.CancellationToken;
        var result = await statisticsUseCase.GetUserStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var stats = result.Value;

        var userStats = new UserStats
        {
            PublicId = stats.PublicId,
            DisplayName = stats.DisplayName,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount,
            AvatarUrl = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.User, 0, stats.AvatarFileName)
        };
        if (stats.Bio != null) userStats.Bio = stats.Bio;
        if (!string.IsNullOrEmpty(stats.AvatarThumbnailFileName))
        {
            userStats.AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(
                stats.PublicId, AvatarEntityType.User, 0, stats.AvatarFileName, stats.AvatarThumbnailFileName);
        }
        return userStats;
    }

    public override async Task<SparklineResponse> GetActivitySparkline(SparklineRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var entityType = request.EntityType switch
        {
            "community"  => ActivityEntityTypeEnum.Community,
            "hub"        => ActivityEntityTypeEnum.Hub,
            "space"      => ActivityEntityTypeEnum.Space,
            "discussion" => ActivityEntityTypeEnum.Discussion,
            "user"       => ActivityEntityTypeEnum.User,
            _            => ActivityEntityTypeEnum.Platform
        };

        var days = Math.Clamp(request.Days, 1, 90);
        var publicId = string.IsNullOrEmpty(request.PublicId) ? null : request.PublicId;

        var data = await activitySnapshotRepository.GetSparklineAsync(entityType, publicId, days, ct);

        var response = new SparklineResponse();
        foreach (var d in data)
        {
            response.Days.Add(new SparklineDay
            {
                Date            = d.Date.ToString("yyyy-MM-dd"),
                PostCount       = d.PostCount,
                DiscussionCount = d.DiscussionCount
            });
        }
        return response;
    }

    public override async Task<SparklineBatchResponse> GetActivitySparklinesBatch(
        SparklineBatchRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var days = Math.Clamp(request.Days, 1, 90);
        var data = await activitySnapshotRepository.GetSparklinesForSpacesAsync(
            request.PublicIds, days, ct);

        var response = new SparklineBatchResponse();
        foreach (var (publicId, sparkline) in data)
        {
            var entry = new SpaceSparklineEntry { PublicId = publicId };
            foreach (var d in sparkline)
                entry.Days.Add(new SparklineDay
                {
                    Date            = d.Date.ToString("yyyy-MM-dd"),
                    PostCount       = d.PostCount,
                    DiscussionCount = d.DiscussionCount
                });
            response.Entries.Add(entry);
        }
        return response;
    }
}
