using Microsoft.AspNetCore.HttpOverrides;
using Snakk.Realtime;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

// Add SignalR
builder.Services.AddSignalR();

// Configure forwarded headers for proxy scenarios
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add CORS for browser connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? ["https://localhost:17000"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

// Use path base when running behind gateway
if (app.Environment.IsDevelopment())
{
    app.UsePathBase("/realtime");
}

// API Key authentication for internal service calls
app.UseApiKeyAuth();

app.UseCors();

// SignalR hub for browser WebSocket connections
app.MapHub<RealtimeHub>("/realtime");

// HTTP API for internal services to broadcast events (protected by API key)
app.MapPost("/api/broadcast", BroadcastEndpoints.BroadcastEvent);
app.MapPost("/api/broadcast/activity", BroadcastEndpoints.BroadcastActivity);

app.Run();
