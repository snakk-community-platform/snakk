using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Snakk.Protos.Discussion;
using Snakk.Shared.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Endpoints;

public static class RssFeedEndpoints
{
    private const int CacheMinutes = 5;
    private static readonly MemoryCache FeedCache = new(new MemoryCacheOptions { SizeLimit = 200 });

    /// <summary>
    /// Unified feed item used by all three format builders.
    /// </summary>
    private sealed record FeedItem(
        string Title,
        string Url,
        string Id,
        DateTime PublishedUtc,
        string AuthorName,
        string? Category);

    /// <summary>
    /// Metadata shared by all feed formats.
    /// </summary>
    private sealed record FeedMeta(
        string Title,
        string Description,
        string SiteUrl,
        string BaseFeedPath);

    private enum FeedFormat { Rss, Atom, Json }

    public static void MapRssFeedEndpoints(this IEndpointRouteBuilder app)
    {
        // Site-wide feeds
        app.MapGet("/feed.xml", (Delegate)(async (HttpContext ctx) => await SiteWideFeedAsync(ctx, FeedFormat.Rss))).ExcludeFromDescription();
        app.MapGet("/feed.json", (Delegate)(async (HttpContext ctx) => await SiteWideFeedAsync(ctx, FeedFormat.Json))).ExcludeFromDescription();
        app.MapGet("/feed.atom", (Delegate)(async (HttpContext ctx) => await SiteWideFeedAsync(ctx, FeedFormat.Atom))).ExcludeFromDescription();

        // Community feeds
        app.MapGet("/c/{communitySlug}/feed.xml", (string communitySlug, HttpContext ctx) => CommunityFeedAsync(communitySlug, ctx, FeedFormat.Rss)).ExcludeFromDescription();
        app.MapGet("/c/{communitySlug}/feed.json", (string communitySlug, HttpContext ctx) => CommunityFeedAsync(communitySlug, ctx, FeedFormat.Json)).ExcludeFromDescription();
        app.MapGet("/c/{communitySlug}/feed.atom", (string communitySlug, HttpContext ctx) => CommunityFeedAsync(communitySlug, ctx, FeedFormat.Atom)).ExcludeFromDescription();

        // Hub feeds
        app.MapGet("/c/{communitySlug}/h/{hubSlug}/feed.xml", (string communitySlug, string hubSlug, HttpContext ctx) => HubFeedAsync(communitySlug, hubSlug, ctx, FeedFormat.Rss)).ExcludeFromDescription();
        app.MapGet("/c/{communitySlug}/h/{hubSlug}/feed.json", (string communitySlug, string hubSlug, HttpContext ctx) => HubFeedAsync(communitySlug, hubSlug, ctx, FeedFormat.Json)).ExcludeFromDescription();
        app.MapGet("/c/{communitySlug}/h/{hubSlug}/feed.atom", (string communitySlug, string hubSlug, HttpContext ctx) => HubFeedAsync(communitySlug, hubSlug, ctx, FeedFormat.Atom)).ExcludeFromDescription();

        // Space feeds
        app.MapGet("/c/{communitySlug}/h/{hubSlug}/{spaceSlug}/feed.xml", (string communitySlug, string hubSlug, string spaceSlug, HttpContext ctx) => SpaceFeedAsync(communitySlug, hubSlug, spaceSlug, ctx, FeedFormat.Rss)).ExcludeFromDescription();
        app.MapGet("/c/{communitySlug}/h/{hubSlug}/{spaceSlug}/feed.json", (string communitySlug, string hubSlug, string spaceSlug, HttpContext ctx) => SpaceFeedAsync(communitySlug, hubSlug, spaceSlug, ctx, FeedFormat.Json)).ExcludeFromDescription();
        app.MapGet("/c/{communitySlug}/h/{hubSlug}/{spaceSlug}/feed.atom", (string communitySlug, string hubSlug, string spaceSlug, HttpContext ctx) => SpaceFeedAsync(communitySlug, hubSlug, spaceSlug, ctx, FeedFormat.Atom)).ExcludeFromDescription();

        // Shortcut routes (single-community mode)
        app.MapGet("/h/{hubSlug}/feed.xml", (string hubSlug, HttpContext ctx) => HubFeedShortcutAsync(hubSlug, ctx, FeedFormat.Rss)).ExcludeFromDescription();
        app.MapGet("/h/{hubSlug}/feed.json", (string hubSlug, HttpContext ctx) => HubFeedShortcutAsync(hubSlug, ctx, FeedFormat.Json)).ExcludeFromDescription();
        app.MapGet("/h/{hubSlug}/feed.atom", (string hubSlug, HttpContext ctx) => HubFeedShortcutAsync(hubSlug, ctx, FeedFormat.Atom)).ExcludeFromDescription();

        app.MapGet("/h/{hubSlug}/{spaceSlug}/feed.xml", (string hubSlug, string spaceSlug, HttpContext ctx) => SpaceFeedShortcutAsync(hubSlug, spaceSlug, ctx, FeedFormat.Rss)).ExcludeFromDescription();
        app.MapGet("/h/{hubSlug}/{spaceSlug}/feed.json", (string hubSlug, string spaceSlug, HttpContext ctx) => SpaceFeedShortcutAsync(hubSlug, spaceSlug, ctx, FeedFormat.Json)).ExcludeFromDescription();
        app.MapGet("/h/{hubSlug}/{spaceSlug}/feed.atom", (string hubSlug, string spaceSlug, HttpContext ctx) => SpaceFeedShortcutAsync(hubSlug, spaceSlug, ctx, FeedFormat.Atom)).ExcludeFromDescription();

        // User feeds
        app.MapGet("/u/{encodedUserId}/feed.xml", (string encodedUserId, HttpContext ctx) => UserFeedAsync(encodedUserId, ctx, FeedFormat.Rss)).ExcludeFromDescription();
        app.MapGet("/u/{encodedUserId}/feed.json", (string encodedUserId, HttpContext ctx) => UserFeedAsync(encodedUserId, ctx, FeedFormat.Json)).ExcludeFromDescription();
        app.MapGet("/u/{encodedUserId}/feed.atom", (string encodedUserId, HttpContext ctx) => UserFeedAsync(encodedUserId, ctx, FeedFormat.Atom)).ExcludeFromDescription();
    }

