namespace Snakk.Application.Services;

public record LinkMetadata(
    string? Title,
    string? Description,
    string? ImageUrl,
    string? Domain,
    string? OEmbedHtml,
    string? ImagePath,
    string? ImageThumbnailPath,
    string? ImageBlurDataUri,
    bool IsInternal = false);

public interface ILinkMetadataService
{
    /// <summary>
    /// Fetches metadata from a URL by parsing OG tags and oEmbed.
    /// Returns null if the URL is unreachable or unparseable.
    /// </summary>
    /// <param name="url">The URL to fetch metadata from</param>
    /// <param name="languageCode">ISO 639-1 language code for Accept-Language header (default: "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<LinkMetadata?> FetchAsync(string url, string? languageCode = null, CancellationToken cancellationToken = default);
}
