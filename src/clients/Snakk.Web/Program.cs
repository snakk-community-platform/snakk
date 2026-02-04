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

// Output caching disabled - JWT auth uses localStorage which can't be checked server-side
// Caching authenticated pages as anonymous causes logout issues
// builder.Services.AddOutputCache();

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
    // Push critical resources on all page loads
    context.Response.Headers.Append("Link", "</css/tailwind.css>; rel=preload; as=style");
    context.Response.Headers.Append("Link", "</css/site.css>; rel=preload; as=style");
    context.Response.Headers.Append("Link", "</js/theme.js>; rel=preload; as=script");
    context.Response.Headers.Append("Link", "</js/htmx.min.js>; rel=preload; as=script");
    context.Response.Headers.Append("Link", "</js/auth.js>; rel=preload; as=script");
    context.Response.Headers.Append("Link", "</js/search-focus.js>; rel=preload; as=script");
    context.Response.Headers.Append("Link", "</js/sidebar-scrollbar.js>; rel=preload; as=script");

    await next();
});

app.UseRouting();

// Output caching disabled - causes issues with localStorage-based JWT auth
// app.UseOutputCache();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// BFF API endpoints
app.MapBffApiEndpoints();

app.Run();
