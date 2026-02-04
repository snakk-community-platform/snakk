namespace Snakk.Infrastructure.BackgroundJobs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using System.Text.Json;

public class AchievementCheckerWorker(
    IServiceProvider serviceProvider,
    ILogger<AchievementCheckerWorker> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<AchievementCheckerWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Achievement Checker Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAchievementsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Achievement Checker Worker");
            }

            // Run every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Achievement Checker Worker stopped");
    }

    private async Task CheckAchievementsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SnakkDbContext>();
        var achievementService = scope.ServiceProvider.GetRequiredService<AchievementService>();
        var achievementRepo = scope.ServiceProvider.GetRequiredService<IAchievementRepository>();
        var userAchievementRepo = scope.ServiceProvider.GetRequiredService<IUserAchievementRepository>();

        // Get all active COUNT-based achievements
        var achievements = await achievementRepo.GetAllActiveAsync();
        var countAchievements = achievements
            .Where(a => a.RequirementType == Shared.Enums.AchievementRequirementTypeEnum.Count)
            .ToList();

        if (!countAchievements.Any())
            return;

        // Get users with recent metric updates (last 60 seconds)
        var recentlyActiveUsers = await context.UserMetrics
            .Where(m => m.LastUpdated >= DateTime.UtcNow.AddSeconds(-60))
            .Select(m => m.User.PublicId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userPublicId in recentlyActiveUsers)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                await CheckUserAchievementsAsync(
                    UserId.From(userPublicId),
                    countAchievements,
                    context,
                    achievementService,
                    userAchievementRepo,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking achievements for user {UserId}", userPublicId);
            }
        }
    }

    private async Task CheckUserAchievementsAsync(
        UserId userId,
        List<Domain.Entities.Achievement> achievements,
        SnakkDbContext context,
        AchievementService achievementService,
        IUserAchievementRepository userAchievementRepo,
        CancellationToken ct)
    {
        var userIdInt = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userIdInt == 0)
            return;

        // Get user's global metrics
        var userMetrics = await context.UserMetrics
            .Where(m => m.UserId == userIdInt && m.Scope == "Global")
            .ToDictionaryAsync(m => m.MetricType, m => m.Value, ct);

        foreach (var achievement in achievements)
        {
            // Check if user already has this achievement
            var hasAchievement = await userAchievementRepo.HasAchievementAsync(userId, achievement.PublicId);
            if (hasAchievement)
                continue;

            // Parse requirement config
            var config = JsonSerializer.Deserialize<AchievementConfig>(achievement.RequirementConfig);
            if (config == null || string.IsNullOrEmpty(config.EventType))
                continue;

            // Map eventType to metricType
            var metricType = GetMetricTypeFromEventType(config.EventType);
            if (metricType == null)
                continue;

            // Check if user has reached the threshold
            if (userMetrics.TryGetValue(metricType, out var currentValue))
            {
                if (currentValue >= config.Target)
                {
                    // Award achievement!
                    _logger.LogInformation(
                        "Awarding achievement {Achievement} to user {UserId} (threshold: {Threshold}, actual: {Value})",
                        achievement.Slug,
                        userId,
                        config.Target,
                        currentValue);

                    await achievementService.AwardAchievementAsync(userId, achievement.PublicId);
                }
            }
        }
    }

    private static string? GetMetricTypeFromEventType(string eventType)
    {
        return eventType switch
        {
            "POST_CREATE" => "POST_COUNT",
            "DISCUSSION_CREATE" => "DISCUSSION_COUNT",
            "REACTION_GIVEN" => "REACTION_GIVEN",
            "REACTION_RECEIVED" => "REACTION_RECEIVED",
            _ => null
        };
    }

    private class AchievementConfig
    {
        public string EventType { get; set; } = string.Empty;
        public int Target { get; set; }
    }
}
