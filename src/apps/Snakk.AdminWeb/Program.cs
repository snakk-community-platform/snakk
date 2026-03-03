using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Snakk.AdminWeb.Services;
using Snakk.Sdk;
using System.Net.Http.Headers;
using Serilog;
using Snakk.ServiceDefaults;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load shared production config (written by setup wizard)
var sharedConfigDir = builder.Configuration["FileStorage:BasePath"] ?? "/app/storage";
builder.Configuration.AddJsonFile(
    Path.Combine(sharedConfigDir, "appsettings.Production.json"),
    optional: true,
    reloadOnChange: true);

builder.AddSnakkDefaults();

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();

// Scoped token provider for Blazor Server circuits
var snakkApiBaseUrl = builder.Configuration["SnakkApi:BaseUrl"] ?? "https://localhost:17100";

builder.Services.AddScoped<CircuitTokenProvider>();

// Named HttpClient for API calls (IHttpClientFactory manages socket pooling)
builder.Services.AddHttpClient("SnakkApi", client =>
{
    client.BaseAddress = new Uri(snakkApiBaseUrl);
});

// Resolve SnakkApiClient as scoped, setting auth header from circuit token
builder.Services.AddScoped(sp =>
{
    var httpClient = CreateAuthenticatedClient(sp);
    var apiClient = new SnakkApiClient(httpClient);
    apiClient.BaseUrl = snakkApiBaseUrl;

    return apiClient;
});

builder.Services.AddScoped<AdminApiClientService>();

builder.Services.AddScoped(sp =>
{
    var httpClient = CreateAuthenticatedClient(sp);
    var logger = sp.GetRequiredService<ILogger<ManageScopeService>>();

    return new ManageScopeService(httpClient, logger);
});

// JWT-based authentication from SSO service
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
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
            },
            OnChallenge = context =>
            {
                // Redirect to SSO login when not authenticated
                context.HandleResponse();
                var returnUrl = context.Request.Path + context.Request.QueryString;
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

// Configure forwarded headers for proxy scenarios
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Session for temp data
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle forwarded headers from reverse proxy
app.UseForwardedHeaders();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Health check for gateway probes
app.MapGet("/health", () => Results.Ok());

app.Run();

/// <summary>
/// Creates an HttpClient from the named factory with the JWT Authorization header set.
/// Reads token from CircuitTokenProvider (Blazor circuit) or falls back to HttpContext cookie (prerender).
/// </summary>
static HttpClient CreateAuthenticatedClient(IServiceProvider sp)
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = factory.CreateClient("SnakkApi");

    // Try circuit token first (Blazor circuit is active)
    var tokenProvider = sp.GetRequiredService<CircuitTokenProvider>();
    var token = tokenProvider.Token;

    // Fallback to HttpContext cookie (initial HTTP request / prerender)
    if (string.IsNullOrEmpty(token))
    {
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        token = httpContextAccessor.HttpContext?.Request.Cookies[".Snakk.Auth"];
    }

    if (!string.IsNullOrEmpty(token))
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    return httpClient;
}
