using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Snakk.Worker.Workers;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Services;
using Snakk.Application.Services;
using Snakk.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

// Load shared config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "conf", "snakk-config.json"), optional: true, reloadOnChange: true);

// Fail-fast: reject placeholder secrets in production
if (!builder.Environment.IsDevelopment() && builder.Environment.EnvironmentName != "Testing")
{
    var realtimeApiKey = builder.Configuration["Realtime:ApiKey"];
    if (string.IsNullOrEmpty(realtimeApiKey) || realtimeApiKey.Contains("change-in-production", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SECURITY: Realtime:ApiKey must be overridden in production. Set it in snakk-config.json.");
}

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

// Distributed cache: Valkey on production, in-memory fallback for development
var valkeyConn = builder.Configuration["Valkey:ConnectionString"];
if (!string.IsNullOrEmpty(valkeyConn))
    builder.Services.AddStackExchangeRedisCache(opts => { opts.Configuration = valkeyConn; opts.InstanceName = "snakk:"; });
else
    builder.Services.AddDistributedMemoryCache();

// HybridCache uses IDistributedCache above as L2 backing store
builder.Services.AddHybridCache();

// File Storage for avatars
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// Persist Data Protection keys in Postgres — shared across all services,
// durable as app data, and read at most once per 24 h (cached in memory).
builder.Services.AddDbContext<DataProtectionDbContext>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("DbConnection")
        ?? throw new InvalidOperationException("DbConnection not configured")));

builder.Services.AddDataProtection()
    .SetApplicationName("Snakk")
    .PersistKeysToDbContext<DataProtectionDbContext>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IEmailProtector, EmailProtector>();
builder.Services.AddScoped<IUserGrantsCacheService, UserGrantsCacheService>();
builder.Services.AddScoped<IAvatarGenerationService, AvatarGenerationService>();
builder.Services.AddScoped<IMediaService, MediaService>();
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

// Activity snapshot repository
builder.Services.AddScoped<Snakk.Application.Repositories.IActivitySnapshotRepository, Snakk.Infrastructure.Database.Repositories.ActivitySnapshotRepository>();

// Background workers
// AchievementCheckerWorker disabled — will be rewritten as event-driven
// builder.Services.AddHostedService<AchievementCheckerWorker>();
builder.Services.AddHostedService<TemporaryRoleExpirationWorker>();
// builder.Services.AddHostedService<WebhookRetryWorker>(); // not yet implemented
builder.Services.AddHostedService<AvatarGenerationHostedService>();
builder.Services.AddHostedService<OrphanMediaCleanupWorker>();
builder.Services.AddHostedService<ActivitySnapshotWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dpDb = scope.ServiceProvider.GetRequiredService<DataProtectionDbContext>();
    await dpDb.EnsureSchemaAsync();
}

host.Run();
