namespace Snakk.Web.Middleware;

using Snakk.Web.Services;

/// <summary>
/// Resolves the current community from the request.
///
/// Single-community mode (IsMultiCommunityEnabled = false):
///   All URLs are flat (/h/..., /rules, etc.) — no /c/ prefix.
///   For /h/... paths, the middleware prepends /c/{defaultSlug} so the
///   Razor Pages routes (which all use /c/{slug}/h/...) match correctly.
///
/// Multi-community mode (IsMultiCommunityEnabled = true):
///   1. Custom domain → community resolved from Host header, no path changes.
///   2. /c/{slug}/... → community extracted from path, no rewriting.
///   3. /h/... without /c/ prefix → redirect to / (invalid in multi-community).
///   4. Everything else (/, /rules, /search, etc.) → no community context.
/// </summary>
public class CommunityResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _singleCommunitySlug;
    private readonly bool _isMultiCommunity;

    public CommunityResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _singleCommunitySlug = configuration["Snakk:DefaultCommunitySlug"] ?? "main";
        _isMultiCommunity = configuration.GetValue<bool>("Features:MultiCommunityEnabled");
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICommunityContext communityContext,
        ICommunityDomainCacheService domainCache)
    {
        var path = context.Request.Path.Value ?? "";
        var host = context.Request.Host.Host;

        // Single-community: flat URLs, no /c/ prefix in the browser.
        // Prepend /c/{defaultSlug} for /h/... paths so they hit the
        // /c/{communitySlug}/h/... Razor Pages routes.
        if (!_isMultiCommunity)
        {
            communityContext.SetCommunity(_singleCommunitySlug, isMultiCommunity: false);
            if (path.StartsWith("/h/", StringComparison.OrdinalIgnoreCase))
                context.Request.Path = $"/c/{_singleCommunitySlug}{path}";
            await _next(context);
            return;
        }

        // Multi-community: Step 1 — custom domain resolution
        var domainLookup = await domainCache.GetCommunitySlugForDomainAsync(host);
        if (domainLookup.Found && domainLookup.CommunitySlug is not null)
        {
            communityContext.SetCommunity(
                domainLookup.CommunitySlug,
                isCustomDomain: true,
                name: domainLookup.CommunityName,
                isMultiCommunity: true,
                timezone: domainLookup.Timezone);
            await _next(context);
            return;
        }

        // Multi-community: Step 2 — /c/{slug}/... path resolution (no rewriting)
        if (path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) && path.Length > 3)
        {
            var remainingPath = path[3..]; // skip "/c/"
            var slashIndex = remainingPath.IndexOf('/');

            if (slashIndex > 0)
                communityContext.SetCommunity(remainingPath[..slashIndex], isMultiCommunity: true);
            else
                communityContext.SetCommunity(remainingPath.TrimEnd('/'), isMultiCommunity: true);

            await _next(context);
            return;
        }

        // Multi-community: Step 3 — bare /h/... is not valid, redirect to /
        if (path.StartsWith("/h/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/", permanent: false);
            return;
        }

        // Multi-community: site-level pages (/, /rules, /search, etc.)
        communityContext.SetCommunity("", isMultiCommunity: true);
        await _next(context);
    }
}

public static class CommunityResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseCommunityResolution(this IApplicationBuilder builder) =>
        builder.UseMiddleware<CommunityResolutionMiddleware>();
}
