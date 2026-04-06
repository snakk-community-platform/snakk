using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Snakk.Api;
using Snakk.Api.Endpoints;
using Snakk.Api.Middleware;
using Snakk.Api.Services;
using Snakk.Infrastructure.Database;
using Serilog;
using Snakk.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Load shared config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "conf", "snakk-config.json"), optional: true, reloadOnChange: true);

//builder.AddSnakkDefaults();

// Kestrel tuning — gRPC requires HTTP/2, REST endpoints work over either
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxConcurrentConnections = 5_000;
    kestrel.Limits.MaxConcurrentUpgradedConnections = 2_500;
    kestrel.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    kestrel.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    kestrel.Limits.Http2.MaxStreamsPerConnection = 100;
    kestrel.Limits.Http2.InitialConnectionWindowSize = 1024 * 1024;     // 1 MB
    kestrel.Limits.Http2.InitialStreamWindowSize = 768 * 1024;          // 768 KB

    // Docker (plain HTTP): gRPC needs HTTP/2 (h2c), REST proxy needs HTTP/1.1.
    // When RestPort is set, bind both ports explicitly with correct protocols.
    // When not set (dev/Aspire with HTTPS), use default with ALPN negotiation.
    var restPort = builder.Configuration["RestPort"];
    if (!string.IsNullOrEmpty(restPort) && int.TryParse(restPort, out var rp))
    {
        var mainUrl = builder.Configuration["ASPNETCORE_URLS"] ?? "http://127.0.0.1:5242";
        var mainUri = new Uri(mainUrl);

        // gRPC port — HTTP/2 only (h2c for cleartext)
        kestrel.Listen(System.Net.IPAddress.Parse(mainUri.Host), mainUri.Port, lo =>
            lo.Protocols = HttpProtocols.Http2);

        // REST port — HTTP/1.1 for file uploads and other proxy calls
        kestrel.Listen(System.Net.IPAddress.Loopback, rp, lo =>
            lo.Protocols = HttpProtocols.Http1);
    }
    else
    {
        kestrel.ConfigureEndpointDefaults(lo =>
            lo.Protocols = HttpProtocols.Http1AndHttp2);
    }
});

// Fail-fast: reject insecure default secrets in production
if (!builder.Environment.IsDevelopment() && builder.Environment.EnvironmentName != "Testing")
{
    var jwtKey = builder.Configuration["Jwt:SecretKey"];

    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Contains("change-in-production", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SECURITY: Jwt:SecretKey must be overridden in production. Set it in snakk-config.json.");

    var realtimeKey = builder.Configuration["Realtime:ApiKey"];

    if (string.IsNullOrEmpty(realtimeKey) || realtimeKey.Contains("CHANGE_IN_PRODUCTION", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SECURITY: Realtime:ApiKey must be overridden in production. Set it in snakk-config.json.");
}

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddSnakkServices(builder.Configuration);
// Rate limiting is handled at the gateway level — API only receives trusted internal requests
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SnakkDbContext>();

var app = builder.Build();

//app.UseSerilogRequestLogging();

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
app.MapGrpcService<Snakk.Api.GrpcServices.BannerGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.ManageGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.TwoFactorGrpcService>();
app.MapGrpcService<Snakk.Api.GrpcServices.ConsentGrpcService>();

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
app.MapGroupEndpoints();
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
app.MapBannerManagementEndpoints();
app.MapMediaEndpoints();
app.MapRealtimeInternalEndpoints();
// Sitemap moved to Snakk.Web (public-facing app)

app.Run();
