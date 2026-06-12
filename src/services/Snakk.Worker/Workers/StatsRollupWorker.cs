namespace Snakk.Worker.Workers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using System.Text.Json;

public class StatsRollupWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<StatsRollupWorker> logger) : BackgroundService
{
    private static readonly string[] Periods = ["day", "week", "month", "year", "all_time"];
    private const int DefaultLimit = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Stats Rollup Worker started");

        // Short startup delay so the rollup is populated quickly after launch
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIterationAsync(stoppingToken);
                logger.LogInformation("Stats rollup computed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Stats Rollup Worker");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }

        logger.LogInformation("Stats Rollup Worker stopped");
    }

    private async Task RunIterationAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<StatisticsUseCase>();
        var rollupRepo = scope.ServiceProvider.GetRequiredService<IStatsRollupRepository>();

        var now = DateTime.UtcNow;
        var trendingLookbackHours = configuration.GetValue("Trending:LookbackHours", 24);
        var spacesLookbackDays = configuration.GetValue("Trending:SpacesLookbackDays", 7);
        var contributorsLookbackDays = configuration.GetValue("Trending:ContributorsLookbackDays", 7);

        var trendingSince = now.AddHours(-trendingLookbackHours);
        var spacesTrendingSince = now.AddDays(-spacesLookbackDays);
        var contributorsTrendingSince = now.AddDays(-contributorsLookbackDays);

        // platform-stats (single row)
        var platformStats = await useCase.GetPlatformStatsAsync();
        await rollupRepo.ReplaceKindAsync("platform-stats",
            [new StatsRollupRow("platform-stats", 1, JsonSerializer.Serialize(platformStats), now)], ct);

        // trending-spaces (global)
        var trendingSpaces = await useCase.GetTrendingSpacesAsync(spacesTrendingSince, limit: DefaultLimit);
        await rollupRepo.ReplaceKindAsync("trending-spaces",
            trendingSpaces.Select((s, i) => new StatsRollupRow("trending-spaces", i + 1, JsonSerializer.Serialize(s), now)).ToList(), ct);

        // trending-contributors (global)
        var trendingContributors = await useCase.GetTrendingContributorsAsync(contributorsTrendingSince, limit: DefaultLimit);
        if (trendingContributors.IsSuccess && trendingContributors.Value is not null)
            await rollupRepo.ReplaceKindAsync("trending-contributors",
                trendingContributors.Value.Items.Select((c, i) => new StatsRollupRow("trending-contributors", i + 1, JsonSerializer.Serialize(c), now)).ToList(), ct);

        // top-spaces-today (global)
        var topSpacesToday = await useCase.GetTopActiveSpacesTodayAsync(trendingSince, limit: DefaultLimit);
        await rollupRepo.ReplaceKindAsync("top-spaces-today",
            topSpacesToday.Select((s, i) => new StatsRollupRow("top-spaces-today", i + 1, JsonSerializer.Serialize(s), now)).ToList(), ct);

        // top-contributors-today (global)
        var topContributorsToday = await useCase.GetTopContributorsTodayAsync(trendingSince, limit: DefaultLimit);
        if (topContributorsToday.IsSuccess && topContributorsToday.Value is not null)
            await rollupRepo.ReplaceKindAsync("top-contributors-today",
                topContributorsToday.Value.Items.Select((c, i) => new StatsRollupRow("top-contributors-today", i + 1, JsonSerializer.Serialize(c), now)).ToList(), ct);

        // latest-active-spaces (global)
        var latestSpaces = await useCase.GetLatestActiveSpacesAsync(limit: DefaultLimit);
        await rollupRepo.ReplaceKindAsync("latest-active-spaces",
            latestSpaces.Select((s, i) => new StatsRollupRow("latest-active-spaces", i + 1, JsonSerializer.Serialize(s), now)).ToList(), ct);

        // latest-contributors (global)
        var latestContributors = await useCase.GetLatestContributorsAsync(limit: DefaultLimit);
        if (latestContributors.IsSuccess && latestContributors.Value is not null)
            await rollupRepo.ReplaceKindAsync("latest-contributors",
                latestContributors.Value.Items.Select((c, i) => new StatsRollupRow("latest-contributors", i + 1, JsonSerializer.Serialize(c), now)).ToList(), ct);

        // top-spaces-period:{period} and top-contributors-period:{period} for each period
        foreach (var period in Periods)
        {
            var since = GetPeriodSince(period);

            var topSpacesPeriod = await useCase.GetTopSpacesByPeriodAsync(since, limit: DefaultLimit);
            await rollupRepo.ReplaceKindAsync($"top-spaces-period:{period}",
                topSpacesPeriod.Select((s, i) => new StatsRollupRow($"top-spaces-period:{period}", i + 1, JsonSerializer.Serialize(s), now)).ToList(), ct);

            var topContributorsPeriod = await useCase.GetTopContributorsByPeriodAsync(since, limit: DefaultLimit);
            if (topContributorsPeriod.IsSuccess && topContributorsPeriod.Value is not null)
                await rollupRepo.ReplaceKindAsync($"top-contributors-period:{period}",
                    topContributorsPeriod.Value.Items.Select((c, i) => new StatsRollupRow($"top-contributors-period:{period}", i + 1, JsonSerializer.Serialize(c), now)).ToList(), ct);
        }
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
}