    // ------------------------------------------------------------------
    //  Data-fetching handlers — fetch once, delegate to format builder
    // ------------------------------------------------------------------

    private static async Task<IResult> SiteWideFeedAsync(HttpContext httpContext, FeedFormat format)
    {
        var apiClient = httpContext.RequestServices.GetRequiredService<SnakkApiClient>();
        var community = httpContext.RequestServices.GetRequiredService<ICommunityContext>();
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

        var result = await apiClient.GetRecentDiscussionsAsync(offset: 0, pageSize: 30);
        if (result is null) return Results.StatusCode(503);

        var baseUrl = GetBaseUrl(httpContext, configuration);

        var meta = new FeedMeta(
            Title: "Latest Discussions",
            Description: "Recent discussions across the platform",
            SiteUrl: baseUrl,
            BaseFeedPath: $"{baseUrl}/feed");

        var items = MapRecentDiscussions(result.Items, baseUrl, community);
        return BuildFeed(meta, items, format, httpContext);
    }

    private static async Task<IResult> CommunityFeedAsync(
        string communitySlug, HttpContext httpContext, FeedFormat format)
    {
        var apiClient = httpContext.RequestServices.GetRequiredService<SnakkApiClient>();
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

        var communityInfo = await apiClient.GetCommunityBySlugAsync(communitySlug);
        if (communityInfo is null) return Results.NotFound();

        var result = await apiClient.GetRecentDiscussionsAsync(
            offset: 0, pageSize: 30, communityId: communityInfo.PublicId);
        if (result is null) return Results.StatusCode(503);

        var baseUrl = GetBaseUrl(httpContext, configuration);
        var community = httpContext.RequestServices.GetRequiredService<ICommunityContext>();

        var meta = new FeedMeta(
            Title: $"{communityInfo.Name} — Latest Discussions",
            Description: $"Recent discussions in {communityInfo.Name}",
            SiteUrl: $"{baseUrl}/c/{communitySlug}",
            BaseFeedPath: $"{baseUrl}/c/{communitySlug}/feed");

        var items = MapRecentDiscussions(result.Items, baseUrl, community);
        return BuildFeed(meta, items, format, httpContext);
    }

