using Snakk.Web.Services;
using Snakk.Web.Filters;
using System.Threading.RateLimiting;
using Snakk.Web.Middleware;
using Snakk.Web.Endpoints;
using Snakk.Web.Helpers;
using Snakk.Shared.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.AspNetCore.HttpOverrides;
using System.IO.Compression;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Grpc.Core.Interceptors;
using Prometheus;
using Serilog;
using Snakk.ServiceDefaults;
using System.Net;
using System.Security.Claims;
using System.Text;

// Allow gRPC (HTTP/2) over plain HTTP — needed in Docker where services communicate without TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Load shared config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(
    Path.Combine(sharedConfigDir, "conf", "snakk-config.json"),
    optional: true,
    reloadOnChange: true);

// Load UI locale strings at startup — platform-wide, picked via Ui:Language
{
    var requested = builder.Configuration["Ui:Language"] ?? "en";
    var localesDir = Path.Combine(AppContext.BaseDirectory, "Locales");
    var path = Path.Combine(localesDir, $"{requested}.json");
    if (!File.Exists(path))
    {
        Console.WriteLine($"[i18n] locale '{requested}' not found, falling back to en.json");
        path = Path.Combine(localesDir, "en.json");
    }
    T._loc = LocaleLoader.LoadFlat(path);
}

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

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
});

// Add services to the container
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new HtmxLayoutFilter());
});

// Add HttpContextAccessor for forwarding auth cookies
builder.Services.AddHttpContextAccessor();

// Distributed cache: Valkey on production, in-memory fallback for development
var valkeyConn = builder.Configuration["Valkey:ConnectionString"];
if (!string.IsNullOrEmpty(valkeyConn))
    builder.Services.AddStackExchangeRedisCache(opts => { opts.Configuration = valkeyConn; opts.InstanceName = "snakk:"; });
else
    builder.Services.AddDistributedMemoryCache();

// HybridCache uses IDistributedCache above as L2 backing store
builder.Services.AddHybridCache();

// Output cache for anonymous visitors — cached responses bypass the full gRPC pipeline
builder.Services.AddOutputCache(options =>
{
    // Disable lock coalescing: if cache generation hangs, waiters don't pile up.
    // Combined with the gRPC 5s deadline, worst case is a duplicate generation, not a hang.
    // Pages: 30s cache for anonymous visitors
    // Vary by HX-Request header so HTMX boosted requests get separate cache entries
    options.AddPolicy("AnonymousPage", builder => builder
        .With(ctx =>
            !ctx.HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName) &&
            !ctx.HttpContext.Request.Cookies.ContainsKey(AdultContentGate.ConfirmedCookie) &&
            !ctx.HttpContext.Request.Cookies.ContainsKey(AdultContentGate.DeclinedCookie))
        .Expire(TimeSpan.FromSeconds(30))
        .SetLocking(false)
        .SetVaryByHeader("HX-Request")
        .SetVaryByQuery("cursor", "offset", "pageSize", "typeFilter")
        .SetVaryByRouteValue("communitySlug", "hubSlug", "spaceSlug", "discussionSlugId", "publicId"));

    // HTMX partials: 10s cache for anonymous visitors
    options.AddPolicy("AnonymousPartial", builder => builder
        .With(ctx =>
            !ctx.HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName) &&
            !ctx.HttpContext.Request.Cookies.ContainsKey(AdultContentGate.ConfirmedCookie) &&
            !ctx.HttpContext.Request.Cookies.ContainsKey(AdultContentGate.DeclinedCookie))
        .Expire(TimeSpan.FromSeconds(10))
        .SetLocking(false)
        .SetVaryByHeader("HX-Request")
        .SetVaryByQuery("cursor", "offset", "pageSize", "communityId", "hubId", "spaceId", "typeFilter", "hideCommunity", "hideHub"));

    // User profiles: 60s cache for anonymous visitors
    options.AddPolicy("AnonymousProfile", builder => builder
        .With(ctx => !ctx.HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
        .Expire(TimeSpan.FromSeconds(60))
        .SetLocking(false)
        .SetVaryByHeader("HX-Request")
        .SetVaryByRouteValue("publicId"));
});

