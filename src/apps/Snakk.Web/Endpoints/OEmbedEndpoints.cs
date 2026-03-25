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
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Results.NotFound();

        if (string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(501);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Results.NotFound();

        var path = uri.AbsolutePath.TrimEnd('/');
        var providerUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var defaultCommunitySlug = configuration["Snakk:DefaultCommunitySlug"] ?? "main";

        // Try each entity type in order of specificity (most segments first)
        return await TryDiscussion(path, url, providerUrl, apiClient)
            ?? await TrySpace(path, url, providerUrl, apiClient)
            ?? await TryHub(path, url, providerUrl, apiClient, defaultCommunitySlug)
            ?? await TryCommunity(path, url, providerUrl, apiClient)
            ?? await TryUserProfile(path, url, providerUrl, apiClient)
            ?? Results.NotFound();
    }

    // Discussion: /h/{hub}/{space}/{slug~id} or /c/{community}/h/{hub}/{space}/{slug~id}
    private static async Task<IResult?> TryDiscussion(
        string path, string url, string providerUrl, SnakkApiClient apiClient)
    {
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
        string path, string url, string providerUrl, SnakkApiClient apiClient)
    {
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
        string defaultCommunitySlug)
    {
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
        string path, string url, string providerUrl, SnakkApiClient apiClient)
    {
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
        string path, string url, string providerUrl, SnakkApiClient apiClient)
    {
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
        var response = new Dictionary<string, object?>
        {
            ["version"] = "1.0",
            ["type"] = "link",
            ["title"] = title,
            ["provider_name"] = "Snakk",
            ["provider_url"] = providerUrl,
            ["url"] = url,
        };

        if (description is not null)
            response["description"] = description;
        if (authorName is not null)
            response["author_name"] = authorName;
        if (authorUrl is not null)
            response["author_url"] = authorUrl;

        return Results.Json(response);
    }
}
