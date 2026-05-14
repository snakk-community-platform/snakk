namespace Snakk.Application.Services;

public record MediaUploadResult(string PublicId, string Url, string? ThumbnailUrl, string? MediumThumbnailUrl, string? BlurDataUri);

/// <summary>
/// Authoritative render data for a media image, fetched from the Image table.
/// All URL fields are null when the variant was not generated (e.g. image too small).
/// </summary>
public record ImageRenderData(string? ThumbUrl, string? MedUrl, string? BlurDataUri, int? Width = null, int? Height = null);

public interface IMediaService
{
    /// <summary>
    /// Uploads a media file, deduplicating by SHA-256 hash.
    /// Returns the public URL for the stored file.
    /// </summary>
    /// <param name="stream">File content stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="contentType">MIME type (e.g., "image/png")</param>
    /// <param name="userPublicId">Public ID of the uploading user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MediaUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string userPublicId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a draft media file (main + thumbnail) by its public URL.
    /// Only deletes if the media is still a draft (not published).
    /// </summary>
    Task<bool> DeleteDraftAsync(string mediaUrl, string userPublicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses media URLs from post content and creates PostMedia links.
    /// Removes stale links when content is edited.
    /// </summary>
    /// <param name="postPublicId">Public ID of the post</param>
    /// <param name="content">Markdown content to scan for media URLs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LinkMediaToPostAsync(
        string postPublicId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark all draft media referenced in the content as published.
    /// Only publishes drafts uploaded by the specified user.
    /// Call after a discussion or post is successfully created.
    /// </summary>
    Task PublishDraftMediaAsync(string content, string userPublicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete expired draft media (files + database records).
    /// Returns the number of drafts cleaned up.
    /// </summary>
    Task<int> CleanupExpiredDraftsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans raw markdown content for Snakk media image URLs and returns authoritative
    /// render data (thumbnail URLs, medium URL, blur data URI) for each from the Image table.
    /// Used to enrich RenderedContent with accurate srcset and LQIP placeholders at write time.
    /// </summary>
    Task<IReadOnlyDictionary<string, ImageRenderData>> GetImageRenderDataFromContentAsync(
        string content,
        CancellationToken cancellationToken = default);
}
