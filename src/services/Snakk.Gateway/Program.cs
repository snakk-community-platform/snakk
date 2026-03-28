using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Snakk.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Kestrel tuning
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxConcurrentConnections = 10_000;
    kestrel.Limits.MaxConcurrentUpgradedConnections = 5_000;
    kestrel.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    kestrel.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    kestrel.Limits.Http2.MaxStreamsPerConnection = 100;
    kestrel.Limits.Http2.InitialConnectionWindowSize = 1024 * 1024;
    kestrel.Limits.Http2.InitialStreamWindowSize = 768 * 1024;

    kestrel.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

// Load shared production config (written by setup wizard)
var sharedConfigDir = Environment.GetEnvironmentVariable("SNAKK_STORAGE_PATH") ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

// Setup mode: if first-run setup hasn't completed, route catch-all and static assets to Snakk.Setup
// This check runs once at startup. After setup completes, entrypoint.sh restarts all services,
// so the gateway re-launches and sees .setup-complete → normal routing.
var setupComplete = builder.Environment.IsDevelopment()
    || File.Exists(Path.Combine(sharedConfigDir, ".setup-complete"));
if (!setupComplete)
{
    builder.Configuration["ReverseProxy:Routes:web-route:ClusterId"] = "setup-cluster";
    builder.Configuration["ReverseProxy:Routes:static-css-route:ClusterId"] = "setup-cluster";
    builder.Configuration["ReverseProxy:Routes:static-js-route:ClusterId"] = "setup-cluster";
    builder.Configuration["ReverseProxy:Routes:static-images-route:ClusterId"] = "setup-cluster";
    builder.Configuration["ReverseProxy:Routes:static-media-route:ClusterId"] = "setup-cluster";

    // Disable health checks on clusters not used during setup
    builder.Configuration["ReverseProxy:Clusters:auth-cluster:HealthCheck:Active:Enabled"] = "false";
    builder.Configuration["ReverseProxy:Clusters:admin-cluster:HealthCheck:Active:Enabled"] = "false";
    builder.Configuration["ReverseProxy:Clusters:web-cluster:HealthCheck:Active:Enabled"] = "false";
    builder.Configuration["ReverseProxy:Clusters:realtime-cluster:HealthCheck:Active:Enabled"] = "false";
}
else
{
    // Setup app is stopped after first-run — stop polling its health endpoint
    builder.Configuration["ReverseProxy:Clusters:setup-cluster:HealthCheck:Active:Enabled"] = "false";
}

//builder.AddSnakkDefaults();

// Real client IP header (set by CDN/reverse proxy like Cloudflare)
var clientIpHeader = builder.Configuration["Gateway:ClientIpHeader"] ?? "CF-Connecting-IP";

// Response compression (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
        "application/json",
        "image/svg+xml"
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);

// Gateway-level rate limiting (per real client IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
            ? retry.TotalSeconds
            : 60;

        context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter).ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Policy"] = context.Lease.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests. Please try again later.",
            retryAfter
        }, cancellationToken);
    };

    // Auth endpoints: strict on POST (form submissions), relaxed on GET (page loads)
    options.AddPolicy("auth", context =>
    {
        var ip = GetClientIp(context, clientIpHeader);
        var isPost = context.Request.Method == "POST";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{ip}:{(isPost ? "post" : "get")}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isPost ? 30 : 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // HTMX partials: separate bucket (sidebars, infinite scroll, fragments)
    options.AddPolicy("partials", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: GetClientIp(context, clientIpHeader),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    // Dynamic page/API routes: moderate per-IP (excludes static assets and realtime)
    options.AddPolicy("general", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: GetClientIp(context, clientIpHeader),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
});

// Request timeouts (per-route in appsettings.json)
builder.Services.AddRequestTimeouts();

// YARP reverse proxy with connection pooling
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .ConfigureHttpClient((context, handler) =>
    {
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
        handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);
        handler.ConnectTimeout = TimeSpan.FromSeconds(5);

        // Longer connection lifetime for realtime (WebSocket connections are persistent)
        if (context.ClusterId == "realtime-cluster")
        {
            handler.PooledConnectionLifetime = TimeSpan.FromMinutes(15);
            handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10);
        }
    });

var app = builder.Build();

//app.UseSerilogRequestLogging();

// HTTPS redirection (skip in production — Caddy/Cloudflare handles TLS)
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();

// Middleware pipeline (order matters)
app.UseResponseCompression();
app.UseRouting();
app.UseRateLimiter();
app.UseRequestTimeouts();

// Gateway's own health endpoint (not proxied — handled locally before YARP)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));

app.MapReverseProxy();

app.Run();

// Resolve the real client IP behind a CDN/reverse proxy.
// Uses the configured header (default: CF-Connecting-IP for Cloudflare),
// falls back to X-Forwarded-For, then direct connection IP.
static string GetClientIp(HttpContext context, string clientIpHeader) =>
    context.Request.Headers[clientIpHeader].FirstOrDefault()
    ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',', StringSplitOptions.TrimEntries)[0]
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";
