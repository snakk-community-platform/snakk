namespace Snakk.Api;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Application.UseCases;
using Snakk.Application.Services;
using Snakk.Infrastructure.Services;
using Snakk.Infrastructure.Rendering;
using Snakk.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSnakkServices(this IServiceCollection services, IConfiguration configuration)
    {
        // CORS - Allow Web client to call API
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("https://localhost:7001", "http://localhost:5001")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // Database (PostgreSQL) with DbContext pooling for better performance
        services.AddDbContextPool<SnakkDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DbConnection")),
            poolSize: 128); // Default is 1024, using 128 for typical web app

        // Authentication
        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Cookies";
            options.DefaultChallengeScheme = "Cookies";
        })
        .AddCookie("Cookies", options =>
        {
            options.LoginPath = "/auth/login";
            options.LogoutPath = "/auth/logout";
            options.AccessDeniedPath = "/auth/access-denied";
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            // Set domain to share cookie between API and Web client on localhost
            // This allows the web server to forward the auth cookie to the API
            options.Cookie.Domain = "localhost";
        });

        // Conditionally add OAuth providers only if they have valid credentials

        var googleClientId = configuration["Authentication:Google:ClientId"];
        var googleClientSecret = configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret) &&
            !googleClientId.StartsWith("your-") && !googleClientSecret.StartsWith("your-"))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.SignInScheme = "Cookies";
            });
        }

        var microsoftClientId = configuration["Authentication:Microsoft:ClientId"];
        var microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret"];
        if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret) &&
            !microsoftClientId.StartsWith("your-") && !microsoftClientSecret.StartsWith("your-"))
        {
            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = microsoftClientId;
                options.ClientSecret = microsoftClientSecret;
                options.SignInScheme = "Cookies";
            });
        }

        var facebookAppId = configuration["Authentication:Facebook:AppId"];
        var facebookAppSecret = configuration["Authentication:Facebook:AppSecret"];
        if (!string.IsNullOrEmpty(facebookAppId) && !string.IsNullOrEmpty(facebookAppSecret) &&
            !facebookAppId.StartsWith("your-") && !facebookAppSecret.StartsWith("your-"))
        {
            authBuilder.AddFacebook(options =>
            {
                options.AppId = facebookAppId;
                options.AppSecret = facebookAppSecret;
                options.SignInScheme = "Cookies";
            });
        }

        var githubClientId = configuration["Authentication:GitHub:ClientId"];
        var githubClientSecret = configuration["Authentication:GitHub:ClientSecret"];
        if (!string.IsNullOrEmpty(githubClientId) && !string.IsNullOrEmpty(githubClientSecret) &&
            !githubClientId.StartsWith("your-") && !githubClientSecret.StartsWith("your-"))
        {
            authBuilder.AddGitHub(options =>
            {
                options.ClientId = githubClientId;
                options.ClientSecret = githubClientSecret;
                options.SignInScheme = "Cookies";
            });
        }

        var discordClientId = configuration["Authentication:Discord:ClientId"];
        var discordClientSecret = configuration["Authentication:Discord:ClientSecret"];
        if (!string.IsNullOrEmpty(discordClientId) && !string.IsNullOrEmpty(discordClientSecret) &&
            !discordClientId.StartsWith("your-") && !discordClientSecret.StartsWith("your-"))
        {
            authBuilder.AddDiscord(options =>
            {
                options.ClientId = discordClientId;
                options.ClientSecret = discordClientSecret;
                options.SignInScheme = "Cookies";
            });
        }

        var appleClientId = configuration["Authentication:Apple:ClientId"];
        var appleTeamId = configuration["Authentication:Apple:TeamId"];
        var appleKeyId = configuration["Authentication:Apple:KeyId"];
        if (!string.IsNullOrEmpty(appleClientId) && !string.IsNullOrEmpty(appleTeamId) && !string.IsNullOrEmpty(appleKeyId) &&
            !appleClientId.StartsWith("your-") && !appleTeamId.StartsWith("your-") && !appleKeyId.StartsWith("your-"))
        {
            authBuilder.AddApple(options =>
            {
                options.ClientId = appleClientId;
                options.TeamId = appleTeamId;
                options.KeyId = appleKeyId;
                options.SignInScheme = "Cookies";
            });
        }

        services.AddAuthorization();

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

        // Moderation Repositories
        services.AddScoped<Infrastructure.Database.Repositories.IUserRoleRepository, Infrastructure.Database.Repositories.UserRoleRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IUserBanRepository, Infrastructure.Database.Repositories.UserBanRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReportRepository, Infrastructure.Database.Repositories.ReportRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReportCommentRepository, Infrastructure.Database.Repositories.ReportCommentRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IReportReasonRepository, Infrastructure.Database.Repositories.ReportReasonRepository>();
        services.AddScoped<Infrastructure.Database.Repositories.IModerationLogRepository, Infrastructure.Database.Repositories.ModerationLogRepository>();

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

        // Search Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.ISearchRepository, Infrastructure.Database.Repositories.SearchRepository>();

        // Moderation Repository (Application layer interface, Infrastructure implementation)
        services.AddScoped<Application.Repositories.IModerationRepository, Infrastructure.Database.Repositories.ModerationRepository>();

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
        services.AddScoped<StatisticsUseCase>();

        // API Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IViewRenderingService, ViewRenderingService>();

        // Services
        services.AddScoped<Application.Services.MentionService>();

        // Services
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IPasswordHasher, Infrastructure.Services.BCryptPasswordHasher>();
        services.AddScoped<IEmailSender, Infrastructure.Services.ConsoleEmailSender>();
        services.AddScoped<ICounterService, Infrastructure.Services.CounterService>();

        // SignalR
        services.AddSignalR();

        // Rendering
        services.AddSingleton<IMarkupParser, MarkupParser>();

        // Realtime Services
        services.AddScoped<Infrastructure.Services.IPostHtmlRenderer, Infrastructure.Services.PostHtmlRenderer>();
        services.AddScoped<IRealtimeNotifier, Infrastructure.Services.SignalRRealtimeNotifier>();

        return services;
    }
}
