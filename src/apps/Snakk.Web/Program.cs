using Snakk.Web.Services;
using Snakk.Web.Filters;
using Snakk.Web.Middleware;
using Snakk.Web.Endpoints;
using Snakk.Shared.Helpers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.AspNetCore.HttpOverrides;
using System.IO.Compression;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Grpc.Core.Interceptors;
using Serilog;
using Snakk.ServiceDefaults;
using System.Text;

// Allow gRPC (HTTP/2) over plain HTTP — needed in Docker where services communicate without TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(
    Path.Combine(sharedConfigDir, "appsettings.Production.json"),
    optional: true,
    reloadOnChange: true);

//builder.AddSnakkDefaults();

// HTTP/1.1 + HTTP/2 — supports both REST and gRPC clients
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// Add response compression (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/css",
        "application/javascript",
        "text/javascript",
        "application/json",
        "text/html"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// Configure JSON serialization with source generators for BFF types
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, Snakk.Web.Models.Bff.BffJsonContext.Default);
});

// Add services to the container
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new HtmxLayoutFilter());
});

// Add HttpContextAccessor for forwarding auth cookies
builder.Services.AddHttpContextAccessor();

// HybridCache for caching (stampede-safe, also registers IMemoryCache for domain cache)
builder.Services.AddHybridCache();

// Community context (scoped per request)
builder.Services.AddScoped<ICommunityContext, CommunityContext>();

// Community domain cache service (singleton - uses IMemoryCache)
builder.Services.AddSingleton<ICommunityDomainCacheService, CommunityDomainCacheService>();

// Prefetch cache service for sidebar data (singleton - uses IMemoryCache)
builder.Services.AddSingleton<IPrefetchCacheService, PrefetchCacheService>();

// WebOptimizer for CSS minification only (JS minification breaks TypeScript output)
builder.Services.AddWebOptimizer(pipeline =>
{
    // Minify all CSS files on-the-fly
    pipeline.MinifyCssFiles();
});

// Configure HttpClient for Internal API (for avatar proxy and other BFF endpoints)
builder.Services.AddHttpClient("InternalApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17100");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler();

// gRPC channel + clients for API communication
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17100";
builder.Services.AddSingleton(sp =>
{
    return Grpc.Net.Client.GrpcChannel.ForAddress(apiBaseUrl, new Grpc.Net.Client.GrpcChannelOptions
    {
        HttpHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true
        }
    });
});
builder.Services.AddSingleton<GrpcAuthInterceptor>();

// Helper to register typed gRPC clients (all share channel + auth interceptor)
void AddGrpcClient<T>(IServiceCollection services) where T : class
{
    services.AddSingleton(sp =>
    {
        var channel = sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>();
        var interceptor = sp.GetRequiredService<GrpcAuthInterceptor>();
        var invoker = channel.CreateCallInvoker().Intercept(interceptor);
        return (T)Activator.CreateInstance(typeof(T), invoker)!;
    });
}

AddGrpcClient<Snakk.Protos.Auth.AuthService.AuthServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Community.CommunityService.CommunityServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Hub.HubService.HubServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Space.SpaceService.SpaceServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Discussion.DiscussionService.DiscussionServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Post.PostService.PostServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Follow.FollowService.FollowServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Reaction.ReactionService.ReactionServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Notification.NotificationService.NotificationServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Moderation.ModerationService.ModerationServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Search.SearchService.SearchServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Statistics.StatisticsService.StatisticsServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.User.UserService.UserServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.ReadState.ReadStateService.ReadStateServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Markup.MarkupService.MarkupServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Banner.BannerService.BannerServiceClient>(builder.Services);

// Register SnakkApiClient (DI resolves all gRPC clients automatically)
builder.Services.AddSingleton<SnakkApiClient>();

// Site settings cache (background service — refreshes site timezone every 10 min)
builder.Services.AddSingleton<SiteSettingsCacheService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SiteSettingsCacheService>());

// JWT-based authentication from SSO service
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SecretKey is not configured. Run the setup wizard (Snakk.Setup) first to generate configuration.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Snakk";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Snakk";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)) { KeyId = "snakk-hmac" }
        };

        // Read JWT from cookie instead of Authorization header.
        // Strict cookie (.Snakk.Auth) is preferred — it's only sent on same-site requests.
        // Falls back to Lax session cookie (.Snakk.Auth.Session) for cross-site navigation personalization.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies[AuthCookieHelper.AccessCookieName]
                    ?? context.Request.Cookies[AuthCookieHelper.SessionCookieName];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

//app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto
});

app.UseStatusCodePagesWithReExecute("/NotFound");

// Disable status code page re-execution for BFF API endpoints — they return JSON error
// responses with proper HTTP status codes, not HTML error pages
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/bff"))
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>();
        if (feature is not null)
            feature.Enabled = false;
    }

    await next();
});

app.UseHttpsRedirection();

