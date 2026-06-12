namespace Snakk.Web.Endpoints;

using System.Text.RegularExpressions;
using Snakk.Shared.Helpers;
using Snakk.Web.Services;

public static class OEmbedEndpoints
{
    public static void MapOEmbedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/oembed", HandleOEmbedAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> HandleOEmbedAsync(
        string? url,
        string? format,
        SnakkApiClient apiClient,
        IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(url))
            return Results.NotFound();

        if (string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(501);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Results.NotFound();

        // SSRF / host-injection guard: only embed our own canonical host over http(s).
        // Without this, an arbitrary URL (internal services, cloud metadata, file://, etc.)
        // would be parsed and echoed back into the oEmbed response.
        var expectedHost = httpContext.Request.Host.Host;
        if (!string.IsNullOrEmpty(configuration["WebBaseUrl"])
            && Uri.TryCreate(configuration["WebBaseUrl"], UriKind.Absolute, out var baseUri))
            expectedHost = baseUri.Host;
        if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();

        var path = uri.AbsolutePath.TrimEnd('/');
        var providerUrl = configuration["WebBaseUrl"]?.TrimEnd('/') ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var defaultCommunitySlug = configuration["Snakk:DefaultCommunitySlug"] ?? "main";

        // Try each entity type in order of specificity (most segments first)
        return await TryDiscussion(path, url, providerUrl, apiClient, ct)
            ?? await TrySpace(path, url, providerUrl, apiClient, ct)
            ?? await TryHub(path, url, providerUrl, apiClient, defaultCommunitySlug, ct)
            ?? await TryCommunity(path, url, providerUrl, apiClient, ct)
            ?? await TryUserProfile(path, url, providerUrl, apiClient, ct)
            ?? Results.NotFound();
    }

    // Discussion: /h/{hub}/{space}/{slug~id} or /c/{community}/h/{hub}/{space}/{slug~id}
    private static async Task<IResult?> TryDiscussion(
        string path, string url, string providerUrl, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var match = Regex.Match(path, @"^(?:/c/[^/]+)?/h/[^/]+/[^/]+/([^/]+~[^/]+)$");
        if (!match.Success)
            return null;

        var slugWithId = match.Groups[1].Value;
        var parts = slugWithId.Split('~');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[1]))
            return null;

        var publicId = UlidBase62.Decode(parts[1]);

        var discussion = await apiClient.GetDiscussionAsync(publicId);
        if (discussion is null)
            return null;

        var posts = await apiClient.GetDiscussionPostsAsync(publicId, 0, 1);
        var firstPost = posts?.Items?.FirstOrDefault();

        var description = firstPost?.Content is { Length: > 0 } content
            ? (content.Length > 200 ? content[..200] + "..." : content)
            : null;

        return OEmbedResult(
            title: discussion.Title,
            url: url,
            providerUrl: providerUrl,
            description: description,
            authorName: firstPost?.Author?.DisplayName,
            authorUrl: firstPost?.Author?.PublicId is { Length: > 0 } authorId
                ? $"{providerUrl}/u/{authorId}"
                : null);
    }

    // Space: /h/{hub}/{space} or /c/{community}/h/{hub}/{space}
    private static async Task<IResult?> TrySpace(
        string path, string url, string providerUrl, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var match = Regex.Match(path, @"^(?:/c/[^/]+)?/h/([^/]+)/([^/]+)$");
        if (!match.Success)
            return null;

        var hubSlug = match.Groups[1].Value;
        var spaceSlug = match.Groups[2].Value;

        // Skip if it looks like a discussion slug~id
        if (spaceSlug.Contains('~'))
            return null;

        var space = await apiClient.GetSpaceBySlugAsync(spaceSlug, hubSlug);
        if (space is null || space.IsRestricted)
            return null;

        return OEmbedResult(
            title: space.Name,
            url: url,
            providerUrl: providerUrl,
            description: space.Description is { Length: > 0 } desc
                ? (desc.Length > 200 ? desc[..200] + "..." : desc)
                : null);
    }

    // Hub: /h/{hub} or /c/{community}/h/{hub}
    private static async Task<IResult?> TryHub(
        string path, string url, string providerUrl, SnakkApiClient apiClient,
        string defaultCommunitySlug, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var match = Regex.Match(path, @"^(?:/c/([^/]+))?/h/([^/]+)$");
        if (!match.Success)
            return null;

        var communitySlug = match.Groups[1].Value;
        var hubSlug = match.Groups[2].Value;

        // Fall back to default community when URL has no /c/ prefix
        if (string.IsNullOrEmpty(communitySlug))
            communitySlug = defaultCommunitySlug;

        var hub = await apiClient.GetHubBySlugAsync(hubSlug, communitySlug);
        if (hub is null || hub.IsRestricted)
            return null;

        return OEmbedResult(
            title: hub.Name,
            url: url,
            providerUrl: providerUrl,
            description: hub.Description is { Length: > 0 } desc
                ? (desc.Length > 200 ? desc[..200] + "..." : desc)
                : null);
    }

    // Community: /c/{slug}
    private static async Task<IResult?> TryCommunity(
        string path, string url, string providerUrl, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var match = Regex.Match(path, @"^/c/([^/]+)$");
        if (!match.Success)
            return null;

        var slug = match.Groups[1].Value;
        var community = await apiClient.GetCommunityBySlugAsync(slug);
        if (community is null || community.IsRestricted)
            return null;

        return OEmbedResult(
            title: community.Name,
            url: url,
            providerUrl: providerUrl,
            description: community.Description is { Length: > 0 } desc
                ? (desc.Length > 200 ? desc[..200] + "..." : desc)
                : null);
    }

    // User profile: /u/{publicId}
    private static async Task<IResult?> TryUserProfile(
        string path, string url, string providerUrl, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var match = Regex.Match(path, @"^/u/([^/]+)$");
        if (!match.Success)
            return null;

        var publicId = match.Groups[1].Value;
        var profile = await apiClient.GetUserProfileAsync(publicId);
        if (profile is null)
            return null;

        var description = $"{profile.DiscussionCount} discussions, {profile.PostCount} posts";

        return OEmbedResult(
            title: profile.DisplayName,
            url: url,
            providerUrl: providerUrl,
            description: description,
            authorName: profile.DisplayName,
            authorUrl: url);
    }

    private static IResult OEmbedResult(
        string title,
        string url,
        string providerUrl,
        string? description = null,
        string? authorName = null,
        string? authorUrl = null)
    {
        var thumbnailUrl = $"{providerUrl}/img/logo-square.webp";
        const int thumbnailSize = 512;

        var html = BuildEmbedHtml(title, url, providerUrl, thumbnailUrl, description, authorName, authorUrl);

        var response = new Dictionary<string, object?>
        {
            ["version"] = "1.0",
            ["type"] = "rich",
            ["title"] = title,
            ["provider_name"] = "Snakk",
            ["provider_url"] = providerUrl,
            ["url"] = url,
            ["thumbnail_url"] = thumbnailUrl,
            ["thumbnail_width"] = thumbnailSize,
            ["thumbnail_height"] = thumbnailSize,
            ["html"] = html,
            ["width"] = 480,
            ["height"] = 160,
        };

        if (description is not null)
            response["description"] = description;
        if (authorName is not null)
            response["author_name"] = authorName;
        if (authorUrl is not null)
            response["author_url"] = authorUrl;

        return Results.Json(response);
    }

    private static string BuildEmbedHtml(
        string title,
        string url,
        string providerUrl,
        string thumbnailUrl,
        string? description,
        string? authorName,
        string? authorUrl)
    {
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        var safeUrl = System.Net.WebUtility.HtmlEncode(url);
        var safeThumb = System.Net.WebUtility.HtmlEncode(thumbnailUrl);
        var safeProvider = System.Net.WebUtility.HtmlEncode(providerUrl);
        var safeDescription = description is not null ? System.Net.WebUtility.HtmlEncode(description) : null;
        var safeAuthor = authorName is not null ? System.Net.WebUtility.HtmlEncode(authorName) : null;

        var descriptionHtml = safeDescription is not null
            ? $"<div style=\"font-size:13px;color:#6b7280;line-height:1.5;margin-top:6px;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;\">{safeDescription}</div>"
            : "";

        var authorHtml = safeAuthor is not null
            ? $"<div style=\"font-size:11px;color:#9ca3af;margin-top:8px;\">by {safeAuthor}</div>"
            : "";

        return
            $"<div style=\"box-sizing:border-box;max-width:480px;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#fff;\">" +
                $"<a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" style=\"display:flex;gap:14px;padding:14px;text-decoration:none;color:inherit;\">" +
                    $"<img src=\"{safeThumb}\" alt=\"Snakk\" width=\"64\" height=\"64\" style=\"width:64px;height:64px;border-radius:8px;flex-shrink:0;object-fit:cover;\" />" +
                    "<div style=\"flex:1;min-width:0;\">" +
                        $"<div style=\"font-size:15px;font-weight:600;color:#111827;line-height:1.3;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;\">{safeTitle}</div>" +
                        descriptionHtml +
                        authorHtml +
                        $"<div style=\"font-size:11px;color:#9ca3af;margin-top:8px;text-transform:uppercase;letter-spacing:0.04em;\">{safeProvider.Replace("https://", "").Replace("http://", "")}</div>" +
                    "</div>" +
                "</a>" +
            "</div>";
    }
}
