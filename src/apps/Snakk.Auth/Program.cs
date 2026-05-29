using Microsoft.AspNetCore.Authentication.Cookies;
using Prometheus;
using Prometheus.DotNetRuntime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Serilog;
using Snakk.Auth.Data;
using Snakk.Auth.Endpoints;
using Snakk.Infrastructure.Database;
using Snakk.ServiceDefaults;
using System.Net;
using System.Text;

// Allow gRPC (HTTP/2) over plain HTTP — needed in Docker where services communicate without TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
ThreadPool.SetMinThreads(50, 50);
DotNetRuntimeStatsBuilder.Default().StartCollecting();

var builder = WebApplication.CreateBuilder(args);

// Auth/session cookies set Secure=true; over plain HTTP (dev/test) clients drop them,
// breaking every authenticated flow. Default secure, but disable in Development so
// local and load testing can authenticate. Override with Cookies:RequireSecure.
Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure =
    builder.Configuration.GetValue<bool?>("Cookies:RequireSecure") ?? !builder.Environment.IsDevelopment();

// Load shared config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(
    Path.Combine(sharedConfigDir, "conf", "snakk-config.json"),
    optional: true,
    reloadOnChange: true);

//builder.AddSnakkDefaults();

if (!builder.Environment.IsDevelopment())
{
    var jwtKey = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Contains("change-in-production", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("SECURITY: Jwt:SecretKey must be overridden in production. Set it in snakk-config.json.");
}

// Add Razor Pages
builder.Services.AddRazorPages();

// gRPC client for calling Snakk.Api
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17101";
var grpcHandler = new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
    KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
};
var grpcOptions = new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = grpcHandler };

// Plain HTTP (Docker): force HTTP/2 cleartext (h2c) for gRPC
if (apiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
{
    grpcOptions.HttpVersion = new Version(2, 0);
    grpcOptions.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
}

builder.Services.AddSingleton(_ =>
    Grpc.Net.Client.GrpcChannel.ForAddress(apiBaseUrl, grpcOptions));
builder.Services.AddScoped(sp =>
    new Snakk.Protos.Auth.AuthService.AuthServiceClient(sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>()));
builder.Services.AddScoped(sp =>
    new Snakk.Protos.TwoFactor.TwoFactorService.TwoFactorServiceClient(sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>()));
builder.Services.AddScoped(sp =>
    new Snakk.Protos.Consent.ConsentService.ConsentServiceClient(sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>()));
builder.Services.AddScoped(sp =>
    new Snakk.Protos.Passkey.PasskeyService.PasskeyServiceClient(sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>()));

// Auth database — OpenIddict store (isolated from main domain DB)
builder.Services.AddDbContext<SnakkAuthDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDbConnection")
        ?? throw new InvalidOperationException("AuthDbConnection not configured"));
    options.UseOpenIddict();
});

// OpenIddict OAuth2/OIDC Authorization Server
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<SnakkAuthDbContext>();
    })
    .AddServer(options =>
    {
        // Paths registered WITHOUT a leading slash so that URI resolution appends them
        // to context.BaseUri (which includes the PathBase, e.g. "/auth/") rather than
        // resetting to the server root — RFC 3986 §5.2: a leading "/" in a relative-ref
        // replaces the entire path component, discarding any PathBase prefix.
        options.SetTokenEndpointUris("connect/token")
               .SetAuthorizationEndpointUris("connect/authorize")
               .SetRevocationEndpointUris("connect/revoke")
               .SetIntrospectionEndpointUris("connect/introspect");

        options.AllowClientCredentialsFlow()
               .AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
               .AllowRefreshTokenFlow();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.OfflineAccess,
            "discussions:read");

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(30));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        // Explicit issuer — required when served behind a path-prefix reverse proxy (e.g. /auth).
        // Without this, OpenIddict derives the issuer from the incoming Host header and omits the
        // path prefix, causing OidcClient issuer-name validation to fail.
        var explicitIssuer = builder.Configuration["Auth:Issuer"];
        if (!string.IsNullOrEmpty(explicitIssuer) && Uri.TryCreate(explicitIssuer, UriKind.Absolute, out var issuerUri))
            options.SetIssuer(issuerUri);

        // Dev: ephemeral keys — lost on restart, fine for development.
        // Production: replace with persisted ES256 signing key (DB or key vault).
        options.AddEphemeralEncryptionKey()
               .AddEphemeralSigningKey();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .DisableTransportSecurityRequirement();
        // Token endpoint is NOT in passthrough mode — OpenIddict handles it automatically.
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Cookie-based session for auth flow (before JWT is issued)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// OAuth Authentication Providers
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.Cookie.Name = ".Snakk.Auth.OAuthState";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
});

