using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(Path.Combine(sharedConfigDir, "appsettings.Production.json"), optional: true, reloadOnChange: true);

// Add Razor Pages
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// gRPC client for calling Snakk.Api
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:17100";
builder.Services.AddSingleton(_ =>
    Grpc.Net.Client.GrpcChannel.ForAddress(apiBaseUrl, new Grpc.Net.Client.GrpcChannelOptions
    {
        HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
    }));
builder.Services.AddScoped(sp =>
    new Snakk.Protos.Auth.AuthService.AuthServiceClient(sp.GetRequiredService<Grpc.Net.Client.GrpcChannel>()));

// Cookie-based session for auth flow (before JWT is issued)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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
    options.Cookie.Name = ".Snakk.Auth.Session";
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

// Configure forwarded headers for proxy scenarios
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

// Use path base when running behind gateway
if (app.Environment.IsDevelopment())
{
    app.UsePathBase("/auth");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Health check for gateway probes
app.MapGet("/health", () => Results.Ok());

app.Run();
