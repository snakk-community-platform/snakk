namespace Snakk.Worker.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using StackExchange.Redis;
using System.Text.Json;

public class StatsRollupWorker(
    IServiceProvider serviceProvider,
    IConnectionMultiplexer redis,
    ILogger<StatsRollupWorker> logger) : BackgroundService
{
    private static readonly string[] Periods = ["day", "week", "month", "year", "all_time"];
    private const int DefaultLimit = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Stats Rollup Worker started");

        var isCold = !(await redis.GetDatabase().KeyExistsAsync("snakk:stats:platform-stats"));
        if (isCold)
        {
            logger.LogInformation("Stats cache is cold — populating immediately");
            await RunIterationAsync(stoppingToken);
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

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
        var volumeWindow = scope.ServiceProvider.GetRequiredService<IVolumeWindowService>();

        var now = DateTime.UtcNow;
        var postSince = now - await volumeWindow.EstimatePostWindowAsync(ct);
        var allRows = new List<StatsRollupRow>();

        // platform-stats (single row)
        var platformStats = await useCase.GetPlatformStatsAsync();
        allRows.Add(new StatsRollupRow("platform-stats", 1, JsonSerializer.Serialize(platformStats), now));

        // trending-spaces (global)
        var trendingSpaces = await useCase.GetTrendingSpacesAsync(postSince, limit: DefaultLimit);
        allRows.AddRange(trendingSpaces.Select((s, i) => new StatsRollupRow("trending-spaces", i + 1, JsonSerializer.Serialize(s), now)));

        // trending-contributors (global)
        var trendingContributors = await useCase.GetTrendingContributorsAsync(postSince, limit: DefaultLimit);
        if (trendingContributors.IsSuccess && trendingContributors.Value is not null)
            allRows.AddRange(trendingContributors.Value.Items.Select((c, i) => new StatsRollupRow("trending-contributors", i + 1, JsonSerializer.Serialize(c), now)));

        // top-spaces-today (global)
        var topSpacesToday = await useCase.GetTopActiveSpacesTodayAsync(postSince, limit: DefaultLimit);
        allRows.AddRange(topSpacesToday.Select((s, i) => new StatsRollupRow("top-spaces-today", i + 1, JsonSerializer.Serialize(s), now)));

        // top-contributors-today (global)
        var topContributorsToday = await useCase.GetTopContributorsTodayAsync(postSince, limit: DefaultLimit);
        if (topContributorsToday.IsSuccess && topContributorsToday.Value is not null)
            allRows.AddRange(topContributorsToday.Value.Items.Select((c, i) => new StatsRollupRow("top-contributors-today", i + 1, JsonSerializer.Serialize(c), now)));

        // latest-active-spaces (global)
        var latestSpaces = await useCase.GetLatestActiveSpacesAsync(limit: DefaultLimit);
        allRows.AddRange(latestSpaces.Select((s, i) => new StatsRollupRow("latest-active-spaces", i + 1, JsonSerializer.Serialize(s), now)));

        // latest-contributors (global)
        var latestContributors = await useCase.GetLatestContributorsAsync(limit: DefaultLimit);
        if (latestContributors.IsSuccess && latestContributors.Value is not null)
            allRows.AddRange(latestContributors.Value.Items.Select((c, i) => new StatsRollupRow("latest-contributors", i + 1, JsonSerializer.Serialize(c), now)));

        // top-spaces-period:{period} and top-contributors-period:{period} for each period
        foreach (var period in Periods)
        {
            var since = GetPeriodSince(period);

            var topSpacesPeriod = await useCase.GetTopSpacesByPeriodAsync(since, limit: DefaultLimit);
            allRows.AddRange(topSpacesPeriod.Select((s, i) => new StatsRollupRow($"top-spaces-period:{period}", i + 1, JsonSerializer.Serialize(s), now)));

            var topContributorsPeriod = await useCase.GetTopContributorsByPeriodAsync(since, limit: DefaultLimit);
            if (topContributorsPeriod.IsSuccess && topContributorsPeriod.Value is not null)
                allRows.AddRange(topContributorsPeriod.Value.Items.Select((c, i) => new StatsRollupRow($"top-contributors-period:{period}", i + 1, JsonSerializer.Serialize(c), now)));
        }

        await rollupRepo.ReplaceAllAsync(allRows, ct);
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
