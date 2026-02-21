using Microsoft.EntityFrameworkCore;
using Snakk.Api;
using Snakk.Api.Endpoints;
using Snakk.Api.Middleware;
using Snakk.Api.Services;
using Snakk.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddSnakkServices(builder.Configuration);
builder.Services.AddRateLimiting();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SnakkDbContext>();


var app = builder.Build();


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Security headers - must come early in pipeline
app.UseSecurityHeaders();

app.UseCors();

app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<Snakk.Api.Middleware.TokenRefreshMiddleware>();
app.UseAuthorization();

// Health check endpoint (checks DB connectivity)
app.MapHealthChecks("/health");

// Security.txt endpoint (RFC 9116)
app.MapGet("/.well-known/security.txt", () =>
{
    var securityTxt = @"Contact: mailto:security@snakk.local
Expires: 2027-12-31T23:59:59.000Z
Preferred-Languages: en
Canonical: https://snakk.local/.well-known/security.txt
Policy: https://snakk.local/security-policy
Acknowledgments: https://snakk.local/security-thanks";

    return Results.Text(securityTxt, "text/plain; charset=utf-8");
})
.WithName("SecurityTxt")
.ExcludeFromDescription();

// Map all endpoint groups
app.MapCommunityEndpoints();
app.MapHubEndpoints();
app.MapSpaceEndpoints();
app.MapDiscussionEndpoints();
app.MapPostEndpoints();
app.MapAuthEndpoints();
app.MapTwoFactorAuthEndpoints();
app.MapSessionManagementEndpoints();
app.MapAdminModerationEndpoints();
app.MapAdminContentEndpoints();
app.MapAdminSecurityEndpoints();
app.MapAdminSettingsEndpoints();
app.MapAdminPermissionsEndpoints();
app.MapAdminWebhooksEndpoints();
app.MapCommunityManagementEndpoints();
app.MapHubManagementEndpoints();
app.MapSpaceManagementEndpoints();
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
app.MapManageContextEndpoints();
app.MapSitemapEndpoints();

app.Run();
