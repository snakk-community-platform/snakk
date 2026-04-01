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

// Add SignalR with tuned limits
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
    options.StreamBufferCapacity = 20;
});

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
            ValidateIssuer = false,
            ValidateAudience = false,
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
var snakkApiBaseUrl = builder.Configuration["SnakkApi:BaseUrl"] ?? "https://localhost:17100";
var snakkApiKey = builder.Configuration["SnakkApi:ApiKey"] ?? string.Empty;

builder.Services.AddHttpClient<IAccessVerifier, HttpAccessVerifier>(client =>
{
    client.BaseAddress = new Uri(snakkApiBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", snakkApiKey);
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddStandardResilienceHandler();

// Configure forwarded headers for proxy scenarios
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add CORS for browser connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(';') ?? ["https://localhost:17000"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

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
