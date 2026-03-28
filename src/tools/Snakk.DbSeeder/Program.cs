using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Snakk.Application.Services;
using Snakk.DbSeeder.Services;
using Snakk.Domain.Repositories;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Services;

Console.WriteLine("====================================");
Console.WriteLine("   Snakk Database Seeder Tool");
Console.WriteLine("====================================\n");

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

// Show environment info
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Working Directory: {Directory.GetCurrentDirectory()}\n");

// Register DbContext
var connectionString = builder.Configuration.GetConnectionString("DbConnection")
    ?? throw new InvalidOperationException("Database connection string not found");

// Mask password for display
var displayConnectionString = connectionString.Contains("Password=")
    ? System.Text.RegularExpressions.Regex.Replace(connectionString, @"Password=[^;]+", "Password=***")
    : connectionString;
Console.WriteLine($"Connection String: {displayConnectionString}\n");

builder.Services.AddDbContext<SnakkDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IUserGrantsCacheService, UserGrantsCacheService>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// Register repositories (Database layer)
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IUserRepository, UserRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.ICommunityDatabaseRepository, CommunityDatabaseRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.IHubRepository, HubRepository>();
builder.Services.AddScoped<Snakk.Infrastructure.Database.Repositories.ISpaceRepository, SpaceRepository>();

// Register repositories (Domain layer adapters)
builder.Services.AddScoped<Snakk.Domain.Repositories.IUserRepository, UserRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.ICommunityRepository, CommunityRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.IHubRepository, HubRepositoryAdapter>();
builder.Services.AddScoped<Snakk.Domain.Repositories.ISpaceRepository, SpaceRepositoryAdapter>();

builder.Services.AddDataProtection();
builder.Services.AddScoped<IEmailProtector, EmailProtector>();
builder.Services.AddScoped<IAvatarGenerationService, AvatarGenerationService>();
builder.Services.AddSingleton<IMarkupParser, MarkupParser>();
builder.Services.AddScoped<DatabaseSeeder>();

var host = builder.Build();

// Parse CLI flags
var skipSeed = args.Contains("--skip-seed");
if (skipSeed)
    Console.WriteLine("Flag: --skip-seed (migrations + admin only, no test data)\n");

// Run seeder
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        Console.WriteLine("Connecting to database...");
        var context = services.GetRequiredService<SnakkDbContext>();

        Console.WriteLine("Applying pending migrations...");
        await context.Database.MigrateAsync();
        Console.WriteLine("✓ Migrations applied successfully.\n");

        var seeder = services.GetRequiredService<DatabaseSeeder>();

        if (skipSeed)
        {
            Console.WriteLine("Creating admin user (skipping test data)...\n");
            await seeder.SetupOnlyAsync();
        }
        else
        {
            Console.WriteLine("Starting database seeding...\n");
            await seeder.SeedAsync();
        }

        Console.WriteLine("\n====================================");
        Console.WriteLine("   Completed successfully!");
        Console.WriteLine("====================================\n");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n❌ Error during seeding: {ex.Message}");
        Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        Console.ResetColor();
        Environment.Exit(1);
    }
}