// Only register OAuth providers when their ClientId is configured.
// Without this guard, OAuthOptions.Validate() throws on every request
// when ClientId is an empty string (e.g. fresh install without OAuth).
// Redirect OAuth handler failures (bad state, token exchange errors, denied consent)
// to /Login with a reason query param instead of letting them bubble up as 500s.
static Func<Microsoft.AspNetCore.Authentication.RemoteFailureContext, Task> OnRemoteFailure(string provider)
    => context =>
    {
        var reason = context.Failure?.Message ?? "unknown";
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>().CreateLogger("OAuth." + provider);
        logger.LogWarning(context.Failure, "OAuth {Provider} remote failure: {Reason}", provider, reason);
        context.Response.Redirect($"/Login?error=oauth_remote_failed&provider={Uri.EscapeDataString(provider)}&reason={Uri.EscapeDataString(reason)}");
        context.HandleResponse();
        return Task.CompletedTask;
    };

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
if (!string.IsNullOrEmpty(googleClientId))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/google/callback";
        options.Events.OnRemoteFailure = OnRemoteFailure("Google");
    });
}

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
if (!string.IsNullOrEmpty(githubClientId))
{
    builder.Services.AddAuthentication().AddGitHub(options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/github/callback";
        options.Events.OnRemoteFailure = OnRemoteFailure("GitHub");
    });
}

var discordClientId = builder.Configuration["Authentication:Discord:ClientId"];
if (!string.IsNullOrEmpty(discordClientId))
{
    builder.Services.AddAuthentication().AddDiscord(options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/discord/callback";
        options.Scope.Add("email");
        options.Events.OnRemoteFailure = OnRemoteFailure("Discord");
    });

    // Second Discord scheme for account linking (no email scope, different callback path)
    builder.Services.AddAuthentication().AddDiscord("DiscordLink", options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/discord-link/callback";
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/discord-link/error?reason=oauth_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

var facebookClientId = builder.Configuration["Authentication:Facebook:ClientId"];
if (!string.IsNullOrEmpty(facebookClientId))
{
    builder.Services.AddAuthentication().AddFacebook(options =>
    {
        options.ClientId = facebookClientId;
        options.ClientSecret = builder.Configuration["Authentication:Facebook:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/facebook/callback";
        options.Events.OnRemoteFailure = OnRemoteFailure("Facebook");
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
if (!string.IsNullOrEmpty(microsoftClientId))
{
    builder.Services.AddAuthentication().AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/microsoft/callback";
        options.Events.OnRemoteFailure = OnRemoteFailure("Microsoft");
    });
}

var steamApiKey = builder.Configuration["Authentication:Steam:ApiKey"];
if (!string.IsNullOrEmpty(steamApiKey))
{
    builder.Services.AddAuthentication().AddSteam(options =>
    {
        options.ApplicationKey = steamApiKey;
        options.CallbackPath = "/oauth/steam/callback";
        options.Events.OnRemoteFailure = OnRemoteFailure("Steam");
    });
}

// Configure forwarded headers — trust only internal Docker/private networks, not any source
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Loopback, 32));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.IPv6Loopback, 128));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});
    
// Persist Data Protection keys in Postgres — shared across all services,
// durable as app data, and read at most once per 24 h (cached in memory).
builder.Services.AddDbContext<DataProtectionDbContext>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("DbConnection")
        ?? builder.Configuration.GetConnectionString("AuthDbConnection")
        ?? throw new InvalidOperationException("No DB connection string for Data Protection")));

builder.Services.AddDataProtection()
    .SetApplicationName("Snakk")
    .PersistKeysToDbContext<DataProtectionDbContext>();

builder.Services.AddHostedService<Snakk.Auth.Services.GrpcChannelWarmupService>();
builder.Services.AddHostedService<Snakk.Auth.Services.MauiClientSeeder>();

var app = builder.Build();

// Apply any pending OpenIddict DB migrations at startup; ensure DP keys table exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SnakkAuthDbContext>();
    await db.Database.MigrateAsync();
    var dpDb = scope.ServiceProvider.GetRequiredService<DataProtectionDbContext>();
    await dpDb.EnsureSchemaAsync();
}

//app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(eb => eb.Run(async ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"error\":\"internal_server_error\"}");
    }));
    app.UseHsts();
}

// Inform ASP.NET Core (and OpenIddict) that this service is mounted at /auth behind the
// gateway. UsePathBase strips /auth from Request.Path into PathBase on every request,
// so OpenIddict generates discovery-doc endpoint URIs that include the /auth/ prefix.
// When accessed without the prefix (Aspire dev / direct), this is a no-op.
app.UsePathBase("/auth");

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpMetrics();

app.MapRazorPages();
app.MapPasskeyLoginEndpoints();

// Health check for gateway probes (verifies gRPC channel connectivity)
app.MapGet("/health", (Grpc.Net.Client.GrpcChannel channel) =>
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

app.MapMetrics("/metrics");

app.Run();
