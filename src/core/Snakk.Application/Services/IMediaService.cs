namespace Snakk.Application.Services;

public record MediaUploadResult(string PublicId, string Url);

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
    /// Call after a discussion or post is successfully created.
    /// </summary>
    Task PublishDraftMediaAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete expired draft media (files + database records).
    /// Returns the number of drafts cleaned up.
    /// </summary>
    Task<int> CleanupExpiredDraftsAsync(CancellationToken cancellationToken = default);
}