    private static async Task<IResult> HubFeedAsync(
        string communitySlug, string hubSlug, HttpContext httpContext, FeedFormat format)
    {
        var apiClient = httpContext.RequestServices.GetRequiredService<SnakkApiClient>();
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

        var hubInfo = await apiClient.GetHubBySlugAsync(hubSlug, communitySlug);
        if (hubInfo is null) return Results.NotFound();

        var result = await apiClient.GetRecentDiscussionsAsync(
            offset: 0, pageSize: 30, hubId: hubInfo.PublicId);
        if (result is null) return Results.StatusCode(503);

        var baseUrl = GetBaseUrl(httpContext, configuration);
        var community = httpContext.RequestServices.GetRequiredService<ICommunityContext>();
        var prefix = $"{baseUrl}/c/{communitySlug}";

        var meta = new FeedMeta(
            Title: $"{hubInfo.Name} — Latest Discussions",
            Description: $"Recent discussions in {hubInfo.Name}",
            SiteUrl: $"{prefix}/h/{hubSlug}",
            BaseFeedPath: $"{prefix}/h/{hubSlug}/feed");

        var items = MapRecentDiscussions(result.Items, baseUrl, community);
        return BuildFeed(meta, items, format, httpContext);
    }

    private static async Task<IResult> SpaceFeedAsync(
        string communitySlug, string hubSlug, string spaceSlug,
        HttpContext httpContext, FeedFormat format)
    {
        var apiClient = httpContext.RequestServices.GetRequiredService<SnakkApiClient>();
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

        var spaceInfo = await apiClient.GetSpaceBySlugAsync(spaceSlug, hubSlug);
        if (spaceInfo is null) return Results.NotFound();

        var result = await apiClient.GetDiscussionsBySpaceAsync(spaceInfo.PublicId, offset: 0, pageSize: 30);
        if (result is null) return Results.StatusCode(503);

        var baseUrl = GetBaseUrl(httpContext, configuration);
        var prefix = $"{baseUrl}/c/{communitySlug}";

        var meta = new FeedMeta(
            Title: $"{spaceInfo.Name} — Latest Discussions",
            Description: $"Recent discussions in {spaceInfo.Name}",
            SiteUrl: $"{prefix}/h/{hubSlug}/{spaceSlug}",
            BaseFeedPath: $"{prefix}/h/{hubSlug}/{spaceSlug}/feed");

        var items = MapSpaceDiscussions(result.Items, baseUrl, communitySlug, hubSlug, spaceSlug);
        return BuildFeed(meta, items, format, httpContext);
    }

    private static async Task<IResult> HubFeedShortcutAsync(
        string hubSlug, HttpContext httpContext, FeedFormat format)
    {
        var community = httpContext.RequestServices.GetRequiredService<ICommunityContext>();
        var communitySlug = community.CommunitySlug;
        if (string.IsNullOrEmpty(communitySlug)) return Results.NotFound();

        return await HubFeedAsync(communitySlug, hubSlug, httpContext, format);
    }

    private static async Task<IResult> SpaceFeedShortcutAsync(
        string hubSlug, string spaceSlug, HttpContext httpContext, FeedFormat format)
    {
        var community = httpContext.RequestServices.GetRequiredService<ICommunityContext>();
        var communitySlug = community.CommunitySlug;
        if (string.IsNullOrEmpty(communitySlug)) return Results.NotFound();

        return await SpaceFeedAsync(communitySlug, hubSlug, spaceSlug, httpContext, format);
    }

    private static async Task<IResult> UserFeedAsync(
        string encodedUserId, HttpContext httpContext, FeedFormat format)
    {
        var apiClient = httpContext.RequestServices.GetRequiredService<SnakkApiClient>();
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

        string userId;
        try { userId = UlidBase62.Decode(encodedUserId); }
        catch { return Results.NotFound(); }

        var profile = await apiClient.GetUserProfileResultAsync(userId);
        if (!profile.IsSuccess || profile.Value is null) return Results.NotFound();

        var result = await apiClient.SearchDiscussionsAsync(
            query: "", authorPublicId: userId, offset: 0, pageSize: 30);
        if (result is null) return Results.StatusCode(503);

        var baseUrl = GetBaseUrl(httpContext, configuration);
        var profileUrl = $"{baseUrl}/u/{encodedUserId}";

        var meta = new FeedMeta(
            Title: $"{profile.Value.DisplayName} — Discussions",
            Description: $"Recent discussions by {profile.Value.DisplayName}",
            SiteUrl: profileUrl,
            BaseFeedPath: $"{profileUrl}/feed");

        var items = result.Items.Select(d =>
        {
            var slugWithId = $"{d.Slug}~{UlidBase62.Encode(d.PublicId)}";
            var link = $"{baseUrl}/c/{d.CommunitySlug}/h/{d.Hub.Slug}/{d.Space.Slug}/{slugWithId}";

            return new FeedItem(
                Title: d.Title,
                Url: link,
                Id: link,
                PublishedUtc: d.CreatedAt.ToDateTime(),
                AuthorName: profile.Value.DisplayName,
                Category: d.Space.Name);
        }).ToList();

        return BuildFeed(meta, items, format, httpContext);
    }

