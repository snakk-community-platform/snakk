namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Interceptors;

/// <summary>
/// Composition-root wiring for the persistence layer. Lives in Infrastructure so that
/// host projects (Snakk.Api etc.) never need to reference Snakk.Infrastructure.Database
/// or name SnakkDbContext / repository implementation types directly.
/// </summary>
public static class InfrastructureDatabaseRegistration
{
    public static IServiceCollection AddSnakkDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(
            configuration.GetConnectionString("DbConnection"))
        {
            // max_connections=300 on server. Budget: Api=200, Worker=20, Auth=20+5 → 245 peak, 55 headroom.
            MaxPoolSize = 200,
            MinPoolSize = 5,
            Timeout = 30,
            ConnectionIdleLifetime = 300
        }.ToString();

        services.AddSingleton<SlowQueryInterceptor>();
        services.AddDbContextPool<SnakkDbContext>((sp, options) =>
            ConfigureSnakkDbOptions(options, connectionString, sp),
            poolSize: 128);

        // Factory for services that need per-operation contexts (e.g. CounterService parallel updates)
        services.AddPooledDbContextFactory<SnakkDbContext>((sp, options) =>
            ConfigureSnakkDbOptions(options, connectionString, sp));

        return services;
    }

    private static void ConfigureSnakkDbOptions(DbContextOptionsBuilder options, string connectionString, IServiceProvider sp) =>
        options
            .UseNpgsql(
                connectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                      .CommandTimeout(60))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
            .AddInterceptors(sp.GetRequiredService<SlowQueryInterceptor>())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                    .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

    /// <summary>Registers a DB connectivity health check without exposing SnakkDbContext to the host.</summary>
    public static IHealthChecksBuilder AddSnakkDbHealthCheck(this IHealthChecksBuilder builder) =>
        builder.AddDbContextCheck<SnakkDbContext>();

    public static IServiceCollection AddSnakkRepositories(this IServiceCollection services)
    {
        // Database Repositories
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.ICommunityDatabaseRepository, Snakk.Infrastructure.Database.Repositories.CommunityDatabaseRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IHubRepository, Snakk.Infrastructure.Database.Repositories.HubRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.ISpaceRepository, Snakk.Infrastructure.Database.Repositories.SpaceRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IDiscussionRepository, Snakk.Infrastructure.Database.Repositories.DiscussionRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IPostRepository, Snakk.Infrastructure.Database.Repositories.PostRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserRepository, Snakk.Infrastructure.Database.Repositories.UserRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IReactionDatabaseRepository, Snakk.Infrastructure.Database.Repositories.ReactionDatabaseRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.INotificationDatabaseRepository, Snakk.Infrastructure.Database.Repositories.NotificationDatabaseRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IFollowDatabaseRepository, Snakk.Infrastructure.Database.Repositories.FollowDatabaseRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IMentionDatabaseRepository, Snakk.Infrastructure.Database.Repositories.MentionDatabaseRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IBannerDatabaseRepository, Snakk.Infrastructure.Database.Repositories.BannerDatabaseRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IAchievementRepository, Snakk.Infrastructure.Database.Repositories.AchievementRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserAchievementRepository, Snakk.Infrastructure.Database.Repositories.UserAchievementRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserAchievementProgressRepository, Snakk.Infrastructure.Database.Repositories.UserAchievementProgressRepository>();

        // Moderation Repositories
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserRoleRepository, Snakk.Infrastructure.Database.Repositories.UserRoleRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserBanRepository, Snakk.Infrastructure.Database.Repositories.UserBanRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IReportRepository, Snakk.Infrastructure.Database.Repositories.ReportRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IReportCommentRepository, Snakk.Infrastructure.Database.Repositories.ReportCommentRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IReportReasonRepository, Snakk.Infrastructure.Database.Repositories.ReportReasonRepository>();
        services.AddScoped<Snakk.Infrastructure.Database.Repositories.IModerationLogRepository, Snakk.Infrastructure.Database.Repositories.ModerationLogRepository>();

        // Refresh Token Repository
        services.AddScoped<Snakk.Domain.Repositories.IRefreshTokenRepository, Snakk.Infrastructure.Database.Repositories.RefreshTokenRepository>();

        // Domain Repository Adapters
        services.AddScoped<Snakk.Domain.Repositories.ICommunityRepository, Snakk.Infrastructure.Adapters.CommunityRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IHubRepository, Snakk.Infrastructure.Adapters.HubRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.ISpaceRepository, Snakk.Infrastructure.Adapters.SpaceRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IDiscussionRepository, Snakk.Infrastructure.Adapters.DiscussionRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IPostRepository, Snakk.Infrastructure.Adapters.PostRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IUserRepository, Snakk.Infrastructure.Adapters.UserRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IDiscussionReadStateRepository, Snakk.Infrastructure.Adapters.DiscussionReadStateRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IReactionRepository, Snakk.Infrastructure.Adapters.ReactionRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.INotificationRepository, Snakk.Infrastructure.Adapters.NotificationRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IFollowRepository, Snakk.Infrastructure.Adapters.FollowRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IMentionRepository, Snakk.Infrastructure.Adapters.MentionRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IBannerRepository, Snakk.Infrastructure.Adapters.BannerRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IAchievementRepository, Snakk.Infrastructure.Adapters.AchievementRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IUserAchievementRepository, Snakk.Infrastructure.Adapters.UserAchievementRepositoryAdapter>();
        services.AddScoped<Snakk.Domain.Repositories.IUserAchievementProgressRepository, Snakk.Infrastructure.Adapters.UserAchievementProgressRepositoryAdapter>();

        // Application-layer repository interfaces with Infrastructure implementations
        services.AddScoped<Snakk.Application.Repositories.ISearchRepository, Snakk.Infrastructure.Database.Repositories.SearchRepository>();
        services.AddScoped<Snakk.Application.Repositories.ISaveRepository, Snakk.Infrastructure.Database.Repositories.SaveRepository>();
        services.AddScoped<Snakk.Application.Repositories.IUserSocialLinkRepository, Snakk.Infrastructure.Database.Repositories.UserSocialLinkRepository>();
        services.AddScoped<Snakk.Application.Repositories.IReactionQueryRepository, Snakk.Infrastructure.Database.Repositories.ReactionQueryRepository>();
        services.AddScoped<Snakk.Application.Repositories.IModerationRepository, Snakk.Infrastructure.Database.Repositories.ModerationRepository>();
        services.AddScoped<Snakk.Application.Repositories.IStatsRepository, Snakk.Infrastructure.Database.Repositories.StatsRepository>();
        services.AddScoped<Snakk.Application.Repositories.IActivitySnapshotRepository, Snakk.Infrastructure.Database.Repositories.ActivitySnapshotRepository>();
        services.AddScoped<Snakk.Application.Repositories.IDiscussionViewRepository, Snakk.Infrastructure.Database.Repositories.DiscussionViewRepository>();
        services.AddScoped<Snakk.Application.Repositories.IStatsRollupRepository, Snakk.Infrastructure.Database.Repositories.StatsRollupRepository>();
        services.AddScoped<Snakk.Application.Repositories.IDashboardChartRepository, Snakk.Infrastructure.Database.Repositories.DashboardChartRepository>();
        services.AddScoped<Snakk.Application.Repositories.IDisplayNameHistoryRepository, Snakk.Infrastructure.Database.Repositories.DisplayNameHistoryRepository>();
        services.AddScoped<Snakk.Application.Repositories.IPasswordResetTokenRepository, Snakk.Infrastructure.Database.Repositories.PasswordResetTokenRepository>();
        services.AddScoped<Snakk.Application.Repositories.IPasswordResetRequestRepository, Snakk.Infrastructure.Database.Repositories.PasswordResetRequestRepository>();
        services.AddScoped<Snakk.Application.Repositories.IDmRepository, Snakk.Infrastructure.Database.Repositories.DmRepository>();
        services.AddScoped<Snakk.Application.Repositories.IGdprRepository, Snakk.Infrastructure.Database.Repositories.GdprRepository>();

        return services;
    }
}
