using Snakk.Realtime;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for browser connections
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? new[] { "http://localhost:5001", "https://localhost:7001" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// API Key authentication for internal service calls
app.UseApiKeyAuth();

app.UseCors();

// SignalR hub for browser WebSocket connections
app.MapHub<RealtimeHub>("/realtime");

// HTTP API for internal services to broadcast events (protected by API key)
app.MapPost("/api/broadcast", BroadcastEndpoints.BroadcastEvent);
app.MapPost("/api/broadcast/activity", BroadcastEndpoints.BroadcastActivity);

app.Run();