    // ------------------------------------------------------------------
    //  Mapping helpers — convert proto types to unified FeedItem
    // ------------------------------------------------------------------

    private static List<FeedItem> MapRecentDiscussions(
        IEnumerable<RecentDiscussionInfo> discussions,
        string baseUrl,
        ICommunityContext community)
    {
        return discussions.Select(d =>
        {
            var slugWithId = $"{d.Slug}~{UlidBase62.Encode(d.PublicId)}";
            var communitySlug = d.Community?.Slug ?? community.CommunitySlug ?? "";
            var link = $"{baseUrl}/c/{communitySlug}/h/{d.Hub.Slug}/{d.Space.Slug}/{slugWithId}";

            return new FeedItem(
                Title: d.Title,
                Url: link,
                Id: link,
                PublishedUtc: d.CreatedAt.ToDateTime(),
                AuthorName: d.Author.DisplayName,
                Category: d.Space.Name);
        }).ToList();
    }

    private static List<FeedItem> MapSpaceDiscussions(
        IEnumerable<DiscussionBySpaceInfo> discussions,
        string baseUrl,
        string communitySlug,
        string hubSlug,
        string spaceSlug)
    {
        return discussions.Select(d =>
        {
            var slugWithId = $"{d.Slug}~{UlidBase62.Encode(d.PublicId)}";
            var link = $"{baseUrl}/c/{communitySlug}/h/{hubSlug}/{spaceSlug}/{slugWithId}";

            return new FeedItem(
                Title: d.Title,
                Url: link,
                Id: link,
                PublishedUtc: d.CreatedAt.ToDateTime(),
                AuthorName: d.Author.DisplayName,
                Category: null);
        }).ToList();
    }

    // ------------------------------------------------------------------
    //  Format dispatcher
    // ------------------------------------------------------------------

    private static IResult BuildFeed(
        FeedMeta meta, List<FeedItem> items, FeedFormat format, HttpContext httpContext)
    {
        var cacheKey = httpContext.Request.Path.Value ?? "";

        if (FeedCache.TryGetValue(cacheKey, out CachedFeed? cached))
            return FeedResult(cached!.Content, cached.ContentType, httpContext);

        var result = format switch
        {
            FeedFormat.Rss => RenderFeed(meta, items, BuildRss, "application/rss+xml"),
            FeedFormat.Atom => RenderFeed(meta, items, BuildAtom, "application/atom+xml"),
            FeedFormat.Json => RenderFeed(meta, items, BuildJsonFeed, "application/feed+json"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        FeedCache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheMinutes),
            Size = 1
        });

