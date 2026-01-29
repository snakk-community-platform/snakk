namespace Snakk.Web.Middleware;

using Snakk.Web.Services;

/// <summary>
/// Middleware that resolves the current community from the request.
/// Resolution order:
/// 1. Custom domain: Check Host header against domain-to-community mapping
/// 2. Path-based: URLs starting with /c/{community}/... extract the community and rewrite to /...
/// 3. Default: URLs without community context use the default community
/// </summary>
public class CommunityResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _defaultCommunitySlug;

    public CommunityResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _defaultCommunitySlug = configuration["Snakk:DefaultCommunitySlug"] ?? "main";
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICommunityContext communityContext,
        ICommunityDomainCacheService domainCache)
    {
        var path = context.Request.Path.Value ?? "";
        var host = context.Request.Host.Host;

        // Step 1: Check for custom domain
        var domainLookup = await domainCache.GetCommunitySlugForDomainAsync(host);
        if (domainLookup.Found && domainLookup.CommunitySlug != null)
        {
            // Custom domain resolved - set community context with name
            var isDefault = domainLookup.CommunitySlug.Equals(_defaultCommunitySlug, StringComparison.OrdinalIgnoreCase);
            communityContext.SetCommunity(domainLookup.CommunitySlug, isDefault, isCustomDomain: true, name: domainLookup.CommunityName);

            // No path rewriting needed for custom domains
            await _next(context);
            return;
        }

        // Step 2: Check if URL starts with /c/{community}/
        if (path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) && path.Length > 3)
        {
            // Extract community slug and remaining path
            var remainingPath = path[3..]; // Remove "/c/"
            var slashIndex = remainingPath.IndexOf('/');

            if (slashIndex > 0)
            {
                var communitySlug = remainingPath[..slashIndex];
                var newPath = remainingPath[slashIndex..]; // Keep the /h/... part

                // Set community context
                var isDefault = communitySlug.Equals(_defaultCommunitySlug, StringComparison.OrdinalIgnoreCase);
                communityContext.SetCommunity(communitySlug, isDefault);

                // Rewrite path to remove /c/{community} prefix
                context.Request.Path = newPath;
            }
            else
            {
                // Just /c/{community} with no trailing path - this goes to community detail page
                // Don't rewrite, let the /c/{slug} page handle it
                communityContext.SetCommunity(remainingPath.TrimEnd('/'),
                    remainingPath.TrimEnd('/').Equals(_defaultCommunitySlug, StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            // Step 3: No /c/ prefix - use default community
            communityContext.SetCommunity(_defaultCommunitySlug, isDefault: true);
        }

        await _next(context);
    }
}

public static class CommunityResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseCommunityResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CommunityResolutionMiddleware>();
    }
}
