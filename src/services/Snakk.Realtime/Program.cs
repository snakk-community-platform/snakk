using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Snakk.Realtime;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Middleware;
using Snakk.Realtime.Services;
using Snakk.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Load shared config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "conf", "snakk-config.json"), optional: true, reloadOnChange: true);

//builder.AddSnakkDefaults();
builder.AddSnakkObservability();

// Add SignalR with tuned limits
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
    options.StreamBufferCapacity = 20;
});

// Fail-fast: reject placeholder secrets in production
if (!builder.Environment.IsDevelopment() && builder.Environment.EnvironmentName != "Testing")
{
    var jwtKey = builder.Configuration["Realtime:JwtKey"];
    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Contains("CHANGE_IN_PRODUCTION", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SECURITY: Realtime:JwtKey must be overridden in production.");

    var apiKey = builder.Configuration["Realtime:ApiKey"];
    if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("CHANGE_IN_PRODUCTION", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SECURITY: Realtime:ApiKey must be overridden in production.");
}

// JWT auth for browser WebSocket connections
var realtimeJwtKey = builder.Configuration["Realtime:JwtKey"]
    ?? throw new InvalidOperationException("Realtime:JwtKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(realtimeJwtKey)),
            ValidateIssuer = true,
            ValidIssuer = "Snakk",
            ValidateAudience = true,
            ValidAudience = "Snakk-Realtime",
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // SignalR sends the token as ?access_token= query param on WebSocket upgrade
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/realtime"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Background service to clean up stale viewer count entries
builder.Services.AddHostedService<ViewerCountCleanupService>();

// HTTP client to Snakk.Api for subscription access verification
// Uses Realtime:ApiKey — the same shared key that Snakk.Api validates on its internal endpoints
var snakkApiBaseUrl = builder.Configuration["SnakkApi:BaseUrl"] ?? "https://localhost:17101";
var snakkApiKey = builder.Configuration["Realtime:ApiKey"] ?? string.Empty;

builder.Services.AddHttpClient<IAccessVerifier, HttpAccessVerifier>(client =>
{
    client.BaseAddress = new Uri(snakkApiBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", snakkApiKey);
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddStandardResilienceHandler();

// Configure forwarded headers — trust loopback (YARP on same host) and private networks
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});

// Add CORS for browser connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // In dev, snakk-config.json may set a bare origin (e.g. https://localhost) that
            // doesn't match the gateway port (17100), so allow any origin to avoid 403 on upgrade.
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(
                    builder.Configuration["Cors:AllowedOrigins"]?.Split(';') ?? ["https://localhost:17100"])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();
app.MapDefaultEndpoints();

//app.UseSerilogRequestLogging();

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

// API Key authentication for internal service calls (broadcast endpoints)
app.UseApiKeyAuth();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// SignalR hub for browser WebSocket connections (requires JWT auth)
app.MapHub<RealtimeHub>("/realtime");

// HTTP API for internal services to broadcast events (protected by API key)
app.MapPost("/api/broadcast", BroadcastEndpoints.BroadcastEvent);
app.MapPost("/api/broadcast/activity", BroadcastEndpoints.BroadcastActivity);

// Health check for gateway probes
app.MapGet("/health", () => Results.Ok(new { status = "healthy", connections = Snakk.Realtime.Hubs.RealtimeHub.ActiveConnectionCount }));

app.Run();