// OAuth provider availability — derived from Authentication:*:ClientId presence, same as Snakk.Auth
builder.Services.Configure<OAuthProvidersOptions>(opts =>
{
    var c = builder.Configuration;
    opts.Google   = !string.IsNullOrEmpty(c["Authentication:Google:ClientId"]);
    opts.GitHub   = !string.IsNullOrEmpty(c["Authentication:GitHub:ClientId"]);
    opts.Discord  = !string.IsNullOrEmpty(c["Authentication:Discord:ClientId"]);
    opts.Facebook   = !string.IsNullOrEmpty(c["Authentication:Facebook:ClientId"]);
    opts.Microsoft  = !string.IsNullOrEmpty(c["Authentication:Microsoft:ClientId"]);
    opts.Steam      = !string.IsNullOrEmpty(c["Authentication:Steam:ApiKey"]);
});

// Community context (scoped per request)
builder.Services.AddScoped<ICommunityContext, CommunityContext>();

// Community domain cache service (singleton - uses IMemoryCache)
builder.Services.AddSingleton<ICommunityDomainCacheService, CommunityDomainCacheService>();

// Prefetch cache service for sidebar data (singleton - uses IMemoryCache)
builder.Services.AddSingleton<IPrefetchCacheService, PrefetchCacheService>();

// Per-user cache for followed space IDs (2-min TTL, invalidated on follow toggle)
builder.Services.AddSingleton<IFollowedSpacesCacheService, FollowedSpacesCacheService>();

// WebOptimizer for CSS minification only (JS minification breaks TypeScript output)
builder.Services.AddWebOptimizer(pipeline =>
{
    // Minify all CSS files on-the-fly
    pipeline.MinifyCssFiles();
});

// Configure HttpClient for Internal API (for avatar proxy and other BFF endpoints)
// In Docker, REST uses a separate port (HTTP/1.1) from gRPC (HTTP/2)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17101";
var apiRestBaseUrl = builder.Configuration["ApiRestBaseUrl"] ?? apiBaseUrl;

builder.Services.AddHttpClient("InternalApi", client =>
{
    client.BaseAddress = new Uri(apiRestBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler();
builder.Services.AddSingleton(sp =>
{
    var handler = new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
    };

    // When using plain HTTP (no TLS), force HTTP/2 cleartext (h2c) for gRPC.
    // Without this, the client tries HTTP/2 via ALPN which requires TLS.
    if (apiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    {
        return Grpc.Net.Client.GrpcChannel.ForAddress(apiBaseUrl, new Grpc.Net.Client.GrpcChannelOptions
        {
            HttpHandler = handler,
            HttpVersion = new Version(2, 0),
            HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact
        });
    }

    return Grpc.Net.Client.GrpcChannel.ForAddress(apiBaseUrl, new Grpc.Net.Client.GrpcChannelOptions
    {
        HttpHandler = handler
    });
});
builder.Services.AddSingleton<GrpcAuthInterceptor>();

// Helper to register typed gRPC clients (all share channel + auth interceptor)
void AddGrpcClient<T>(IServiceCollection services) where T : class
{
    services.AddSingleton(sp =>
    {
        var channel = sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>();
        var authInterceptor = sp.GetRequiredService<GrpcAuthInterceptor>();
        Grpc.Core.CallInvoker invoker = channel.CreateCallInvoker().Intercept(authInterceptor);
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
AddGrpcClient<Snakk.Protos.Consent.ConsentService.ConsentServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Save.SaveService.SaveServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.Passkey.PasskeyService.PasskeyServiceClient>(builder.Services);
AddGrpcClient<Snakk.Protos.TwoFactor.TwoFactorService.TwoFactorServiceClient>(builder.Services);

// Register SnakkApiClient (DI resolves all gRPC clients automatically)
builder.Services.AddSingleton<SnakkApiClient>();

// Site settings cache (background service — refreshes site timezone every 10 min)
builder.Services.AddSingleton<SiteSettingsCacheService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SiteSettingsCacheService>());

// JWT-based authentication from SSO service
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SecretKey is not configured. Run the setup wizard (Snakk.Setup) first to generate configuration.");
if (!builder.Environment.IsDevelopment() && jwtSecretKey.Contains("change-in-production", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("SECURITY: Jwt:SecretKey must be overridden in production. Set it in snakk-config.json.");
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
                var token = context.HttpContext.Items[TokenRefreshMiddleware.RefreshedAccessTokenKey] as string
                    ?? context.Request.Cookies[AuthCookieHelper.AccessCookieName]
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

var disableRateLimiting = builder.Configuration.GetValue<bool>("DisableRateLimiting");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter, out var retry)
            ? (int)retry.TotalSeconds : 60;
        context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests", retryAfter }, cancellationToken);
    };

    // Content posts: 10 per 5 minutes per user
    options.AddPolicy("flood-post", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(GetRateLimitPartitionKey(httpContext),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10, TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(5),
                QueueLimit = 0, AutoReplenishment = true
            }));

    // Engagement: 20 per 5 minutes per user (reactions, votes)
    options.AddPolicy("flood-engage", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(GetRateLimitPartitionKey(httpContext),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20, TokensPerPeriod = 20,
                ReplenishmentPeriod = TimeSpan.FromMinutes(5),
                QueueLimit = 0, AutoReplenishment = true
            }));

    // Uploads: burst of 15 (full 12-image post), then 3/min sustained
    options.AddPolicy("flood-upload", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(GetRateLimitPartitionKey(httpContext),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 15, TokensPerPeriod = 3,
                ReplenishmentPeriod = TimeSpan.FromSeconds(60),
                QueueLimit = 0, AutoReplenishment = true
            }));
});

