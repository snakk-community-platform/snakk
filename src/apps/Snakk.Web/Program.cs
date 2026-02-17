using Snakk.Web.Services;
using Snakk.Web.Filters;
using Snakk.Web.Middleware;
using Snakk.Web.Endpoints;
using Snakk.Application.Services;
using Snakk.Shared.Helpers;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using WebOptimizer;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2 and Server Push
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

// Markup Parser (for rendering post content)
builder.Services.AddSingleton<IMarkupParser, MarkupParser>();

// WebOptimizer for CSS minification only (JS minification breaks TypeScript output)
builder.Services.AddWebOptimizer(pipeline =>
{
    // Minify all CSS files on-the-fly
    pipeline.MinifyCssFiles();
});

// Configure HttpClient for API with cookie forwarding
builder.Services.AddTransient<CookieForwardingHandler>();
builder.Services.AddHttpClient<SnakkApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5242");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CookieForwardingHandler>();

// Configure HttpClient for Internal API (for avatar proxy and other BFF endpoints)
builder.Services.AddHttpClient("InternalApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5242");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Enable response compression
app.UseResponseCompression();

// WebOptimizer middleware (must be before UseStaticFiles)
app.UseWebOptimizer();

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

// Serve avatars from configured storage path
var storagePath = Path.Combine(
    builder.Configuration["FileStorage:BasePath"] ?? "storage",
    "avatars"
);
var avatarsPath = Path.GetFullPath(storagePath);

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

// Resolve community from URL (must be before routing)
app.UseCommunityResolution();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

// BFF API endpoints
app.MapBffApiEndpoints();

app.Run();
