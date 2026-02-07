namespace Snakk.Application.Services;

/// <summary>
/// Abstraction for file storage operations.
/// Allows switching between local storage (development) and cloud storage (production) without changing business logic.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a file to storage
    /// </summary>
    /// <param name="relativePath">Relative path from storage root (e.g., "avatars/generated/users/u_123.svg")</param>
    /// <param name="content">Stream containing the file content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a file from storage
    /// </summary>
    /// <param name="relativePath">Relative path from storage root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing the file content, or null if file doesn't exist</returns>
    Task<Stream?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists in storage
    /// </summary>
    /// <param name="relativePath">Relative path from storage root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage
    /// </summary>
    /// <param name="relativePath">Relative path from storage root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the public URL for accessing a file
    /// </summary>
    /// <param name="relativePath">Relative path from storage root</param>
    /// <returns>Public URL that can be used in HTML (e.g., "/storage/avatars/generated/users/u_123.svg")</returns>
    string GetPublicUrl(string relativePath);
}
