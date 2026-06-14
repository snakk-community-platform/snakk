using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Snakk.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Snakk.Admin.Services;
using MudBlazor.Services;
using Snakk.ServiceDefaults;
using System.Net;
using System.Net.Http;
using System.Text;
using Grpc.Core.Interceptors;
using Snakk.Protos.Auth;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Manage;
using Snakk.Protos.Space;

// Allow gRPC (HTTP/2) over plain HTTP — needed in Docker where services communicate without TLS
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Load shared config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(
    Path.Combine(sharedConfigDir, "conf", "snakk-config.json"),
    optional: true,
    reloadOnChange: true);

builder.AddSnakkObservability();

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

// Scoped token provider for Blazor Server circuits
builder.Services.AddScoped<CircuitTokenProvider>();

// gRPC channel (singleton — connection pooling with HTTP/2 multiplexing)
var snakkApiBaseUrl = builder.Configuration["SnakkApi:BaseUrl"] ?? "https://localhost:17101";
var grpcHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
var grpcOptions = new Grpc.Net.Client.GrpcChannelOptions { HttpHandler = grpcHandler };

// When using plain HTTP (no TLS), force HTTP/2 cleartext (h2c) for gRPC
if (snakkApiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
{
    grpcOptions.HttpVersion = new Version(2, 0);
    grpcOptions.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
}

var channel = Grpc.Net.Client.GrpcChannel.ForAddress(snakkApiBaseUrl, grpcOptions);
builder.Services.AddSingleton(channel);

// gRPC auth interceptor (scoped — reads from CircuitTokenProvider per circuit)
builder.Services.AddScoped<GrpcAuthInterceptor>();

// gRPC client registrations (scoped — each circuit gets its own intercepted invoker)
void AddGrpcClient<T>(IServiceCollection services) where T : class
{
    services.AddScoped(sp =>
    {
        var ch = sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>();
        var interceptor = sp.GetRequiredService<GrpcAuthInterceptor>();
        var invoker = ch.CreateCallInvoker().Intercept(interceptor);
        return (T)Activator.CreateInstance(typeof(T), invoker)!;
    });
}

AddGrpcClient<AuthService.AuthServiceClient>(builder.Services);
AddGrpcClient<ManageService.ManageServiceClient>(builder.Services);
AddGrpcClient<CommunityService.CommunityServiceClient>(builder.Services);
AddGrpcClient<HubService.HubServiceClient>(builder.Services);
AddGrpcClient<SpaceService.SpaceServiceClient>(builder.Services);

// Named HttpClient for REST calls to internal API (file uploads, etc.)
// In Docker, REST uses a separate HTTP/1.1 port from gRPC (HTTP/2).
// Locally with HTTPS, both protocols work on the same port via ALPN.
var snakkApiRestUrl = builder.Configuration["SnakkApi:RestBaseUrl"] ?? snakkApiBaseUrl;
builder.Services.AddHttpClient("SnakkApi", client =>
{
    client.BaseAddress = new Uri(snakkApiRestUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ManageScopeService (scoped — uses gRPC client)
builder.Services.AddScoped<ManageScopeService>();
builder.Services.AddScoped<ManageScopeState>();
builder.Services.AddScoped<AdminTimezoneService>();
builder.Services.AddScoped<FeatureFlagService>();

// JWT-based authentication from SSO service
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
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
            },
            OnChallenge = context =>
            {
                // Redirect to SSO login when not authenticated
                context.HandleResponse();
                var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                var loginUrl = $"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
                context.Response.Redirect(loginUrl);
                return Task.CompletedTask;
            }
        };
    });

// Require authentication for all admin pages
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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

// Session for temp data
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Persist Data Protection keys in Postgres — shared across all services,
// durable as app data, and read at most once per 24 h (cached in memory).
builder.Services.AddDbContext<DataProtectionDbContext>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("DbConnection")
        ?? throw new InvalidOperationException("DbConnection not configured")));

builder.Services.AddDataProtection()
    .SetApplicationName("Snakk")
    .PersistKeysToDbContext<DataProtectionDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dpDb = scope.ServiceProvider.GetRequiredService<DataProtectionDbContext>();
    await dpDb.EnsureSchemaAsync();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

// All admin routes live under /admin/ — gateway forwards /admin/{**catch-all} here
app.UsePathBase("/admin");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<Snakk.Admin.App>()
    .AddInteractiveServerRenderMode()
    .WithStaticAssets();

// Health check for gateway probes
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
}).AllowAnonymous();

app.Run();
