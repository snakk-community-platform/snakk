using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Snakk.ServiceDefaults;
using System.Net;
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

//builder.AddSnakkDefaults();

// Add Razor Pages
builder.Services.AddRazorPages();

// gRPC client for calling Snakk.Api
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17101";
var grpcHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
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
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
if (!string.IsNullOrEmpty(googleClientId))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/oauth/google/callback";
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
    });
}

// Configure forwarded headers — trust only internal Docker/private networks, not any source
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});
    
// Persist Data Protection keys to shared storage so antiforgery + auth cookies
// survive container restarts and can be decrypted by Snakk.Web/Snakk.Admin too.
var dataProtectionPath = Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? "/app/storage",
    "dataprotection-keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Snakk");

var app = builder.Build();

//app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

// Use path base — gateway routes /auth/* and strips the prefix,
// so generated URLs (OAuth redirect URIs, etc.) need to include it
app.UsePathBase("/auth");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

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

app.Run();
