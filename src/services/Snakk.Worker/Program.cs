using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Snakk.Worker.Workers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Services;
using Snakk.Application.Services;
using Snakk.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

//builder.AddSnakkDefaults();

// Database (PostgreSQL) with DbContext pooling for better performance
var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DbConnection"))
{
    MaxPoolSize = 50,
    MinPoolSize = 2,
    Timeout = 30,
    ConnectionIdleLifetime = 300
}.ToString();

builder.Services.AddDbContextPool<SnakkDbContext>(options =>
    options
        .UseNpgsql(connectionString,
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution),
    poolSize: 32);

// HybridCache for settings and permissions caching (stampede-safe, also registers IMemoryCache)
builder.Services.AddHybridCache();

// File Storage for avatars
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// Services needed by workers
builder.Services.AddScoped<IAvatarGenerationService, AvatarGenerationService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<MetricsService>();

// Repositories needed by workers
builder.Services.AddScoped<Snakk.Domain.Repositories.IAchievementRepository, Snakk.Infrastructure.Adapters.AchievementRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.IUserAchievementRepository, Snakk.Infrastructure.Adapters.UserAchievementRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.IUserAchievementProgressRepository, Snakk.Infrastructure.Adapters.UserAchievementProgressRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.IHubRepository, Snakk.Infrastructure.Adapters.HubRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.ISpaceRepository, Snakk.Infrastructure.Adapters.SpaceRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.ICommunityRepository, Snakk.Infrastructure.Adapters.CommunityRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IAchievementRepository, Snakk.Infrastructure.Database.Repositories.AchievementRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserAchievementRepository, Snakk.Infrastructure.Database.Repositories.UserAchievementRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserAchievementProgressRepository, Snakk.Infrastructure.Database.Repositories.UserAchievementProgressRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IHubRepository, Snakk.Infrastructure.Database.Repositories.HubRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.ISpaceRepository, Snakk.Infrastructure.Database.Repositories.SpaceRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.ICommunityDatabaseRepository, Snakk.Infrastructure.Database.Repositories.CommunityDatabaseRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserRepository, Snakk.Infrastructure.Database.Repositories.UserRepository>();
builder.Services.AddScoped<Snakk.Domain.Repositories.IUserRepository, Snakk.Infrastructure.Adapters.UserRepositoryAdapter>();

// HttpClient for webhook service
builder.Services.AddHttpClient("WebhookService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler();

// Background workers
builder.Services.AddHostedService<AchievementCheckerWorker>();
builder.Services.AddHostedService<TemporaryRoleExpirationWorker>();
builder.Services.AddHostedService<WebhookRetryWorker>();
builder.Services.AddHostedService<AvatarGenerationHostedService>();
builder.Services.AddHostedService<OrphanMediaCleanupWorker>();

var host = builder.Build();
host.Run();
