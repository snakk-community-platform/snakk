namespace Snakk.Api;

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Snakk.Infrastructure.Database;
using Snakk.Application.UseCases;
using Snakk.Application.Services;
using Snakk.Infrastructure.Services;
using Snakk.Api.Middleware;
using Snakk.Api.Services;
using Snakk.Api.Validators;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSnakkServices(this IServiceCollection services, IConfiguration configuration)
    {
        // CORS - Allow Web client and Admin panel to call API (configurable for production)
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var origins = configuration["Cors:AllowedOrigins"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? ["https://localhost:17110", "https://localhost:17112", "https://localhost:17111"];
                policy.WithOrigins(origins)
                      .WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With", "x-signalr-user-agent")
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                      .AllowCredentials();
            });
        });

        // Database (PostgreSQL) with DbContext pooling for better performance
        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(
            configuration.GetConnectionString("DbConnection"))
        {
            MaxPoolSize = 200,
            MinPoolSize = 5,
            Timeout = 30,
            ConnectionIdleLifetime = 300
        }.ToString();

        services.AddSingleton<Snakk.Api.Interceptors.SlowQueryInterceptor>();
        services.AddDbContextPool<SnakkDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    connectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                          .CommandTimeout(60))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .AddInterceptors(sp.GetRequiredService<Snakk.Api.Interceptors.SlowQueryInterceptor>()),
            poolSize: 128);

        // Factory for services that need per-operation contexts (e.g. CounterService parallel updates)
        services.AddPooledDbContextFactory<SnakkDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    connectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                          .CommandTimeout(60))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .AddInterceptors(sp.GetRequiredService<Snakk.Api.Interceptors.SlowQueryInterceptor>()));

        // Persist Data Protection keys so encrypted 2FA secrets and email addresses
        // survive container restarts. Shared key ring with Snakk.Auth/Web/Admin.
        var dataProtectionPath = Path.Combine(
            configuration["FileStorage:BasePath"] ?? "/app/storage",
            "dataprotection-keys");
        Directory.CreateDirectory(dataProtectionPath);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
            .SetApplicationName("Snakk");

        // HybridCache for settings/permissions caching (stampede-safe, also registers IMemoryCache)
        services.AddHybridCache();

        // JWT Service (IMemoryCache used for token revocation blacklist)
        services.AddMemoryCache();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Authentication
        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "Snakk",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "Snakk",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var jwtService = context.HttpContext.RequestServices
                        .GetRequiredService<IJwtTokenService>();
                    var jti = context.Principal?
                        .FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                    if (jti is not null && jwtService.IsRevoked(jti))
                        context.Fail("Token has been revoked");
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            // Policy that requires 2FA for admin users
            options.AddPolicy("RequireAdmin2FA", policy =>
                policy.Requirements.Add(new Authorization.Require2FARequirement()));
        });

        // Register authorization handlers
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Authorization.PermissionAuthorizationHandler>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Authorization.Require2FAAuthorizationHandler>();

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        // Database Repositories
        services.AddScoped<Infrastructure.Database.Repositories.ICommunityDatabaseRepository, Infrastructure.Database.Repositories.CommunityDatabaseRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IHubRepository, Infrastructure.Database.Repositories.HubRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.ISpaceRepository, Infrastructure.Database.Repositories.SpaceRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IDiscussionRepository, Infrastructure.Database.Repositories.DiscussionRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IPostRepository, Infrastructure.Database.Repositories.PostRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IUserRepository, Infrastructure.Database.Repositories.UserRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReactionDatabaseRepository, Infrastructure.Database.Repositories.ReactionDatabaseRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.INotificationDatabaseRepository, Infrastructure.Database.Repositories.NotificationDatabaseRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IFollowDatabaseRepository, Infrastructure.Database.Repositories.FollowDatabaseRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IMentionDatabaseRepository, Infrastructure.Database.Repositories.MentionDatabaseRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IBannerDatabaseRepository, Infrastructure.Database.Repositories.BannerDatabaseRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IAchievementRepository, Infrastructure.Database.Repositories.AchievementRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IUserAchievementRepository, Infrastructure.Database.Repositories.UserAchievementRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IUserAchievementProgressRepository, Infrastructure.Database.Repositories.UserAchievementProgressRepository>();

        // Moderation Repositories
        services.AddScoped<Infrastructure.Database.Repositories.IUserRoleRepository, Infrastructure.Database.Repositories.UserRoleRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IUserBanRepository, Infrastructure.Database.Repositories.UserBanRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReportRepository, Infrastructure.Database.Repositories.ReportRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReportCommentRepository, Infrastructure.Database.Repositories.ReportCommentRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReportReasonRepository, Infrastructure.Database.Repositories.ReportReasonRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IModerationLogRepository, Infrastructure.Database.Repositories.ModerationLogRepository>();

        // Refresh Token Repository
        services.AddScoped<Domain.Repositories.IRefreshTokenRepository, Infrastructure.Database.Repositories.RefreshTokenRepository>();

        // Domain Repository Adapters
        services.AddScoped<Domain.Repositories.ICommunityRepository, Infrastructure.Adapters.CommunityRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IHubRepository, Infrastructure.Adapters.HubRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.ISpaceRepository, Infrastructure.Adapters.SpaceRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IDiscussionRepository, Infrastructure.Adapters.DiscussionRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IPostRepository, Infrastructure.Adapters.PostRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IUserRepository, Infrastructure.Adapters.UserRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IDiscussionReadStateRepository, Infrastructure.Adapters.DiscussionReadStateRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IReactionRepository, Infrastructure.Adapters.ReactionRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.INotificationRepository, Infrastructure.Adapters.NotificationRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IFollowRepository, Infrastructure.Adapters.FollowRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IMentionRepository, Infrastructure.Adapters.MentionRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IBannerRepository, Infrastructure.Adapters.BannerRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IAchievementRepository, Infrastructure.Adapters.AchievementRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IUserAchievementRepository, Infrastructure.Adapters.UserAchievementRepositoryAdapter>();
        services.AddScoped<Domain.Repositories.IUserAchievementProgressRepository, Infrastructure.Adapters.UserAchievementProgressRepositoryAdapter>();

        // Search Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.ISearchRepository, Infrastructure.Database.Repositories.SearchRepository>();

        // Save Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.ISaveRepository, Infrastructure.Database.Repositories.SaveRepository>();

        // Social Link Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.IUserSocialLinkRepository, Infrastructure.Database.Repositories.UserSocialLinkRepository>();

        // Reaction Query Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.IReactionQueryRepository, Infrastructure.Database.Repositories.ReactionQueryRepository>();

        // Moderation Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.IModerationRepository, Infrastructure.Database.Repositories.ModerationRepository>();

        // Stats Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.IStatsRepository, Infrastructure.Database.Repositories.StatsRepository>();

        // Activity snapshot repository
        services.AddScoped<Application.Repositories.IActivitySnapshotRepository, Infrastructure.Database.Repositories.ActivitySnapshotRepository>();

        // Dashboard Chart Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.IDashboardChartRepository, Infrastructure.Database.Repositories.DashboardChartRepository>();

        // Display Name History Repository
        services.AddScoped<Application.Repositories.IDisplayNameHistoryRepository, Infrastructure.Database.Repositories.DisplayNameHistoryRepository>();

        // Password Reset Repositories
        services.AddScoped<Application.Repositories.IPasswordResetTokenRepository, Infrastructure.Database.Repositories.PasswordResetTokenRepository>();
        services.AddScoped<Application.Repositories.IPasswordResetRequestRepository, Infrastructure.Database.Repositories.PasswordResetRequestRepository>();

        services.AddScoped<Application.Services.DisplayNameValidator>();

        // Use Cases
        services.AddScoped<CommunityUseCase>();
        services.AddScoped<HubUseCase>();
        services.AddScoped<SpaceUseCase>();
        services.AddScoped<DiscussionUseCase>();
        services.AddScoped<PostUseCase>();
        services.AddScoped<AuthenticationUseCase>();
        services.AddScoped<ReactionUseCase>();
        services.AddScoped<NotificationUseCase>();
        services.AddScoped<FollowUseCase>();
        services.AddScoped<SearchUseCase>();
        services.AddScoped<UserProfileUseCase>();
        services.AddScoped<ModerationUseCase>();
        services.AddScoped<BannerUseCase>();
        services.AddScoped<StatisticsUseCase>();

        // API Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<Application.Services.IUserGrantsCacheService, Infrastructure.Services.UserGrantsCacheService>();
        services.AddScoped<Application.Services.IEntityHierarchyCacheService, Infrastructure.Services.EntityHierarchyCacheService>();
        services.AddScoped<IViewRenderingService, ViewRenderingService>();

        // Services
        services.AddScoped<Application.Services.MentionService>();
        services.AddScoped<Application.Services.AchievementService>();
        services.AddScoped<Infrastructure.Services.MetricsService>();
        // File storage: S3-compatible (R2, AWS, MinIO) or local filesystem
        var storageProvider = configuration["FileStorage:Provider"];
        if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IFileStorage, Infrastructure.Services.S3FileStorage>();
        else
            services.AddSingleton<IFileStorage, Infrastructure.Services.LocalFileStorage>();

        services.AddScoped<IMediaService, Infrastructure.Services.MediaService>();
        services.AddScoped<IAvatarGenerationService, Infrastructure.Services.AvatarGenerationService>();
        services.AddScoped<IAllowedTypesService, Infrastructure.Services.AllowedTypesService>();
        services.AddTransient<PrivateIpBlockHandler>();
        services.AddHttpClient("LinkMetadata")
            .AddHttpMessageHandler<PrivateIpBlockHandler>();
        // Unblocked client for calls to the operator-configured proxy URL (internal Docker service)
        services.AddHttpClient("LinkMetadataProxy");
        services.AddHttpClient("DiscordWebhook", client => { client.Timeout = TimeSpan.FromSeconds(5); });
        services.AddScoped<Application.Services.IDiscordNotificationService,
            Infrastructure.Services.DiscordNotificationService>();
        services.AddScoped<ILinkMetadataService, Infrastructure.Services.LinkMetadataService>();
        services.AddScoped<IDiscussionExtensionService, Infrastructure.Services.DiscussionExtensionService>();
        services.AddScoped<IPollService, Infrastructure.Services.PollService>();
        services.AddScoped<IDiscussionTypeQueryService, Infrastructure.Services.DiscussionTypeQueryService>();

        // 2FA & Security Services
        services.AddScoped<Application.Services.ITotpService, Infrastructure.Services.TotpService>();
        services.AddScoped<Application.Services.ITokenService, Infrastructure.Services.TokenService>();
        services.AddScoped<Application.Services.ITrustedDeviceService, Infrastructure.Services.TrustedDeviceService>();

        // Achievement Event Handlers
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.PostCreatedEvent>,
            Infrastructure.EventHandlers.Achievements.PostCreatedAchievementHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.DiscussionCreatedEvent>,
            Infrastructure.EventHandlers.Achievements.DiscussionCreatedAchievementHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.ReactionAddedEvent>,
            Infrastructure.EventHandlers.Achievements.ReactionAddedAchievementHandler>();

        // Trending Score Event Handlers
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.PostCreatedEvent>,
            Infrastructure.EventHandlers.Trending.PostCreatedTrendingHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.ReactionAddedEvent>,
            Infrastructure.EventHandlers.Trending.ReactionAddedTrendingHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.ReactionRemovedEvent>,
            Infrastructure.EventHandlers.Trending.ReactionRemovedTrendingHandler>();

        // Avatar Event Handlers
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.UserCreatedEvent>,
            Infrastructure.EventHandlers.Avatars.UserCreatedAvatarGenerationHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.UserDeletedEvent>,
            Infrastructure.EventHandlers.Avatars.UserDeletedAvatarCleanupHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.HubCreatedEvent>,
            Infrastructure.EventHandlers.Avatars.HubCreatedAvatarGenerationHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.HubDeletedEvent>,
            Infrastructure.EventHandlers.Avatars.HubDeletedAvatarCleanupHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.SpaceCreatedEvent>,
            Infrastructure.EventHandlers.Avatars.SpaceCreatedAvatarGenerationHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.SpaceDeletedEvent>,
            Infrastructure.EventHandlers.Avatars.SpaceDeletedAvatarCleanupHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.CommunityCreatedEvent>,
            Infrastructure.EventHandlers.Avatars.CommunityCreatedAvatarGenerationHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.CommunityDeletedEvent>,
            Infrastructure.EventHandlers.Avatars.CommunityDeletedAvatarCleanupHandler>();

        // Activity Broadcast Event Handlers (for live admin feed)
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.PostCreatedEvent>,
            Infrastructure.EventHandlers.Activity.PostCreatedActivityHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.DiscussionCreatedEvent>,
            Infrastructure.EventHandlers.Activity.DiscussionCreatedActivityHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.ReactionAddedEvent>,
            Infrastructure.EventHandlers.Activity.ReactionAddedActivityHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.FollowCreatedEvent>,
            Infrastructure.EventHandlers.Activity.FollowCreatedActivityHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.UserCreatedEvent>,
            Infrastructure.EventHandlers.Activity.UserCreatedActivityHandler>();

        // Discord Webhook Notification Event Handlers
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.DiscussionCreatedEvent>,
            Infrastructure.EventHandlers.Discord.DiscussionCreatedDiscordHandler>();
        services.AddScoped<Application.Events.IDomainEventHandler<Domain.Events.PostCreatedEvent>,
            Infrastructure.EventHandlers.Discord.PostCreatedDiscordHandler>();

        // Services
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IPasswordHasher, Infrastructure.Services.BCryptPasswordHasher>();
        services.AddScoped<IEmailSender, Infrastructure.Services.ConsoleEmailSender>();
        services.AddScoped<ICounterService, Infrastructure.Services.CounterService>();

        // Rendering
        services.AddSingleton<IMarkupParser, MarkupParser>();
        services.AddSingleton<IContentNormalizer, ContentNormalizer>();

        // Realtime Services
        services.AddScoped<Infrastructure.Services.IPostHtmlRenderer, Infrastructure.Services.PostHtmlRenderer>();

        // Realtime broadcaster - HTTP only (posts to dedicated Snakk.Realtime microservice)
        services.AddHttpClient("RealtimeService", client =>
        {
            client.BaseAddress = new Uri(
                configuration["Realtime:BaseUrl"] ?? "https://localhost:17103");
            client.Timeout = TimeSpan.FromSeconds(5);

            var apiKey = configuration["Realtime:ApiKey"];

            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        })
        .AddStandardResilienceHandler();

        services.AddScoped<IRealtimeNotifier, Infrastructure.Realtime.HttpRealtimeNotifier>();

        // Security & Audit Services
        services.AddScoped<Application.Services.ISecurityService, Infrastructure.Services.SecurityService>();
        services.AddScoped<Application.Services.ISettingsService, Infrastructure.Services.SettingsService>();
        services.AddScoped<Application.Services.IPermissionService, Infrastructure.Services.PermissionService>();
        services.AddScoped<Application.Services.IManagePermissionService, Infrastructure.Services.ManagePermissionService>();
        services.AddScoped<Application.Services.IAuthorizationService, Infrastructure.Services.AuthorizationService>();

        // Session Management
        services.AddScoped<Application.Services.ISessionManagementService, Infrastructure.Services.SessionManagementService>();

        // Email encryption (at-rest protection for email addresses in the database)
        services.AddSingleton<Application.Services.IEmailProtector, Infrastructure.Services.EmailProtector>();

        // Two-Factor Authentication
        services.AddSingleton<Application.Services.ITwoFactorSecretProtector, Infrastructure.Services.TwoFactorSecretProtector>();
        services.AddScoped<Application.Services.ITwoFactorAuthService, Infrastructure.Services.TwoFactorAuthService>();
        services.AddScoped<Application.Services.ITurnstileService, Infrastructure.Services.TurnstileService>();
        services.AddScoped<Application.Services.IConsentService, Infrastructure.Services.ConsentService>();

        // Admin Services
        services.AddScoped<Application.Services.IAdminUserService, Infrastructure.Services.AdminUserService>();
        services.AddScoped<Application.Services.IAdminContentService, Infrastructure.Services.AdminContentService>();

        // Management Services
        services.AddScoped<Application.Services.ICommunityManagementService, Infrastructure.Services.CommunityManagementService>();
        services.AddScoped<Application.Services.IHubManagementService, Infrastructure.Services.HubManagementService>();
        services.AddScoped<Application.Services.ISpaceManagementService, Infrastructure.Services.SpaceManagementService>();
        services.AddScoped<Application.Services.IRuleService, Infrastructure.Services.RuleService>();
        services.AddScoped<Application.Services.IGroupService, Infrastructure.Services.GroupService>();
        services.AddScoped<Application.Services.IGroupAccessService, Infrastructure.Services.GroupAccessService>();

        // Webhook Services
        services.AddHttpClient("WebhookService", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler();
        services.AddScoped<Application.Services.IWebhookService, Infrastructure.Services.WebhookService>();

        // Activity Broadcaster (for admin panel activity feed)
        // Uses HTTP to post to Snakk.Realtime microservice
        services.AddHttpClient("ActivityBroadcaster", client =>
        {
            client.BaseAddress = new Uri(
                configuration["Realtime:BaseUrl"] ?? "https://localhost:17103");
            client.Timeout = TimeSpan.FromSeconds(5);

            var apiKey = configuration["Realtime:ApiKey"];

            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        })
        .AddStandardResilienceHandler();

        services.AddSingleton<Application.Services.IActivityBroadcaster, Infrastructure.Realtime.HttpActivityBroadcaster>();

        return services;
    }

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Strict rate limit for authentication endpoints — partitioned per client IP
            options.AddPolicy("auth", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetRateLimitPartitionKey(ctx),
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Standard rate limit for API endpoints — partitioned per client IP
            options.AddPolicy("api", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetRateLimitPartitionKey(ctx),
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Stricter limit for expensive operations — partitioned per client IP
            options.AddPolicy("expensive", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetRateLimitPartitionKey(ctx),
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests;

                double? retryAfterSeconds = null;

                if (context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = retryAfter.TotalSeconds;
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests. Please try again later.",
                    retryAfter = retryAfterSeconds
                }, cancellationToken);
            };
        });

        return services;
    }

    // Reads the real client IP from X-Forwarded-For (set by trusted internal callers)
    // with fallback to the raw socket address and finally "unknown".
    private static string GetRateLimitPartitionKey(HttpContext ctx) =>
        ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
