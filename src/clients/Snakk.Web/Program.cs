using Snakk.Web.Services;
using Snakk.Web.Filters;
using Snakk.Web.Middleware;
using Snakk.Web.Endpoints;
using Snakk.Application.Services;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using WebOptimizer;

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

// WebOptimizer for CSS/JS minification
builder.Services.AddWebOptimizer(pipeline =>
{
    // Minify all CSS files on-the-fly
    pipeline.MinifyCssFiles();

    // Minify all JS files on-the-fly
    pipeline.MinifyJsFiles();
});

// Output caching for pages
builder.Services.AddOutputCache(options =>
{
    // Base policy: cache anonymous requests only, vary by HTMX headers
    options.AddBasePolicy(builder => builder
        .With(c => !c.HttpContext.User.Identity?.IsAuthenticated ?? true)
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByHeader("HX-Request", "HX-Boosted"));

    // Home/Index pages - short cache
    options.AddPolicy("HomePage", builder => builder
        .With(c => !c.HttpContext.User.Identity?.IsAuthenticated ?? true)
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByHeader("HX-Request", "HX-Boosted")
        .Tag("home"));

    // Discussion lists - medium cache
    options.AddPolicy("DiscussionList", builder => builder
        .With(c => !c.HttpContext.User.Identity?.IsAuthenticated ?? true)
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByQuery("offset", "pageSize")
        .SetVaryByHeader("HX-Request", "HX-Boosted")
        .Tag("discussions"));

    // Individual discussions - short cache with query variation
    options.AddPolicy("DiscussionDetail", builder => builder
        .With(c => !c.HttpContext.User.Identity?.IsAuthenticated ?? true)
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByRouteValue("slugWithId")
        .SetVaryByQuery("offset")
        .SetVaryByHeader("HX-Request", "HX-Boosted")
        .Tag("discussions"));

    // Space/Hub pages - medium cache
    options.AddPolicy("Space", builder => builder
        .With(c => !c.HttpContext.User.Identity?.IsAuthenticated ?? true)
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByRouteValue("spaceSlug", "hubSlug")
        .SetVaryByHeader("HX-Request", "HX-Boosted")
        .Tag("spaces"));

    // Communities list - longer cache (changes rarely)
    options.AddPolicy("CommunitiesList", builder => builder
        .With(c => !c.HttpContext.User.Identity?.IsAuthenticated ?? true)
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByHeader("HX-Request", "HX-Boosted")
        .Tag("communities"));
});

// Configure HttpClient for API with cookie forwarding
builder.Services.AddTransient<CookieForwardingHandler>();
builder.Services.AddHttpClient<SnakkApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5242");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<CookieForwardingHandler>();

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

// Static files with caching
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

// Resolve community from URL (must be before routing)
app.UseCommunityResolution();

// HTTP/2 Server Push for critical resources
app.Use(async (context, next) =>
{
    // Only push on initial page loads (HTML requests)
    if (context.Request.Path == "/" ||
        context.Request.Path.StartsWithSegments("/discussions") ||
        context.Request.Path.StartsWithSegments("/spaces") ||
        context.Request.Path.StartsWithSegments("/hubs"))
    {
        // Push critical CSS
        context.Response.Headers.Append("Link", "</css/site.css>; rel=preload; as=style");

        // Push HTMX library
        context.Response.Headers.Append("Link", "<https://unpkg.com/htmx.org@2.0.4/dist/htmx.min.js>; rel=preload; as=script");
    }

    await next();
});

app.UseRouting();

// Output caching (after routing, before authorization)
app.UseOutputCache();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// BFF API endpoints
app.MapBffApiEndpoints();

app.Run();
