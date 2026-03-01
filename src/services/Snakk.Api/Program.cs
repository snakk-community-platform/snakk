using Microsoft.AspNetCore.Server.Kestrel.Core;
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

// HTTP/2 only — required for gRPC over plaintext (h2c) in Docker
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
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

// Map gRPC services
app.MapGrpcService<Snakk.Api.GrpcServices.AuthGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.CommunityGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.HubGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.SpaceGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.DiscussionGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.PostGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.FollowGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.ReactionGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.NotificationGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.ModerationGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.SearchGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.StatisticsGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.UserGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.ReadStateGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.MarkupGrpcService>();

// Map REST endpoint groups (kept alongside gRPC during incremental migration)
app.MapCommunityEndpoints();
app.MapHubEndpoints();
app.MapSpaceEndpoints();
app.MapDiscussionEndpoints();
app.MapPostEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
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
app.MapPlatformEndpoints();
app.MapUserEndpoints();
app.MapMarkupEndpoints();
app.MapReadStateEndpoints();
app.MapSearchEndpoints();
app.MapModerationEndpoints();
app.MapManageContextEndpoints();
// Sitemap moved to Snakk.Web (public-facing app)

app.Run();
