using Snakk.Web.Services;
using Snakk.Web.Filters;
using Snakk.Web.Middleware;
using Snakk.Web.Endpoints;
using Snakk.Application.Services;
using Snakk.Shared.Helpers;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Grpc.Core.Interceptors;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

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
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Add services to the container
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new HtmxLayoutFilter());
});

// Add HttpContextAccessor for forwarding auth cookies
builder.Services.AddHttpContextAccessor();

// Memory cache for domain -> community mapping
builder.Services.AddMemoryCache();

// Community context (scoped per request)
builder.Services.AddScoped<ICommunityContext, CommunityContext>();

// Community domain cache service (singleton - uses IMemoryCache)
builder.Services.AddSingleton<ICommunityDomainCacheService, CommunityDomainCacheService>();

// Prefetch cache service for sidebar data (singleton - uses IMemoryCache)
builder.Services.AddSingleton<IPrefetchCacheService, PrefetchCacheService>();

// Markup Parser (for rendering post content)
builder.Services.AddSingleton<IMarkupParser, MarkupParser>();

// WebOptimizer for CSS minification only (JS minification breaks TypeScript output)
builder.Services.AddWebOptimizer(pipeline =>
{
    // Minify all CSS files on-the-fly
    pipeline.MinifyCssFiles();
});

// Configure HttpClient for Internal API (for avatar proxy and other BFF endpoints)
builder.Services.AddHttpClient("InternalApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5242");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// gRPC channel + clients for API communication
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5242";
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

// Register SnakkApiClient (DI resolves all gRPC clients automatically)
builder.Services.AddSingleton<SnakkApiClient>();

// Setup wizard service (scoped — uses IConfiguration)
builder.Services.AddScoped<SetupService>();

// Session for setup wizard state
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// JWT-based authentication from SSO service
// During first-run setup, Jwt:SecretKey is not configured yet — use a placeholder.
// SetupMiddleware blocks all non-setup requests, so the placeholder key is never used for real auth.
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrEmpty(jwtSecretKey))
{
    jwtSecretKey = "SETUP_NOT_COMPLETE_PLACEHOLDER_KEY_MINIMUM_32_CHARS_LONG_FOR_HMAC256";
    if (!builder.Environment.IsDevelopment())
    {
        var startupLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
        startupLogger.LogWarning("SECURITY: Jwt:SecretKey not configured — using setup placeholder. " +
            "This is expected during first-run setup only. Complete the setup wizard to generate a real key.");
    }
}
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };

        // Read JWT from cookie instead of Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies[".Snakk.Auth"];
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

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

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

// Server-Timing headers for debug panel (dev only, after static files to skip those)
if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<Snakk.Web.Middleware.ServerTimingMiddleware>();
}

// Session (required by setup wizard for state management — skip for partials)
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/partials"),
    appBuilder => appBuilder.UseSession()
);

// First-run setup wizard — redirects to /setup if not configured
app.UseMiddleware<SetupMiddleware>();

// Resolve community from URL (must be before routing)
app.UseCommunityResolution();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Redirect unauthenticated users to SSO login for protected actions
app.UseAuthRedirect();

app.MapRazorPages();

// BFF API endpoints
app.MapBffApiEndpoints();

// Public endpoints
app.MapSitemapEndpoints();

app.Run();
