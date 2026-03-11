namespace Snakk.Web.Endpoints;

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
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Results.NotFound();

        if (string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(501);

        // Extract publicId from last URL path segment (format: {slug}~{publicId})
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Results.NotFound();

        var lastSegment = uri.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(lastSegment))
            return Results.NotFound();

        var parts = lastSegment.Split('~');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[1]))
            return Results.NotFound();

        var publicId = parts[1];

        var discussion = await apiClient.GetDiscussionAsync(publicId);
        if (discussion is null)
            return Results.NotFound();

        var posts = await apiClient.GetDiscussionPostsAsync(publicId, 0, 1);
        var firstPost = posts?.Items?.FirstOrDefault();

        var providerUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var description = firstPost?.Content is { Length: > 0 } content
            ? (content.Length > 200 ? content[..200] : content)
            : (string?)null;

        var response = new
        {
            version = "1.0",
            type = "link",
            title = discussion.Title,
            author_name = firstPost?.Author?.DisplayName ?? "Anonymous",
            author_url = firstPost?.Author?.PublicId is { Length: > 0 } authorId
                ? $"{providerUrl}/u/{authorId}"
                : null,
            provider_name = "Snakk",
            provider_url = providerUrl,
            url,
            description,
        };

        return Results.Json(response);
    }
}
