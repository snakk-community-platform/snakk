using Microsoft.Extensions.Configuration;
using Snakk.Application.Services;

namespace Snakk.Infrastructure.Services;

/// <summary>
/// Local file system implementation of IFileStorage.
/// Stores files in a configurable directory on the local file system.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private readonly string _resolvedBasePath;
    private readonly string _publicUrlBase;

    public LocalFileStorage(IConfiguration configuration)
    {
        _basePath = configuration["FileStorage:BasePath"]
            ?? throw new InvalidOperationException("FileStorage:BasePath is not configured");
        _resolvedBasePath = Path.GetFullPath(_basePath);
        _publicUrlBase = configuration["FileStorage:PublicUrlBase"] ?? "";

        // Ensure the base directory exists
        if (!Directory.Exists(_resolvedBasePath))
            Directory.CreateDirectory(_resolvedBasePath);
    }

    /// <summary>
    /// Resolves a relative path to a full path within the base directory.
    /// Throws if the resolved path escapes the base directory (path traversal prevention).
    /// </summary>
    private string ResolveSafePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));

        if (!fullPath.StartsWith(_resolvedBasePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path traversal detected — resolved path is outside the storage directory.", nameof(relativePath));

        return fullPath;
    }

    public async Task SaveAsync(
        string relativePath,
        Stream content,
        string? cacheControl = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory is not null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    private const long MaxReadSize = 50 * 1024 * 1024; // 50 MB safety limit

    public async Task<Stream?> ReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(relativePath);

        if (!File.Exists(fullPath))
            return null;

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxReadSize)
            throw new InvalidOperationException($"File exceeds maximum read size of {MaxReadSize / (1024 * 1024)} MB: {relativePath}");

        var memoryStream = new MemoryStream((int)fileInfo.Length);
        using var fileStream = File.OpenRead(fullPath);
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public Task<bool> ExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafePath(relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        // Normalize path separators for URLs (always use forward slashes)
        var normalizedPath = relativePath.Replace('\\', '/');
        return $"/{normalizedPath}";
    }
}