builder.Services.AddSingleton<DiscussionCreateRateLimiter>();

static string GetRateLimitPartitionKey(HttpContext ctx)
{
    var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    return string.IsNullOrEmpty(userId)
        ? $"ip:{ctx.Connection.RemoteIpAddress}"
        : $"user:{userId}";
}

// Persist Data Protection keys to shared storage so antiforgery + auth cookies
// survive container restarts and can be decrypted by Snakk.Auth/Snakk.Admin too.
var dataProtectionPath = Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? "/app/storage",
    "dataprotection-keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Snakk");

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

//app.UseSerilogRequestLogging();
app.UseHttpLogging();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Trust forwarded headers only from internal Docker/private networks, not any source
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Loopback, 32));
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.IPv6Loopback, 128));
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseStatusCodePagesWithReExecute("/NotFound");

// Disable status code page re-execution for API-like endpoints — they return structured
// responses with proper HTTP status codes, not HTML error pages
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var pathStr = path.Value ?? "";
    if (path.StartsWithSegments("/bff") ||
        path.StartsWithSegments("/oembed") ||
        pathStr.EndsWith("feed.xml") ||
        pathStr.EndsWith("feed.atom") ||
        pathStr.EndsWith("feed.json"))
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>();
        if (feature is not null)
            feature.Enabled = false;
    }

    await next();
});

// Only redirect to HTTPS when not behind a local reverse proxy (Docker/gateway)
if (!builder.Configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED"))
    app.UseHttpsRedirection();

// Security headers — CSP, clickjacking, MIME sniffing, referrer, permissions
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Generate a per-request nonce for inline scripts (theme flash prevention, FAB observer)
    var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    context.Items["csp-nonce"] = nonce;

    // Content Security Policy — allow self, Cloudflare Turnstile, inline styles (Tailwind), WebSocket for SignalR
    headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}' https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob: https:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-src 'self' https://challenges.cloudflare.com https://www.youtube.com https://www.youtube-nocookie.com https://*.vimeo.com https://vimeo.com; " +
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
        if (!app.Environment.IsDevelopment())
        {
            const int oneYear = 60 * 60 * 24 * 365;
            // Icons served with ?v=hash query string are content-versioned — mark immutable
            var isVersionedIcon = ctx.Context.Request.Path.StartsWithSegments("/icons")
                                  && ctx.Context.Request.QueryString.HasValue;
            var cacheControl = isVersionedIcon
                ? $"public,max-age={oneYear},immutable"
                : $"public,max-age={oneYear}";
            ctx.Context.Response.Headers.Append("Cache-Control", cacheControl);
        }
    }
});

// Configure avatar URL base for CDN support (S3 uses PublicUrlBase, local uses relative URLs)
var webStorageProvider = builder.Configuration["FileStorage:Provider"];
Snakk.Shared.Helpers.AvatarHelper.UploadedAvatarBaseUrl =
    string.Equals(webStorageProvider, "S3", StringComparison.OrdinalIgnoreCase)
        ? (builder.Configuration["FileStorage:S3:PublicUrlBase"] ?? "").TrimEnd('/')
        : "";

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
    RequestPath = "/media",
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

