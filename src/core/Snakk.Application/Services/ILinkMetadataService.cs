namespace Snakk.Application.Services;

public record LinkMetadata(
    string? Title,
    string? Description,
    string? ImageUrl,
    string? Domain,
    string? OEmbedHtml,
    string? LocalImagePath,
    string? ImageBlurDataUri,
    bool IsInternal = false);

public interface ILinkMetadataService
{
    /// <summary>
    /// Fetches metadata from a URL by parsing OG tags and oEmbed.
    /// Returns null if the URL is unreachable or unparseable.
    /// </summary>
    Task<LinkMetadata?> FetchAsync(string url, CancellationToken cancellationToken = default);
}
