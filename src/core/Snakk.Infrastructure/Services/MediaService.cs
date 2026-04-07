namespace Snakk.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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
    private readonly string _mediaUrlBase = configuration["FileStorage:MediaUrlBase"] ?? "";
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxUploadsPerUserPerDay = 50;
    private const int MaxImageDimension = 2048;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
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
            using var image = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, rawStream, cancellationToken);

            // Guard against decompression bombs
            const int MaxPixelDimension = 16384;
            const long MaxTotalPixels = 100_000_000;
            if (image.Width > MaxPixelDimension || image.Height > MaxPixelDimension)
                throw new InvalidOperationException($"Image dimensions exceed maximum ({MaxPixelDimension}x{MaxPixelDimension})");
            if ((long)image.Width * image.Height > MaxTotalPixels)
                throw new InvalidOperationException("Image exceeds maximum pixel count");

            // Resize if either dimension exceeds the max
            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxImageDimension, MaxImageDimension)
                }));
            }

            // Always re-encode as WebP for smallest file size (strips all metadata)
            var encoder = new WebpEncoder { Quality = 80 };

            await image.SaveAsync(processedStream, encoder, cancellationToken);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidOperationException("File is not a valid image.");
        }

        // Compute SHA-256 hash of the processed (clean) image for deduplication
        processedStream.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(processedStream, cancellationToken);
        var sha256Hash = Convert.ToHexStringLower(hashBytes);

        // Check for existing file with same hash (deduplication)
        var existing = await db.Images
            .FirstOrDefaultAsync(m => m.Sha256Hash == sha256Hash && !m.IsDeleted, cancellationToken);

        if (existing is not null)
        {
            if (!await fileStorage.ExistsAsync(existing.StoragePath, cancellationToken))
            {
                logger.LogWarning("Deduplicated record {Hash} exists but file missing at {Path}, re-saving", sha256Hash, existing.StoragePath);
                processedStream.Position = 0;
                await fileStorage.SaveAsync(existing.StoragePath, processedStream, "public, max-age=31536000, immutable", cancellationToken);
            }
            else
            {
                logger.LogDebug("Deduplicated upload: {Hash} already exists at {Path}", sha256Hash, existing.StoragePath);
            }

            return new MediaUploadResult(
                existing.PublicId,
                GetMediaUrl(existing.StoragePath),
                existing.ThumbnailPath is not null ? GetMediaUrl(existing.ThumbnailPath) : null,
                existing.MediumThumbnailPath is not null ? GetMediaUrl(existing.MediumThumbnailPath) : null,
                existing.BlurDataUri);
        }

        // Build storage path using PublicId for short, clean URLs
        var now = DateTime.UtcNow;
        var publicId = Ulid.NewUlid().ToString();
        var storagePath = $"media/posts/{now:yyyy}/{now:MM}/{now:dd}/{publicId}.webp";

        // Resolve uploader's internal user ID
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == userPublicId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        // Per-user daily upload quota
        var dayAgo = DateTime.UtcNow.AddHours(-24);
        var recentUploadCount = await db.Images
            .CountAsync(m => m.UploadedByUserId == user.Id && m.CreatedAt >= dayAgo, cancellationToken);

        if (recentUploadCount >= MaxUploadsPerUserPerDay)
            throw new InvalidOperationException($"Daily upload limit reached. Maximum {MaxUploadsPerUserPerDay} images per day.");

        // Save processed file to storage
        processedStream.Position = 0;
        await fileStorage.SaveAsync(storagePath, processedStream, "public, max-age=31536000, immutable", cancellationToken);

        // Generate thumbnails (300px small, 600px medium)
        string? thumbnailPath = null;
        string? mediumThumbnailPath = null;
        try
        {
            processedStream.Position = 0;
            using var thumbImage = await Image.LoadAsync(processedStream, cancellationToken);

            // Medium thumbnail (600px) — for carousels and larger previews
            if (thumbImage.Width > 600 || thumbImage.Height > 600)
            {
                using var medImage = thumbImage.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(600, 600),
                    Mode = ResizeMode.Max
                }));

                mediumThumbnailPath = $"media/posts/{now:yyyy}/{now:MM}/{now:dd}/{publicId}_med.webp";
                using var medStream = new MemoryStream();
                await medImage.SaveAsWebpAsync(medStream, new WebpEncoder { Quality = 75 }, cancellationToken);
                medStream.Position = 0;
                await fileStorage.SaveAsync(mediumThumbnailPath, medStream, "public, max-age=31536000, immutable", cancellationToken);
            }

            // Small thumbnail (300px) — for grid items and small previews
            if (thumbImage.Width > 300 || thumbImage.Height > 300)
            {
                thumbImage.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }));

                thumbnailPath = $"media/posts/{now:yyyy}/{now:MM}/{now:dd}/{publicId}_thumb.webp";
                using var thumbStream = new MemoryStream();
                await thumbImage.SaveAsWebpAsync(thumbStream, new WebpEncoder { Quality = 75 }, cancellationToken);
                thumbStream.Position = 0;
                await fileStorage.SaveAsync(thumbnailPath, thumbStream, "public, max-age=31536000, immutable", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate thumbnails for {Hash}", sha256Hash);
        }

        // Generate blur placeholder (~20px, base64 data URI)
        string? blurDataUri = null;
        try
        {
            processedStream.Position = 0;
            using var blurImage = await Image.LoadAsync(processedStream, cancellationToken);
            blurImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(20, 20),
                Mode = ResizeMode.Max
            }));

            using var blurStream = new MemoryStream();
            await blurImage.SaveAsWebpAsync(blurStream, new WebpEncoder { Quality = 20 }, cancellationToken);
            blurDataUri = $"data:image/webp;base64,{Convert.ToBase64String(blurStream.ToArray())}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate blur placeholder for {Hash}", sha256Hash);
        }

        // Create database record — uploads start as drafts until linked to a discussion
        var media = new ImageDatabaseEntity
        {
            PublicId = publicId,
            Sha256Hash = sha256Hash,
            OriginalFileName = Path.GetFileName(fileName),
            ContentType = "image/webp",
            SizeBytes = processedStream.Length,
            StoragePath = storagePath,
            ThumbnailPath = thumbnailPath,
            MediumThumbnailPath = mediumThumbnailPath,
            BlurDataUri = blurDataUri,
            CreatedAt = now,
            UploadedByUserId = user.Id,
            IsDraft = true
        };

        db.Images.Add(media);
        await db.SaveChangesAsync(cancellationToken);

        var publicUrl = GetMediaUrl(storagePath);
        var thumbnailUrl = thumbnailPath is not null ? GetMediaUrl(thumbnailPath) : null;
        var mediumThumbnailUrl = mediumThumbnailPath is not null ? GetMediaUrl(mediumThumbnailPath) : null;
        logger.LogInformation("Media uploaded: {PublicId} ({Size} bytes) → {Path}", media.PublicId, media.SizeBytes, storagePath);

        return new MediaUploadResult(media.PublicId, publicUrl, thumbnailUrl, mediumThumbnailUrl, blurDataUri);
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
        // Matches patterns like: ![...](/media/posts/2026/03/07/abc123.png)
        var urlPrefix = _mediaUrlBase.TrimEnd('/') + "/";
        var storagePaths = Regex.Matches(content, @"!\[.*?\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromMilliseconds(250))
            .Select(m => m.Groups[1].Value)
            .Where(url => url.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(url => url[urlPrefix.Length..]) // Strip URL base to get storage path
            .ToList();

        // Look up media records by storage path
        var mediaRecords = storagePaths.Count > 0
            ? await db.Images
                .Where(m => storagePaths.Contains(m.StoragePath))
                .Select(m => new { m.Id })
                .ToListAsync(cancellationToken)
            : [];

        var newMediaIds = mediaRecords.Select(m => m.Id).ToHashSet();

        // Get existing links for this post
        var existingLinks = await db.PostImages
            .Where(pm => pm.PostId == post.Id)
            .ToListAsync(cancellationToken);

        var existingMediaIds = existingLinks.Select(pm => pm.ImageId).ToHashSet();

        // Remove stale links (media no longer referenced in content)
        var toRemove = existingLinks.Where(pm => !newMediaIds.Contains(pm.ImageId)).ToList();
        if (toRemove.Count > 0)
            db.PostImages.RemoveRange(toRemove);

        // Add new links
        var toAdd = newMediaIds.Except(existingMediaIds)
            .Select(mediaId => new PostImageDatabaseEntity
            {
                PostId = post.Id,
                ImageId = mediaId
            })
            .ToList();

        if (toAdd.Count > 0)
            db.PostImages.AddRange(toAdd);

        if (toRemove.Count > 0 || toAdd.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mark all draft media referenced in the given content as published.
    /// Call after a discussion or post is successfully created.
    /// </summary>
    public async Task<bool> DeleteDraftAsync(string mediaUrl, string userPublicId, CancellationToken cancellationToken = default)
    {
        // Extract storage path from URL
        var urlPrefix = _mediaUrlBase.TrimEnd('/') + "/";
        if (!mediaUrl.StartsWith(urlPrefix)) return false;
        var storagePath = mediaUrl[urlPrefix.Length..];

        var media = await db.Images
            .AsTracking()
            .FirstOrDefaultAsync(m => m.StoragePath == storagePath && m.IsDraft, cancellationToken);

        if (media is null) return false;

        // Verify ownership
        var userId = await db.Users
            .Where(u => u.PublicId == userPublicId && !u.IsDeleted)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (userId == 0 || media.UploadedByUserId != userId) return false;

        // Mark for deletion — cleanup worker will remove files on next run
        media.IsReadyForDeletion = true;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Draft media marked for deletion: {PublicId} ({Path})", media.PublicId, media.StoragePath);
        return true;
    }

    public async Task PublishDraftMediaAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content)) return;

        var urlPrefix = _mediaUrlBase.TrimEnd('/') + "/";
        var storagePaths = Regex.Matches(content, @"!\[.*?\]\(([^)]+)\)", RegexOptions.None, TimeSpan.FromMilliseconds(250))
            .Select(m => m.Groups[1].Value)
            .Where(url => url.StartsWith(urlPrefix))
            .Select(url => url[urlPrefix.Length..])
            .ToList();

        if (storagePaths.Count == 0) return;

        var now = DateTime.UtcNow;
        await db.Images
            .Where(m => m.IsDraft && !m.IsDeleted && storagePaths.Contains(m.StoragePath))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsDraft, false)
                .SetProperty(m => m.PublishedAt, now),
                cancellationToken);
    }

    /// <summary>
    /// Process media marked for deletion + stale drafts.
    /// Step 1: Mark stale drafts (12h+ unpublished) as IsReadyForDeletion.
    /// Step 2: Delete files for IsReadyForDeletion records, then set IsDeleted.
    /// Called by background cleanup worker.
    /// </summary>
    public async Task<int> CleanupExpiredDraftsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-12);

        // Step 1: Mark stale unpublished drafts as ready for deletion
        await db.Images
            .Where(m => m.IsDraft && !m.IsReadyForDeletion && !m.IsDeleted && m.CreatedAt < cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadyForDeletion, true), cancellationToken);

        // Step 2: Find all records ready for deletion (user-deleted + stale drafts)
        var toDelete = await db.Images
            .AsTracking()
            .Where(m => m.IsReadyForDeletion && !m.IsDeleted)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (toDelete.Count == 0) return 0;

        // Delete files from storage (main + thumbnail)
        foreach (var media in toDelete)
        {
            try
            {
                await fileStorage.DeleteAsync(media.StoragePath, cancellationToken);
                if (media.ThumbnailPath is not null)
                    await fileStorage.DeleteAsync(media.ThumbnailPath, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete media file: {Path}", media.StoragePath);
            }

            media.IsDeleted = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cleaned up {Count} media files", toDelete.Count);
        return toDelete.Count;
    }

    private string GetMediaUrl(string storagePath) =>
        $"{_mediaUrlBase.TrimEnd('/')}/{storagePath.Replace('\\', '/')}";
}