// Catch USER_REVOKED from gRPC interceptor — store hint cookie, redirect back with session-expired flag
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (AuthenticationRevokedException)
    {
        // context.User is still populated (UseAuthentication ran on the way in before the exception)
        var email       = context.User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var displayName = context.User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var avatarThumb = context.User.FindFirst("AvatarThumbnailFileName")?.Value ?? "";

        var hint    = System.Text.Json.JsonSerializer.Serialize(new { e = email, n = displayName, a = avatarThumb });
        var hintB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(hint));

        context.Response.Cookies.Append(".Snakk.ReloginHint", hintB64, new CookieOptions
        {
            Path        = "/",
            MaxAge      = TimeSpan.FromMinutes(5),
            SameSite    = SameSiteMode.Lax,
            HttpOnly    = false,
            IsEssential = true
        });

        // BFF calls expect JSON, not a redirect — return 401 so JS can dispatch snakk:session:expired
        if (context.Request.Path.StartsWithSegments("/bff"))
        {
            context.Response.StatusCode  = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"session_expired\"}");
            return;
        }

        var returnPath = context.Request.Path.Value ?? "/";
        var dest       = returnPath + "?session-expired=1";

        // HTMX full-page redirect — HX-Redirect header tells HTMX to navigate the whole page
        if (context.Request.Headers.ContainsKey("HX-Request"))
        {
            context.Response.StatusCode          = 200;
            context.Response.Headers["HX-Redirect"] = dest;
            return;
        }

        context.Response.Redirect(dest);
    }
});

app.UseRouting();

app.UseMiddleware<TokenRefreshMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
if (!disableRateLimiting) app.UseRateLimiter();

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

// Force authenticated users with no email address to the email entry page
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && context.User.FindFirst(System.Security.Claims.ClaimTypes.Email) is null
        && !context.Request.Path.StartsWithSegments("/bff"))
    {
        context.Response.Redirect("/auth/oauth/setup-email");
        return;
    }

    await next();
});

// Force users who haven't set their display name to the setup page
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && context.User.FindFirst(System.Security.Claims.ClaimTypes.Name) is null
        && !context.Request.Path.StartsWithSegments("/bff"))
    {
        context.Response.Redirect("/auth/setup-profile");
        return;
    }

    await next();
});

// Output cache for anonymous visitors (after auth so we can check cookies)
app.UseOutputCache();

// CDN cache headers for anonymous page responses (Cloudflare, etc.)
// Logged-in users get private/no-store; anonymous get s-maxage for edge caching.
app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == 200
        && !context.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName)
        && !context.Request.Path.StartsWithSegments("/bff")
        && !context.Request.Path.StartsWithSegments("/partials")
        && context.Response.ContentType?.StartsWith("text/html") == true)
    {
        var isHtmx = context.Request.Headers.ContainsKey("HX-Request");
        if (isHtmx)
        {
            context.Response.Headers.CacheControl = "private, no-store";
        }
        else
        {
            context.Response.Headers.CacheControl = "public, s-maxage=30, max-age=0, must-revalidate";
            context.Response.Headers.Vary = "HX-Request";
        }
    }
});

app.MapRazorPages();

// BFF API endpoints
app.MapBffApiEndpoints();
app.MapPasskeyBffEndpoints();
app.MapOAuthConnectionBffEndpoints();

app.MapRealtimeTokenEndpoints();

// Public endpoints
app.MapSitemapEndpoints();
app.MapOEmbedEndpoints();
app.MapRssFeedEndpoints();

// Adult content confirmation (sets session cookie)
app.MapPost("/bff/adult-confirm", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Append("snakk.adult-confirmed", "1", new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
        // No Expires = session cookie, cleared when browser closes
    });

    var returnUrl = ctx.Request.Form["returnUrl"].FirstOrDefault() ?? "/";
    if (!returnUrl.StartsWith("/") || returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\")) returnUrl = "/";
    return Results.Redirect(returnUrl);
}).DisableAntiforgery();

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

// Prometheus metrics (GC, thread pool, HTTP request durations)
app.UseHttpMetrics();
app.MapMetrics("/metrics");

app.Run();