        return FeedResult(result.Content, result.ContentType, httpContext);
    }

    private sealed record CachedFeed(string Content, string ContentType);

    private static CachedFeed RenderFeed(
        FeedMeta meta,
        List<FeedItem> items,
        Func<FeedMeta, List<FeedItem>, string> builder,
        string contentType) =>
        new(builder(meta, items), contentType);

    // ------------------------------------------------------------------
    //  RSS 2.0 builder
    // ------------------------------------------------------------------

    private static string BuildRss(FeedMeta meta, List<FeedItem> items)
    {
        var feedUrl = $"{meta.BaseFeedPath}.xml";
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">");
        xml.AppendLine("  <channel>");
        xml.AppendLine($"    <title>{Xml(meta.Title)}</title>");
        xml.AppendLine($"    <link>{Xml(meta.SiteUrl)}</link>");
        xml.AppendLine($"    <description>{Xml(meta.Description)}</description>");
        xml.AppendLine($"    <atom:link href=\"{Xml(feedUrl)}\" rel=\"self\" type=\"application/rss+xml\"/>");
        xml.AppendLine("    <language>en</language>");

        foreach (var item in items)
        {
            xml.AppendLine("    <item>");
            xml.AppendLine($"      <title>{Xml(item.Title)}</title>");
            xml.AppendLine($"      <link>{Xml(item.Url)}</link>");
            xml.AppendLine($"      <guid isPermaLink=\"true\">{Xml(item.Id)}</guid>");
            xml.AppendLine($"      <pubDate>{item.PublishedUtc.ToString("R")}</pubDate>");
            xml.AppendLine($"      <author>{Xml(item.AuthorName)}</author>");

            if (item.Category is not null)
                xml.AppendLine($"      <category>{Xml(item.Category)}</category>");

            xml.AppendLine("    </item>");
        }

        xml.AppendLine("  </channel>");
        xml.AppendLine("</rss>");

        return xml.ToString();
    }

    // ------------------------------------------------------------------
    //  Atom 1.0 builder
    // ------------------------------------------------------------------

    private static string BuildAtom(FeedMeta meta, List<FeedItem> items)
    {
        var feedUrl = $"{meta.BaseFeedPath}.atom";
        var updated = items.Count > 0
            ? items.Max(i => i.PublishedUtc).ToString("O")
            : DateTime.UtcNow.ToString("O");

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<feed xmlns=\"http://www.w3.org/2005/Atom\">");
        xml.AppendLine($"  <title>{Xml(meta.Title)}</title>");
        xml.AppendLine($"  <link href=\"{Xml(feedUrl)}\" rel=\"self\" type=\"application/atom+xml\"/>");
        xml.AppendLine($"  <link href=\"{Xml(meta.SiteUrl)}\" rel=\"alternate\"/>");
        xml.AppendLine($"  <id>{Xml(meta.SiteUrl)}</id>");
        xml.AppendLine($"  <updated>{updated}</updated>");

        foreach (var item in items)
        {
            xml.AppendLine("  <entry>");
            xml.AppendLine($"    <title>{Xml(item.Title)}</title>");
            xml.AppendLine($"    <link href=\"{Xml(item.Url)}\"/>");
            xml.AppendLine($"    <id>{Xml(item.Id)}</id>");
            xml.AppendLine($"    <published>{item.PublishedUtc.ToString("O")}</published>");
            xml.AppendLine($"    <updated>{item.PublishedUtc.ToString("O")}</updated>");
            xml.AppendLine($"    <author><name>{Xml(item.AuthorName)}</name></author>");

            if (item.Category is not null)
                xml.AppendLine($"    <category term=\"{Xml(item.Category)}\"/>");

            xml.AppendLine("  </entry>");
        }

        xml.AppendLine("</feed>");

        return xml.ToString();
    }

    // ------------------------------------------------------------------
    //  JSON Feed 1.1 builder
    // ------------------------------------------------------------------

    private static string BuildJsonFeed(FeedMeta meta, List<FeedItem> items)
    {
        var feedUrl = $"{meta.BaseFeedPath}.json";

        var jsonItems = items.Select(item =>
        {
            var obj = new Dictionary<string, object>
            {
                ["id"] = item.Id,
                ["url"] = item.Url,
                ["title"] = item.Title,
                ["date_published"] = item.PublishedUtc.ToString("O"),
                ["authors"] = new[] { new Dictionary<string, string> { ["name"] = item.AuthorName } }
            };

            if (item.Category is not null)
                obj["tags"] = new[] { item.Category };

            return obj;
        }).ToList();

        var feed = new Dictionary<string, object>
        {
            ["version"] = "https://jsonfeed.org/version/1.1",
            ["title"] = meta.Title,
            ["home_page_url"] = meta.SiteUrl,
            ["feed_url"] = feedUrl,
            ["items"] = jsonItems
        };

        var json = JsonSerializer.Serialize(feed, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null // keep snake_case keys as-is
        });

        return json;
    }

    // ------------------------------------------------------------------
    //  Shared utilities
    // ------------------------------------------------------------------

    private static string GetBaseUrl(HttpContext httpContext, IConfiguration configuration) =>
        configuration["WebBaseUrl"] ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

    private static IResult FeedResult(string content, string contentType, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = $"public, max-age={CacheMinutes * 60}";
        httpContext.Response.Headers.ContentDisposition = "inline";
        return Results.Content(content, contentType, Encoding.UTF8);
    }

    private static string Xml(string value) =>
        string.IsNullOrEmpty(value) ? value : value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
}
