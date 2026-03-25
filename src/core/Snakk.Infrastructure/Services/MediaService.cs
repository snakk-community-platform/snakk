namespace Snakk.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class MediaService(
    SnakkDbContext db,
    IFileStorage fileStorage,
    IConfiguration configuration,
    ILogger<MediaService> logger) : IMediaService
{
    private readonly string _mediaUrlBase = configuration["FileStorage:MediaUrlBase"] ?? "/storage";
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxUploadsPerUserPerDay = 50;
    private const int MaxImageDimension = 2048;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private static readonly Dictionary<string, string> ContentTypeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/gif"] = "gif",
        ["image/webp"] = "webp"
    };

    public async Task<MediaUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string userPublicId,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException($"File type '{contentType}' is not allowed. Allowed types: {string.Join(", ", AllowedContentTypes)}");

        // Read the entire stream into memory for size check
        using var rawStream = new MemoryStream();
        await stream.CopyToAsync(rawStream, cancellationToken);

        if (rawStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        if (rawStream.Length == 0)
            throw new InvalidOperationException("File is empty.");

        // Re-encode image with ImageSharp — sanitizes embedded scripts, strips EXIF, resizes if needed
        rawStream.Position = 0;
        using var processedStream = new MemoryStream();

        try
        {
            using var image = await Image.LoadAsync(rawStream, cancellationToken);

            // Resize if either dimension exceeds the max
            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxImageDimension, MaxImageDimension)
                }));
            }

            // Re-encode in the original format (strips all metadata, produces a clean file)
            var encoder = contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => (SixLabors.ImageSharp.Formats.IImageEncoder)new JpegEncoder { Quality = 85 },
                "image/png" => new PngEncoder(),
                "image/gif" => new GifEncoder(),
                "image/webp" => new WebpEncoder { Quality = 80 },
                _ => new PngEncoder()
            };

            await image.SaveAsync(processedStream, encoder, cancellationToken);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidOperationException("File is not a valid image.");
        }

        // Compute SHA-256 hash of the processed (clean) image
        processedStream.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(processedStream, cancellationToken);
        var sha256Hash = Convert.ToHexStringLower(hashBytes);

        // Check for existing file with same hash (deduplication on processed content)
        var existing = await db.Media
            .FirstOrDefaultAsync(m => m.Sha256Hash == sha256Hash, cancellationToken);

        if (existing is not null)
        {
            // Verify the file still exists on disk — it may have been deleted manually
            if (!await fileStorage.ExistsAsync(existing.StoragePath, cancellationToken))
            {
                logger.LogWarning("Deduplicated record {Hash} exists but file missing at {Path}, re-saving", sha256Hash, existing.StoragePath);
                processedStream.Position = 0;
                await fileStorage.SaveAsync(existing.StoragePath, processedStream, cancellationToken);
            }
            else
            {
                logger.LogDebug("Deduplicated upload: {Hash} already exists at {Path}", sha256Hash, existing.StoragePath);
            }

            return new MediaUploadResult(existing.PublicId, GetMediaUrl(existing.StoragePath));
        }

        // Build storage path: media/posts/{yyyy}/{MM}/{dd}/{sha256}.{ext}
        var now = DateTime.UtcNow;
        var extension = ContentTypeToExtension.GetValueOrDefault(contentType, "bin");
        var storagePath = $"media/posts/{now:yyyy}/{now:MM}/{now:dd}/{sha256Hash}.{extension}";

        // Resolve uploader's internal user ID
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        // Per-user daily upload quota
        var dayAgo = DateTime.UtcNow.AddHours(-24);
        var recentUploadCount = await db.Media
            .CountAsync(m => m.UploadedByUserId == user.Id && m.CreatedAt >= dayAgo, cancellationToken);

        if (recentUploadCount >= MaxUploadsPerUserPerDay)
            throw new InvalidOperationException($"Daily upload limit reached. Maximum {MaxUploadsPerUserPerDay} images per day.");

        // Save processed file to storage
        processedStream.Position = 0;
        await fileStorage.SaveAsync(storagePath, processedStream, cancellationToken);

        // Create database record
        var media = new MediaDatabaseEntity
        {
            PublicId = Ulid.NewUlid().ToString(),
            Sha256Hash = sha256Hash,
            OriginalFileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = processedStream.Length,
            StoragePath = storagePath,
            CreatedAt = now,
            UploadedByUserId = user.Id
        };

        db.Media.Add(media);
        await db.SaveChangesAsync(cancellationToken);

        var publicUrl = GetMediaUrl(storagePath);
        logger.LogInformation("Media uploaded: {PublicId} ({Size} bytes) → {Path}", media.PublicId, media.SizeBytes, storagePath);

        return new MediaUploadResult(media.PublicId, publicUrl);
    }

    public async Task LinkMediaToPostAsync(
        string postPublicId,
        string content,
        CancellationToken cancellationToken = default)
    {
        // Find the post's internal ID
        var post = await db.Posts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == postPublicId, cancellationToken);

        if (post is null)
            return;

        // Extract storage paths from markdown image references
        // Matches patterns like: ![...](/storage/media/posts/2026/03/07/abc123.png)
        var urlPrefix = _mediaUrlBase.TrimEnd('/') + "/";
        var storagePaths = Regex.Matches(content, @"!\[.*?\]\(([^)]+)\)")
            .Select(m => m.Groups[1].Value)
            .Where(url => url.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(url => url[urlPrefix.Length..]) // Strip URL base to get storage path
            .ToList();

        // Look up media records by storage path
        var mediaRecords = storagePaths.Count > 0
            ? await db.Media
                .Where(m => storagePaths.Contains(m.StoragePath))
                .Select(m => new { m.Id })
                .ToListAsync(cancellationToken)
            : [];

        var newMediaIds = mediaRecords.Select(m => m.Id).ToHashSet();

        // Get existing links for this post
        var existingLinks = await db.PostMedia
            .Where(pm => pm.PostId == post.Id)
            .ToListAsync(cancellationToken);

        var existingMediaIds = existingLinks.Select(pm => pm.MediaId).ToHashSet();

        // Remove stale links (media no longer referenced in content)
        var toRemove = existingLinks.Where(pm => !newMediaIds.Contains(pm.MediaId)).ToList();
        if (toRemove.Count > 0)
            db.PostMedia.RemoveRange(toRemove);

        // Add new links
        var toAdd = newMediaIds.Except(existingMediaIds)
            .Select(mediaId => new PostMediaDatabaseEntity
            {
                PostId = post.Id,
                MediaId = mediaId
            })
            .ToList();

        if (toAdd.Count > 0)
            db.PostMedia.AddRange(toAdd);

        if (toRemove.Count > 0 || toAdd.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private string GetMediaUrl(string storagePath) =>
        $"{_mediaUrlBase.TrimEnd('/')}/{storagePath.Replace('\\', '/')}";
}
