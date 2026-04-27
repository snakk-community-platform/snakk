namespace Snakk.Web.Middleware;

using Snakk.Web.Services;

/// <summary>
/// Resolves the current community from the request.
///
/// Single-community mode (IsMultiCommunityEnabled = false):
///   Canonical user-facing URLs are /c (community detail) and /h/... (hubs, spaces, discussions).
///   /h/...             → internal rewrite to /c/{defaultSlug}/h/... (Razor Pages route)
///   /c or /c/          → internal rewrite to /c/{defaultSlug}       (community detail route)
///   /c/{defaultSlug}[/...] → 301 redirect to canonical form (/c or /{rest})
///   /c/{anything-else} → 404
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

        if (!_isMultiCommunity)
        {
            communityContext.SetCommunity(_singleCommunitySlug, isMultiCommunity: false);

            if (path.StartsWith("/h/", StringComparison.OrdinalIgnoreCase))
            {
                // /h/... → internal rewrite so the /c/{slug}/h/... Razor routes match
                context.Request.Path = $"/c/{_singleCommunitySlug}{path}";
            }
            else if (path.Equals("/c", StringComparison.OrdinalIgnoreCase)
                  || path.Equals("/c/", StringComparison.OrdinalIgnoreCase))
            {
                // Bare /c → internal rewrite to community detail route
                context.Request.Path = $"/c/{_singleCommunitySlug}";
            }
            else if (path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase))
            {
                var afterPrefix = path[3..]; // skip "/c/"
                var slashIndex = afterPrefix.IndexOf('/');
                var firstSegment = slashIndex >= 0 ? afterPrefix[..slashIndex] : afterPrefix;

                if (firstSegment.Equals(_singleCommunitySlug, StringComparison.OrdinalIgnoreCase))
                {
                    // /c/{defaultSlug}[/...] → 301 to canonical form
                    var rest = path.Length > 3 + _singleCommunitySlug.Length
                        ? path[(3 + _singleCommunitySlug.Length)..]
                        : "";
                    var canonical = rest.Length == 0 ? "/c" : rest;
                    context.Response.Redirect(canonical + context.Request.QueryString.ToString(), permanent: true);
                    return;
                }

                // /c/{other} → 404
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

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
