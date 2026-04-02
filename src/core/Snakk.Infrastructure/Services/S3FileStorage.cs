namespace Snakk.Infrastructure.Services;

using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Snakk.Application.Services;

/// <summary>
/// S3-compatible file storage implementation.
/// Works with AWS S3, Cloudflare R2, MinIO, and other S3-compatible services.
/// </summary>
public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrlBase;

    public S3FileStorage(IConfiguration configuration)
    {
        var endpoint = configuration["FileStorage:S3:Endpoint"]
            ?? throw new InvalidOperationException("FileStorage:S3:Endpoint is not configured");
        var accessKey = configuration["FileStorage:S3:AccessKey"]
            ?? throw new InvalidOperationException("FileStorage:S3:AccessKey is not configured");
        var secretKey = configuration["FileStorage:S3:SecretKey"]
            ?? throw new InvalidOperationException("FileStorage:S3:SecretKey is not configured");

        _bucketName = configuration["FileStorage:S3:BucketName"]
            ?? throw new InvalidOperationException("FileStorage:S3:BucketName is not configured");
        _publicUrlBase = configuration["FileStorage:S3:PublicUrlBase"]
            ?? throw new InvalidOperationException("FileStorage:S3:PublicUrlBase is not configured");

        _s3Client = new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true
        });
    }

    public async Task SaveAsync(
        string relativePath,
        Stream content,
        string? cacheControl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        var key = NormalizeKey(relativePath);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = GetContentType(key),
            DisablePayloadSigning = true // Required for R2
        };

        if (!string.IsNullOrEmpty(cacheControl))
            request.Headers.CacheControl = cacheControl;

        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    private const long MaxReadSize = 50 * 1024 * 1024; // 50 MB safety limit

    public async Task<Stream?> ReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, NormalizeKey(relativePath), cancellationToken);

            if (response.ContentLength > MaxReadSize)
                throw new InvalidOperationException($"S3 object exceeds maximum read size of {MaxReadSize / (1024 * 1024)} MB: {relativePath}");

            var memoryStream = new MemoryStream((int)Math.Min(response.ContentLength, MaxReadSize));
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucketName, NormalizeKey(relativePath), cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        await _s3Client.DeleteObjectAsync(_bucketName, NormalizeKey(relativePath), cancellationToken);
    }

    public string GetPublicUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty", nameof(relativePath));

        return $"{_publicUrlBase.TrimEnd('/')}/{NormalizeKey(relativePath)}";
    }

    private static string NormalizeKey(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string GetContentType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
}