// Security headers — CSP, clickjacking, MIME sniffing, referrer, permissions
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Content Security Policy — allow self, Cloudflare Turnstile, inline styles (Tailwind), WebSocket for SignalR
    headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self'; " +
        "connect-src 'self' wss: ws:; " +
        "frame-src 'self' https://challenges.cloudflare.com; " +
        "frame-ancestors 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'");

    // Clickjacking protection (redundant with frame-ancestors but covers older browsers)
    headers.Append("X-Frame-Options", "SAMEORIGIN");

    // Prevent MIME type sniffing
    headers.Append("X-Content-Type-Options", "nosniff");

    // Control referrer information
    headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    // Restrict browser features
    headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=(), payment=(), usb=()");

    await next();
});

// Resource preload hints — send Link headers for critical CSS so browser starts downloading
// before HTML parsing. These headers propagate through YARP gateway to the client.
app.Use(async (context, next) =>
{
    var accept = context.Request.Headers.Accept.ToString();
    if (accept.Contains("text/html")
        && !context.Request.Path.StartsWithSegments("/bff")
        && !context.Request.Path.StartsWithSegments("/partials")
        && !context.Request.Path.StartsWithSegments("/health"))
    {
        var fvp = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.IFileVersionProvider>();
        var basePath = context.Request.PathBase;

        var tailwindUrl = fvp.AddFileVersionToPath(basePath, "/css/vendor/tailwind.css");
        var siteUrl = fvp.AddFileVersionToPath(basePath, "/css/dist/site.css");

        context.Response.Headers.Append("Link",
            $"<{tailwindUrl}>; rel=preload; as=style, <{siteUrl}>; rel=preload; as=style");
    }

    await next();
});

// Enable response compression and WebOptimizer (skip for partials — small HTML fragments)
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/partials"),
    appBuilder =>
    {
        appBuilder.UseResponseCompression();
        appBuilder.UseWebOptimizer();
    }
);

// Serve wwwroot from root (default behavior) - allows /robots.txt, /favicon.ico, etc.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 1 year in production
        if (!app.Environment.IsDevelopment())
        {
            const int durationInSeconds = 60 * 60 * 24 * 365; // 1 year
            ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={durationInSeconds}");
        }
    }
});

// Serve avatars from configured storage path (ensure directory exists for first-run)
var storagePath = Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? "storage",
    "avatars"
);
var avatarsPath = Path.GetFullPath(storagePath);
Directory.CreateDirectory(avatarsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(avatarsPath),
    RequestPath = "/avatars",
    OnPrepareResponse = ctx =>
    {
        // Generated avatars are immutable (deterministic SVGs based on ID hash)
        if (ctx.Context.Request.Path.StartsWithSegments("/avatars/generated"))
        {
            const int oneYear = 60 * 60 * 24 * 365;
            ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={oneYear},immutable");
        }
        else
        {
            // Uploaded avatars can change - shorter cache time
            const int oneHour = 60 * 60;
            ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={oneHour}");
        }
    }
});

// Serve uploaded media from configured storage path (local dev only — in production, S3/R2 serves directly)
var mediaStoragePath = Path.GetFullPath(Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? "storage",
    "media"));
Directory.CreateDirectory(mediaStoragePath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaStoragePath),
    RequestPath = "/storage/media",
    OnPrepareResponse = ctx =>
    {
        // Media files are content-addressed (SHA-256 hash) — immutable
        const int oneYear = 60 * 60 * 24 * 365;
        ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={oneYear},immutable");

        // Security: prevent MIME sniffing and force inline display (no download prompts)
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        ctx.Context.Response.Headers.Append("Content-Disposition", "inline");
    }
});

// Server-Timing headers for debug panel (dev only, after static files to skip those)
if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<Snakk.Web.Middleware.ServerTimingMiddleware>();
}

// Resolve community from URL (must be before routing)
app.UseCommunityResolution();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// SameSite=Strict mutation guard: BFF state-changing requests (POST/PUT/DELETE) require
// the Strict auth cookie, not just the Lax session cookie. This prevents CSRF attacks
// where the Lax cookie is sent on cross-site navigations but can't perform mutations.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/bff")
        && context.Request.Method is not "GET" and not "HEAD" and not "OPTIONS"
        && context.User.Identity?.IsAuthenticated == true
        && !AuthCookieHelper.HasStrictAuthCookie(context))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsJsonAsync(new { error = "Cross-site mutation rejected" });
        return;
    }

    await next();
});

// Redirect unauthenticated users to SSO login for protected actions
app.UseAuthRedirect();

app.MapRazorPages();

// BFF API endpoints
app.MapBffApiEndpoints();
app.MapRealtimeTokenEndpoints();

// Public endpoints
app.MapSitemapEndpoints();
app.MapOEmbedEndpoints();
app.MapRssFeedEndpoints();

// Health check for gateway probes (verifies gRPC channel connectivity)
app.MapGet("/health", async (Grpc.Net.Client.GrpcChannel channel) =>
{
    try
    {
        var state = channel.State;
        return Results.Ok(new { status = "healthy", grpcChannel = state.ToString() });
    }
    catch
    {
        return Results.Ok(new { status = "degraded", grpcChannel = "unknown" });
    }
});

app.Run();
