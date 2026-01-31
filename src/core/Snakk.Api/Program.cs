using Microsoft.EntityFrameworkCore;
using Snakk.Api;
using Snakk.Api.Endpoints;
using Snakk.Api.Services;
using Snakk.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddSnakkServices(builder.Configuration);
builder.Services.AddRateLimiting();

// Database Seeder
builder.Services.AddScoped<DatabaseSeeder>();

var app = builder.Build();

// Seed the database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health");

// Map all endpoint groups
app.MapCommunityEndpoints();
app.MapHubEndpoints();
app.MapSpaceEndpoints();
app.MapDiscussionEndpoints();
app.MapPostEndpoints();
app.MapAuthEndpoints();
app.MapAvatarEndpoints();
app.MapReactionEndpoints();
app.MapNotificationEndpoints();
app.MapFollowEndpoints();
app.MapStatsEndpoints();
app.MapUserEndpoints();
app.MapMarkupEndpoints();
app.MapReadStateEndpoints();
app.MapSearchEndpoints();
app.MapModerationEndpoints();
app.MapSitemapEndpoints();

// SignalR hub endpoint
app.MapHub<Snakk.Infrastructure.Hubs.SnakkHub>("/realtime");

app.Run();
