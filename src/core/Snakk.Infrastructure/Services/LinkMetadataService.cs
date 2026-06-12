using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Snakk.Application.Services;

namespace Snakk.Infrastructure.Services;

public partial class LinkMetadataService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IFileStorage fileStorage,
    ILogger<LinkMetadataService> logger) : ILinkMetadataService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int MaxBodyBytes = 5_000_000; // 5MB — YouTube puts huge scripts before <meta> tags
    private const int MaxImageBytes = 5 * 1024 * 1024; // 5MB
    private const int MaxImageDimension = 800;
    private const int BlurSize = 20;

    public async Task<LinkMetadata?> FetchAsync(string url, string? languageCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        // Internal link detection — skip external fetch for our own URLs
        if (IsInternalUrl(url))
        {
            var uri = new Uri(url);
            return new LinkMetadata(
                Title: null, Description: null, ImageUrl: null,
                Domain: uri.Host, OEmbedHtml: null,
                ImagePath: null, ImageThumbnailPath: null, ImageBlurDataUri: null,
                IsInternal: true);
        }

        try
        {
            // Proxy mode: delegate fetching to an external service
            var proxyUrl = configuration["LinkMetadata:ProxyUrl"];
            if (!string.IsNullOrWhiteSpace(proxyUrl))
                return await FetchViaProxyAsync(url, proxyUrl, cancellationToken);

            var uri = new Uri(url);
            var domain = uri.Host.TrimStart('w', 'w', 'w', '.');

            // Fetch HTML and parse OG tags
            var html = await FetchHtmlAsync(url, languageCode, cancellationToken);

            var ogResult = html is not null ? ParseOgTags(html) : null;

            // Try oEmbed for known providers
            var oembedResult = await TryKnownOEmbedAsync(url, uri, cancellationToken);

            if (ogResult is null && oembedResult is null)
                return new LinkMetadata(null, null, null, domain, null, null, null, null);

            var metadata = new LinkMetadata(
                Title: oembedResult?.Title ?? ogResult?.Title,
                Description: ogResult?.Description,
                ImageUrl: oembedResult?.ThumbnailUrl ?? ogResult?.ImageUrl,
                Domain: domain,
                OEmbedHtml: oembedResult?.Html,
                ImageThumbnailPath: null,
                ImagePath: null,
                ImageBlurDataUri: null);

            // Download and process the preview image
            metadata = await ProcessPreviewImageAsync(metadata, cancellationToken);

            return metadata;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch link metadata for {Url}", url);
            return null;
        }
    }

    private async Task<LinkMetadata?> FetchViaProxyAsync(string url, string proxyUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Use the unblocked proxy client — proxyUrl is operator-configured, not user-supplied
            var client = httpClientFactory.CreateClient("LinkMetadataProxy");
            client.Timeout = Timeout;

            var payload = new { url };
            var response = await client.PostAsJsonAsync(proxyUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            var title = json.TryGetProperty("title", out var t) ? t.GetString() : null;
            var description = json.TryGetProperty("description", out var d) ? d.GetString() : null;
            var imageUrl = json.TryGetProperty("imageUrl", out var i) ? i.GetString() : null;
            var domain = json.TryGetProperty("domain", out var dm) ? dm.GetString() : null;
            var oembedHtml = json.TryGetProperty("oembedHtml", out var o) ? o.GetString() : null;

            var metadata = new LinkMetadata(title, description, imageUrl, domain, oembedHtml, null, null, null);

            // Download and process the preview image
            metadata = await ProcessPreviewImageAsync(metadata, cancellationToken);

            return metadata;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Proxy fetch failed for {Url}", url);
            return null;
        }
    }

    private async Task<LinkMetadata> ProcessPreviewImageAsync(LinkMetadata metadata, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadata.ImageUrl))
            return metadata;

        try
        {
            var imageBytes = await DownloadImageAsync(metadata.ImageUrl, cancellationToken);
            if (imageBytes is null)
                return metadata;

            using var image = Image.Load(new DecoderOptions { MaxFrames = 1 }, imageBytes);

            if (image.Width > 4096 || image.Height > 4096 || (long)image.Width * image.Height > 16_777_216)
                return metadata;

            // Resize to max 800px on longest side
            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxImageDimension, MaxImageDimension),
                    Mode = ResizeMode.Max
                }));
            }

            // Encode as WebP quality 80
            using var webpStream = new MemoryStream();
            await image.SaveAsWebpAsync(webpStream, new WebpEncoder { Quality = 80 }, cancellationToken);

            // Save to storage
            var storagePath = $"media/link-previews/{Ulid.NewUlid()}.webp";
            webpStream.Position = 0;
            await fileStorage.SaveAsync(storagePath, webpStream, "public, max-age=31536000, immutable", cancellationToken);

            // Generate thumbnail (400px max)
            string? thumbnailPath = null;
            try
            {
                if (image.Width > 400 || image.Height > 400)
                {
                    using var thumbImage = image.Clone(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(400, 400),
                        Mode = ResizeMode.Max
                    }));

                    thumbnailPath = $"media/link-previews/{Ulid.NewUlid()}_thumb.webp";
                    using var thumbStream = new MemoryStream();
                    await thumbImage.SaveAsWebpAsync(thumbStream, new WebpEncoder { Quality = 75 }, cancellationToken);
                    thumbStream.Position = 0;
                    await fileStorage.SaveAsync(thumbnailPath, thumbStream, "public, max-age=31536000, immutable", cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate link preview thumbnail");
            }

            // Generate blur data URI (20px WebP, base64)
            string? blurDataUri = null;
            try
            {
                using var blurImage = image.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(BlurSize, BlurSize),
                    Mode = ResizeMode.Max
                }));

                using var blurStream = new MemoryStream();
                await blurImage.SaveAsWebpAsync(blurStream, new WebpEncoder { Quality = 20 }, cancellationToken);
                blurDataUri = $"data:image/webp;base64,{Convert.ToBase64String(blurStream.ToArray())}";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate blur placeholder for link preview image");
            }

            return metadata with
            {
                ImagePath = storagePath,
                ImageThumbnailPath = thumbnailPath ?? storagePath, // fallback to full if image was already small
                ImageBlurDataUri = blurDataUri,
                ImageWidth = image.Width,
                ImageHeight = image.Height
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process link preview image from {ImageUrl}", metadata.ImageUrl);
            return metadata;
        }
    }

    private async Task<byte[]?> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("LinkMetadata");
            client.Timeout = TimeSpan.FromSeconds(10);

            using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            request.Headers.Add("User-Agent", "Snakk/1.0 (Link Preview)");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            // Check content type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;

            // Check content length
            if (response.Content.Headers.ContentLength > MaxImageBytes)
                return null;

            // Read with size limit
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            var buffer = new byte[8192];
            int totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > MaxImageBytes)
                    return null;

                memoryStream.Write(buffer, 0, bytesRead);
            }

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to download image from {ImageUrl}", imageUrl);
            return null;
        }
    }

    private async Task<string?> FetchHtmlAsync(string url, string? languageCode, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("LinkMetadata");
            client.Timeout = Timeout;

            var userAgent = configuration["LinkMetadata:UserAgent"]
                ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";

            var lang = languageCode ?? "en";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", userAgent);
            request.Headers.Add("Accept", "text/html");
            request.Headers.Add("Accept-Language", lang == "en" ? "en" : $"{lang}, en;q=0.5");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;

            // Read limited body — chunked to avoid a 5 MB LOH allocation per fetch
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            var buffer = new byte[8192];
            int totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > MaxBodyBytes)
                    break;

                memoryStream.Write(buffer, 0, bytesRead);
            }

            return System.Text.Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch HTML from {Url}", url);
            return null;
        }
    }

    private static OgTags? ParseOgTags(string html)
    {
        string? title = null;
        string? description = null;
        string? imageUrl = null;

        // Two-pass: match entire <meta> tag first, then extract attributes within it.
        // This handles sites (e.g. Imgur) that inject extra attributes like data-react-helmet
        // between the property and content attributes, which a single-pass regex can't handle.
        foreach (Match tag in MetaTagRegex().Matches(html))
        {
            var propMatch = MetaPropAttrRegex().Match(tag.Value);
            if (!propMatch.Success) continue;

            var contentMatch = MetaContentAttrRegex().Match(tag.Value);
            if (!contentMatch.Success) continue;

            var property = propMatch.Groups["prop"].Value.ToLowerInvariant();
            var content = System.Net.WebUtility.HtmlDecode(contentMatch.Groups["content"].Value);

            switch (property)
            {
                case "og:title": title ??= content; break;
                case "og:description": description ??= content; break;
                case "og:image": imageUrl ??= content; break;
                case "twitter:title": title ??= content; break;
                case "twitter:description": description ??= content; break;
                case "twitter:image": imageUrl ??= content; break;
                case "description": description ??= content; break;
            }
        }

        // Fallback: <title> tag
        if (title is null)
        {
            var titleMatch = TitleTagRegex().Match(html);
            if (titleMatch.Success)
                title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
        }

        if (title is null && description is null && imageUrl is null)
            return null;

        return new OgTags(title, description, imageUrl);
    }

    /// <summary>
    /// Try oEmbed for known whitelisted providers only.
    /// </summary>
    private async Task<OEmbedResult?> TryKnownOEmbedAsync(string url, Uri uri, CancellationToken cancellationToken)
    {
        var endpoint = GetKnownOEmbedEndpoint(url, uri);
        if (endpoint is null) return null;

        return await FetchOEmbedResponseAsync(endpoint, cancellationToken);
    }

    /// <summary>
    /// Known oEmbed providers — whitelisted by host, returns endpoint URL.
    /// </summary>
    private static string? GetKnownOEmbedEndpoint(string url, Uri uri)
    {
        var host = uri.Host.ToLowerInvariant().TrimStart('w', 'w', 'w', '.');
        var encodedUrl = Uri.EscapeDataString(url);

        return host switch
        {
            // Video
            "youtube.com" or "youtu.be"
                => $"https://www.youtube.com/oembed?url={encodedUrl}&format=json",
            "vimeo.com"
                => $"https://vimeo.com/api/oembed.json?url={encodedUrl}",
            "tiktok.com"
                => $"https://www.tiktok.com/oembed?url={encodedUrl}",

            // Social
            "twitter.com" or "x.com"
                => $"https://publish.twitter.com/oembed?url={encodedUrl}",
            "bsky.app"
                => $"https://embed.bsky.app/oembed?url={encodedUrl}&format=json",
            "reddit.com" or "old.reddit.com"
                => $"https://www.reddit.com/oembed?url={encodedUrl}",

            // Audio
            "open.spotify.com"
                => $"https://open.spotify.com/oembed?url={encodedUrl}",
            "soundcloud.com"
                => $"https://soundcloud.com/oembed?url={encodedUrl}&format=json",

            // Images
            "imgur.com" or "i.imgur.com"
                => $"https://api.imgur.com/oembed.json?url={encodedUrl}",

            // Streaming
            "twitch.tv"
                => $"https://api.twitch.tv/v5/oembed?url={encodedUrl}",

            // Music — includes artist subdomains (artist.bandcamp.com)
            _ when host == "bandcamp.com" || host.EndsWith(".bandcamp.com")
                => $"https://bandcamp.com/oembed?url={encodedUrl}&format=json",

            // Code / Design
            "codepen.io"
                => $"https://codepen.io/api/oembed?url={encodedUrl}&format=json",
            "canva.com"
                => $"https://www.canva.com/api/v1/oembed?url={encodedUrl}",

            _ => null
        };
    }

    private async Task<OEmbedResult?> FetchOEmbedResponseAsync(string oembedUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("LinkMetadata");
            client.Timeout = Timeout;

            var response = await client.GetFromJsonAsync<OEmbedResponse>(oembedUrl, cancellationToken);
            if (response is null) return null;

            return new OEmbedResult(
                Title: response.title,
                Html: response.html,
                ThumbnailUrl: response.thumbnail_url);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "oEmbed fetch failed for {Url}", oembedUrl);
            return null;
        }
    }

    // Two-pass meta tag parsing regex set.
    // Pass 1: capture the whole <meta ...> element so attribute order doesn't matter.
    [GeneratedRegex("""<meta\s[^>]+/?>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MetaTagRegex();

    // Pass 2a: extract property/name attribute value from within a matched tag.
    [GeneratedRegex("""\b(?:property|name)=["'](?<prop>[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaPropAttrRegex();

    // Pass 2b: extract content attribute value from within a matched tag.
    [GeneratedRegex("""\bcontent=["'](?<content>[^"']*)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaContentAttrRegex();

    [GeneratedRegex("""<title[^>]*>([^<]+)</title>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTagRegex();

    private bool IsInternalUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var host = uri.Host.ToLowerInvariant();

        // Check against configured domain
        var siteDomain = configuration["Snakk:Domain"]?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(siteDomain) && host == siteDomain)
            return true;

        // Check against all configured primary domains
        var primaryDomains = configuration.GetSection("Snakk:PrimaryDomains").Get<string[]>();
        if (primaryDomains is not null)
        {
            foreach (var d in primaryDomains)
            {
                if (host == d.ToLowerInvariant()) return true;
            }
        }

        // localhost is always internal (dev)
        if (host is "localhost" or "127.0.0.1") return true;

        return false;
    }

    private sealed record OgTags(string? Title, string? Description, string? ImageUrl);
    private sealed record OEmbedResult(string? Title, string? Html, string? ThumbnailUrl);

    // oEmbed JSON response (snake_case matches the standard)
    private sealed record OEmbedResponse(
        string? title,
        string? html,
        string? thumbnail_url);
}
