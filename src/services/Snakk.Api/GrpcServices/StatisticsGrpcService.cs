using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.UseCases;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Protos;
using Snakk.Protos.Statistics;
using Snakk.Application.Repositories;
using Snakk.Shared.Enums;
using System.Text.Json;

namespace Snakk.Api.GrpcServices;

public class StatisticsGrpcService(
    StatisticsUseCase statisticsUseCase,
    IActivitySnapshotRepository activitySnapshotRepository,
    IDiscussionViewRepository discussionViewRepository,
    ICurrentUserService currentUser,
    HybridCache cache,
    IStatsRollupRepository rollupRepository,
    IUserRepository userRepository,
    IVolumeWindowService volumeWindowService) : StatisticsService.StatisticsServiceBase
{
    private static readonly TimeSpan RollupFreshnessCap = TimeSpan.FromMinutes(10);

    // -------------------------------------------------------------------------
    // Cache options
    // -------------------------------------------------------------------------

    private static readonly HybridCacheEntryOptions PlatformStatsCacheOptions     = new() { Expiration = TimeSpan.FromSeconds(60) };
    private static readonly HybridCacheEntryOptions TwoMinCacheOptions            = new() { Expiration = TimeSpan.FromMinutes(2) };
    private static readonly HybridCacheEntryOptions FiveMinCacheOptions           = new() { Expiration = TimeSpan.FromMinutes(5) };

    // Keep a named alias so existing callers that reference TrendingCacheOptions compile unchanged.
    private static readonly HybridCacheEntryOptions TrendingCacheOptions = TwoMinCacheOptions;

    // -------------------------------------------------------------------------
    // Rollup helpers
    // -------------------------------------------------------------------------

    private static bool IsStale(List<StatsRollupRow> rows) =>
        rows.Count == 0 || (DateTime.UtcNow - rows[0].ComputedAt) > RollupFreshnessCap;

    // -------------------------------------------------------------------------
    // GetPlatformStats — TTL 60 s
    // -------------------------------------------------------------------------

    public override Task<PlatformStats> GetPlatformStats(GetPlatformStatsRequest request, ServerCallContext context)
    {
        const string key = "stats:platform";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchPlatformStatsAsync(ct),
            PlatformStatsCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<PlatformStats> FetchPlatformStatsAsync(CancellationToken ct)
    {
        // Try rollup first (global stat — always pre-computed)
        var rows = await rollupRepository.GetRowsAsync("platform-stats", ct);
        if (!IsStale(rows))
        {
            var dto = JsonSerializer.Deserialize<PlatformStatsDto>(rows[0].PayloadJson)!;
            return new PlatformStats
            {
                HubCount        = dto.HubCount,
                SpaceCount      = dto.SpaceCount,
                DiscussionCount = dto.DiscussionCount,
                ReplyCount      = dto.ReplyCount
            };
        }
        // Fallback: live aggregation (cold-start safety)
        var stats = await statisticsUseCase.GetPlatformStatsAsync();
        return new PlatformStats
        {
            HubCount        = stats.HubCount,
            SpaceCount      = stats.SpaceCount,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount      = stats.ReplyCount
        };
    }

    // -------------------------------------------------------------------------
    // GetTopActiveDiscussionsToday — TTL 2 min  (original cached method)
    // -------------------------------------------------------------------------

    public override Task<TopActiveDiscussionsList> GetTopActiveDiscussionsToday(
        GetTopActiveDiscussionsTodayRequest request, ServerCallContext context)
    {
        var userId = currentUser.GetCurrentUserId() ?? "";
        var key = $"trending:{(request.HasHubId ? request.HubId : "")}:{(request.HasSpaceId ? request.SpaceId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}:{userId}:{request.ViewerAllowsAdult}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTopActiveDiscussionsTodayAsync(request, userId, ct),
            TrendingCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopActiveDiscussionsList> FetchTopActiveDiscussionsTodayAsync(
        GetTopActiveDiscussionsTodayRequest request, string userId, CancellationToken ct)
    {
        var result = await statisticsUseCase.GetTopActiveDiscussionsTodayAsync(
            DateTime.UtcNow - await volumeWindowService.EstimateDiscussionWindowAsync(ct),
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit,
            string.IsNullOrEmpty(userId) ? null : userId,
            request.ViewerAllowsAdult);

        if (!result.IsSuccess || result.Value is null)
            return new TopActiveDiscussionsList();

        var authorIds = result.Value.Items
            .Select(d => d.AuthorPublicId)
            .OfType<string>()
            .Distinct()
            .ToList();
        var authorSlugs = await userRepository.GetSlugsByPublicIdsAsync(authorIds, ct);

        var response = new TopActiveDiscussionsList();
        foreach (var d in result.Value.Items)
        {
            // Coalesce: GDPR-anonymized authors have null ids; proto setters throw on null
            var authorPid = d.AuthorPublicId ?? "";
            var authorRef = new AuthorRef
            {
                PublicId = authorPid,
                DisplayName = d.AuthorDisplayName ?? "",
                AvatarUrl = AvatarHelper.GetAvatarUrl(authorPid, AvatarEntityType.User, 0, d.AuthorAvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(authorPid, AvatarEntityType.User, 0, d.AuthorAvatarFileName, d.AuthorAvatarThumbnailFileName),
                AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(authorPid, AvatarEntityType.User, 0, d.AuthorAvatarFileName)
            };
            if (authorPid.Length > 0 && authorSlugs.TryGetValue(authorPid, out var authorSlug))
                authorRef.Slug = authorSlug;

            response.Items.Add(new TopActiveDiscussionInfo
            {
                PublicId = d.DiscussionId,
                Title = d.Title,
                Slug = d.Slug,
                PostCountToday = d.PostCountToday,
                Space = new EntityRef { PublicId = d.SpacePublicId, Slug = d.SpaceSlug, Name = d.SpaceName },
                Hub = new EntityRef { PublicId = d.HubPublicId, Slug = d.HubSlug, Name = d.HubName },
                CommunitySlug = d.CommunitySlug,
                Author = authorRef
            });
        }
        return response;
    }

    // -------------------------------------------------------------------------
    // GetTopActiveSpacesToday — TTL 2 min
    // -------------------------------------------------------------------------

    public override Task<TopActiveSpacesList> GetTopActiveSpacesToday(GetTopActiveSpacesTodayRequest request, ServerCallContext context)
    {
        var key = $"stats:top-spaces-today:{(request.HasHubId ? request.HubId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTopActiveSpacesTodayAsync(request, ct),
            TwoMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopActiveSpacesList> FetchTopActiveSpacesTodayAsync(GetTopActiveSpacesTodayRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync("top-spaces-today", ct);
            if (!IsStale(rows))
            {
                var items = rows.Take(request.Limit).Select(r => JsonSerializer.Deserialize<TopActiveSpaceDto>(r.PayloadJson)!);
                return MapSpaces(items);
            }
        }
        // Scoped or stale: live aggregation
        var spaces = await statisticsUseCase.GetTopActiveSpacesTodayAsync(
            DateTime.UtcNow - await volumeWindowService.EstimatePostWindowAsync(ct),
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return MapSpaces(spaces);
    }

    // -------------------------------------------------------------------------
    // GetTopContributorsToday — TTL 2 min
    // -------------------------------------------------------------------------

    public override Task<TopContributorsList> GetTopContributorsToday(GetTopContributorsTodayRequest request, ServerCallContext context)
    {
        var key = $"stats:top-contributors-today:{(request.HasHubId ? request.HubId : "")}:{(request.HasSpaceId ? request.SpaceId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTopContributorsTodayAsync(request, ct),
            TwoMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopContributorsList> FetchTopContributorsTodayAsync(GetTopContributorsTodayRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasSpaceId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync("top-contributors-today", ct);
            if (!IsStale(rows))
            {
                var items = rows.Take(request.Limit).Select(r => JsonSerializer.Deserialize<TopContributorResult>(r.PayloadJson)!);
                return MapContributors(items);
            }
        }
        // Scoped or stale: live aggregation
        var result = await statisticsUseCase.GetTopContributorsTodayAsync(
            DateTime.UtcNow - await volumeWindowService.EstimatePostWindowAsync(ct),
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        if (!result.IsSuccess || result.Value is null)
            return new TopContributorsList();
        return MapContributors(result.Value.Items);
    }

    // -------------------------------------------------------------------------
    // Period helpers
    // -------------------------------------------------------------------------

    private static DateTime GetPeriodSince(string period) => period switch
    {
        "day"      => DateTime.UtcNow.AddDays(-1),
        "week"     => DateTime.UtcNow.AddDays(-7),
        "month"    => DateTime.UtcNow.AddMonths(-1),
        "year"     => DateTime.UtcNow.AddYears(-1),
        "all_time" => DateTime.MinValue,
        _          => DateTime.UtcNow.AddDays(-7)
    };

    // -------------------------------------------------------------------------
    // GetTrendingSpaces — TTL 5 min
    // -------------------------------------------------------------------------

    public override Task<TopActiveSpacesList> GetTrendingSpaces(GetTrendingSpacesRequest request, ServerCallContext context)
    {
        var key = $"stats:trending-spaces:{(request.HasHubId ? request.HubId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTrendingSpacesAsync(request, ct),
            FiveMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopActiveSpacesList> FetchTrendingSpacesAsync(GetTrendingSpacesRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync("trending-spaces", ct);
            if (!IsStale(rows))
            {
                var items = rows.Take(request.Limit).Select(r => JsonSerializer.Deserialize<TopActiveSpaceDto>(r.PayloadJson)!);
                return MapSpaces(items);
            }
        }
        // Scoped or stale: live aggregation
        var since = DateTime.UtcNow - await volumeWindowService.EstimatePostWindowAsync(ct);
        var spaces = await statisticsUseCase.GetTrendingSpacesAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return MapSpaces(spaces);
    }

    // -------------------------------------------------------------------------
    // GetTrendingContributors — TTL 5 min
    // -------------------------------------------------------------------------

    public override Task<TopContributorsList> GetTrendingContributors(GetTrendingContributorsRequest request, ServerCallContext context)
    {
        var key = $"stats:trending-contributors:{(request.HasHubId ? request.HubId : "")}:{(request.HasSpaceId ? request.SpaceId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTrendingContributorsAsync(request, ct),
            FiveMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopContributorsList> FetchTrendingContributorsAsync(GetTrendingContributorsRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasSpaceId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync("trending-contributors", ct);
            if (!IsStale(rows))
            {
                var items = rows.Take(request.Limit).Select(r => JsonSerializer.Deserialize<TopContributorResult>(r.PayloadJson)!);
                return MapContributors(items);
            }
        }
        // Scoped or stale: live aggregation
        var since = DateTime.UtcNow - await volumeWindowService.EstimatePostWindowAsync(ct);
        var result = await statisticsUseCase.GetTrendingContributorsAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return result.IsSuccess && result.Value is not null ? MapContributors(result.Value.Items) : new TopContributorsList();
    }

    // -------------------------------------------------------------------------
    // GetTopSpacesByPeriod — TTL 5 min
    // -------------------------------------------------------------------------

    public override Task<TopActiveSpacesList> GetTopSpacesByPeriod(GetTopSpacesByPeriodRequest request, ServerCallContext context)
    {
        var key = $"stats:top-spaces-period:{request.TimePeriod}:{(request.HasHubId ? request.HubId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTopSpacesByPeriodAsync(request, ct),
            FiveMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopActiveSpacesList> FetchTopSpacesByPeriodAsync(GetTopSpacesByPeriodRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync($"top-spaces-period:{request.TimePeriod}", ct);
            if (!IsStale(rows))
            {
                var items = rows.Take(request.Limit).Select(r => JsonSerializer.Deserialize<TopActiveSpaceDto>(r.PayloadJson)!);
                return MapSpaces(items);
            }
        }
        // Scoped or stale: live aggregation
        var since = GetPeriodSince(request.TimePeriod);
        var spaces = await statisticsUseCase.GetTopSpacesByPeriodAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return MapSpaces(spaces);
    }

    // -------------------------------------------------------------------------
    // GetTopContributorsByPeriod — TTL 5 min
    // -------------------------------------------------------------------------

    public override Task<TopContributorsList> GetTopContributorsByPeriod(GetTopContributorsByPeriodRequest request, ServerCallContext context)
    {
        var key = $"stats:top-contributors-period:{request.TimePeriod}:{(request.HasHubId ? request.HubId : "")}:{(request.HasSpaceId ? request.SpaceId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchTopContributorsByPeriodAsync(request, ct),
            FiveMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<TopContributorsList> FetchTopContributorsByPeriodAsync(GetTopContributorsByPeriodRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasSpaceId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync($"top-contributors-period:{request.TimePeriod}", ct);
            if (!IsStale(rows))
            {
                var items = rows.Take(request.Limit).Select(r => JsonSerializer.Deserialize<TopContributorResult>(r.PayloadJson)!);
                return MapContributors(items);
            }
        }
        // Scoped or stale: live aggregation
        var since = GetPeriodSince(request.TimePeriod);
        var result = await statisticsUseCase.GetTopContributorsByPeriodAsync(
            since,
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        return result.IsSuccess && result.Value is not null ? MapContributors(result.Value.Items) : new TopContributorsList();
    }

    // -------------------------------------------------------------------------
    // GetLatestActiveSpaces — TTL 2 min
    // -------------------------------------------------------------------------

    public override Task<LatestSpacesList> GetLatestActiveSpaces(GetLatestActiveSpacesRequest request, ServerCallContext context)
    {
        var key = $"stats:latest-spaces:{(request.HasHubId ? request.HubId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchLatestActiveSpacesAsync(request, ct),
            TwoMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<LatestSpacesList> FetchLatestActiveSpacesAsync(GetLatestActiveSpacesRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync("latest-active-spaces", ct);
            if (!IsStale(rows))
            {
                var response = new LatestSpacesList();
                foreach (var row in rows.Take(request.Limit))
                {
                    var s = JsonSerializer.Deserialize<LatestActiveSpaceDto>(row.PayloadJson)!;
                    response.Items.Add(new LatestSpaceInfo
                    {
                        PublicId      = s.PublicId,
                        Name          = s.Name,
                        Slug          = s.Slug,
                        LastPostAt    = s.LastPostAt.ToString("o"),
                        CommunitySlug = s.CommunitySlug,
                        Hub           = new EntityRef { PublicId = s.HubPublicId, Slug = s.HubSlug, Name = s.HubName }
                    });
                }
                return response;
            }
        }
        // Scoped or stale: live aggregation
        var spaces = await statisticsUseCase.GetLatestActiveSpacesAsync(
            request.HasHubId ? request.HubId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        var fallbackResponse = new LatestSpacesList();
        foreach (var s in spaces)
        {
            fallbackResponse.Items.Add(new LatestSpaceInfo
            {
                PublicId      = s.PublicId,
                Name          = s.Name,
                Slug          = s.Slug,
                LastPostAt    = s.LastPostAt.ToString("o"),
                CommunitySlug = s.CommunitySlug,
                Hub           = new EntityRef { PublicId = s.HubPublicId, Slug = s.HubSlug, Name = s.HubName }
            });
        }
        return fallbackResponse;
    }

    // -------------------------------------------------------------------------
    // GetLatestContributors — TTL 2 min
    // -------------------------------------------------------------------------

    public override Task<LatestContributorsList> GetLatestContributors(GetLatestContributorsRequest request, ServerCallContext context)
    {
        var key = $"stats:latest-contributors:{(request.HasHubId ? request.HubId : "")}:{(request.HasSpaceId ? request.SpaceId : "")}:{(request.HasCommunityId ? request.CommunityId : "")}:{request.Limit}";
        return cache.GetOrCreateAsync(
            key,
            ct => FetchLatestContributorsAsync(request, ct),
            TwoMinCacheOptions,
            cancellationToken: context.CancellationToken).AsTask();
    }

    private async ValueTask<LatestContributorsList> FetchLatestContributorsAsync(GetLatestContributorsRequest request, CancellationToken ct)
    {
        // Use rollup for global (unscoped) requests
        if (!request.HasHubId && !request.HasSpaceId && !request.HasCommunityId)
        {
            var rows = await rollupRepository.GetRowsAsync("latest-contributors", ct);
            if (!IsStale(rows))
            {
                var response = new LatestContributorsList();
                foreach (var row in rows.Take(request.Limit))
                {
                    var c = JsonSerializer.Deserialize<LatestContributorResult>(row.PayloadJson)!;
                    var item = new LatestContributorInfo
                    {
                        PublicId           = c.UserId,
                        DisplayName        = c.DisplayName,
                        LastPostAt         = c.LastPostAt.ToString("o"),
                        AvatarUrl          = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName),
                        AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarThumbnailFileName),
                        AvatarMicroUrl     = AvatarHelper.GetAvatarMicroUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarMicroFileName)
                    };
                    if (c.Slug is not null) item.Slug = c.Slug;
                    response.Items.Add(item);
                }
                return response;
            }
        }
        // Scoped or stale: live aggregation
        var result = await statisticsUseCase.GetLatestContributorsAsync(
            request.HasHubId ? request.HubId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasCommunityId ? request.CommunityId : null,
            request.Limit);
        if (!result.IsSuccess || result.Value is null)
            return new LatestContributorsList();
        var fallbackResponse = new LatestContributorsList();
        foreach (var c in result.Value.Items)
        {
            var latestItem = new LatestContributorInfo
            {
                PublicId           = c.UserId,
                DisplayName        = c.DisplayName,
                LastPostAt         = c.LastPostAt.ToString("o"),
                AvatarUrl          = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarThumbnailFileName),
                AvatarMicroUrl     = AvatarHelper.GetAvatarMicroUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarMicroFileName)
            };
            if (c.Slug is not null) latestItem.Slug = c.Slug;
            fallbackResponse.Items.Add(latestItem);
        }
        return fallbackResponse;
    }

    // -------------------------------------------------------------------------
    // Mapping helpers
    // -------------------------------------------------------------------------

    private static TopActiveSpacesList MapSpaces(IEnumerable<TopActiveSpaceDto> spaces)
    {
        var response = new TopActiveSpacesList();
        foreach (var s in spaces)
        {
            response.Items.Add(new TopActiveSpaceInfo
            {
                PublicId      = s.PublicId,
                Name          = s.Name,
                Slug          = s.Slug,
                PostCountToday = s.PostCountToday,
                CommunitySlug = s.CommunitySlug,
                Hub           = new EntityRef { PublicId = s.HubPublicId, Slug = s.HubSlug, Name = s.HubName }
            });
        }
        return response;
    }

    private TopContributorsList MapContributors(IEnumerable<TopContributorResult> contributors)
    {
        var response = new TopContributorsList();
        foreach (var c in contributors)
        {
            var item = new TopContributorInfo
            {
                PublicId           = c.UserId,
                DisplayName        = c.DisplayName,
                PostCountToday     = c.PostCountToday,
                AvatarUrl          = AvatarHelper.GetAvatarUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarThumbnailFileName),
                AvatarMicroUrl     = AvatarHelper.GetAvatarMicroUrl(c.UserId, AvatarEntityType.User, c.AvatarRevision, c.AvatarFileName, c.AvatarMicroFileName)
            };
            if (c.Slug is not null) item.Slug = c.Slug;
            response.Items.Add(item);
        }
        return response;
    }

    // -------------------------------------------------------------------------
    // Non-cached methods (user-specific or write paths — not appropriate to cache)
    // -------------------------------------------------------------------------

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
                Date        = day.Date.ToString("yyyy-MM-dd"),
                Discussions = day.Discussions,
                Posts       = day.Posts,
                Total       = day.Total
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
            PublicId        = stats.PublicId,
            DisplayName     = stats.DisplayName,
            DiscussionCount = stats.DiscussionCount,
            ReplyCount      = stats.ReplyCount,
            FollowerCount   = stats.FollowerCount,
            AvatarUrl       = AvatarHelper.GetAvatarUrl(stats.PublicId, AvatarEntityType.User, 0, stats.AvatarFileName),
            IsGlobalAdmin   = stats.IsGlobalAdmin
        };
        if (stats.Bio != null) userStats.Bio = stats.Bio;
        if (!string.IsNullOrEmpty(stats.AvatarThumbnailFileName))
        {
            userStats.AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(
                stats.PublicId, AvatarEntityType.User, 0, stats.AvatarFileName, stats.AvatarThumbnailFileName);
        }
        return userStats;
    }

    public override Task<SparklineResponse> GetActivitySparkline(SparklineRequest request, ServerCallContext context)
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

        var days     = Math.Clamp(request.Days, 1, 90);
        var publicId = string.IsNullOrEmpty(request.PublicId) ? null : request.PublicId;

        // Aggregate: TTL-only, no write-path invalidation (data from ActivityDailySnapshot, refreshed by background worker)
        var cacheKey = $"sparkline:{request.EntityType}:{publicId ?? "platform"}:{days}";
        return cache.GetOrCreateAsync(cacheKey, async cToken =>
        {
            var data = await activitySnapshotRepository.GetSparklineAsync(entityType, publicId, days, cToken);
            var response = new SparklineResponse();
            foreach (var d in data)
                response.Days.Add(new SparklineDay { Date = d.Date.ToString("yyyy-MM-dd"), PostCount = d.PostCount, DiscussionCount = d.DiscussionCount });
            return response;
        }, FiveMinCacheOptions, cancellationToken: ct).AsTask();
    }

    public override async Task<SparklineBatchResponse> GetActivitySparklinesBatch(
        SparklineBatchRequest request, ServerCallContext context)
    {
        var ct         = context.CancellationToken;
        var days       = Math.Clamp(request.Days, 1, 90);
        var entityType = string.IsNullOrEmpty(request.EntityType) ? "space" : request.EntityType;

        var data = entityType == "community"
            ? await activitySnapshotRepository.GetSparklinesForCommunitiesAsync(request.PublicIds, days, ct)
            : await activitySnapshotRepository.GetSparklinesForSpacesAsync(request.PublicIds, days, ct);

        var response = new SparklineBatchResponse();
        foreach (var (publicId, sparkline) in data)
        {
            var entry = new SpaceSparklineEntry { PublicId = publicId };
            foreach (var d in sparkline)
                entry.Days.Add(new SparklineDay { Date = d.Date.ToString("yyyy-MM-dd"), PostCount = d.PostCount, DiscussionCount = d.DiscussionCount });
            response.Entries.Add(entry);
        }
        return response;
    }

    public override async Task<RecordBatchViewsResponse> RecordBatchViews(RecordBatchViewsRequest request, ServerCallContext context)
    {
        if (request.Entries.Count == 0)
            return new RecordBatchViewsResponse();

        var counts = request.Entries
            .Where(e => !string.IsNullOrEmpty(e.DiscussionPublicId) && e.Count > 0)
            .ToDictionary(
                e => (e.DiscussionPublicId, e.CountryCode),
                e => e.Count);

        if (counts.Count > 0)
            await discussionViewRepository.FlushViewsAsync(counts, context.CancellationToken);

        return new RecordBatchViewsResponse();
    }
}
