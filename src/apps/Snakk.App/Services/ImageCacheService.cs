namespace Snakk.App.Services;

using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

public class ImageCacheService(IHttpClientFactory httpClientFactory, ILogger<ImageCacheService> logger)
{
    private readonly string _cacheDir = Path.Combine(FileSystem.CacheDirectory, "img");

    public async Task<string> GetLocalUriAsync(string url)
    {
        try
        {
            var cachePath = GetCachePath(url);
            if (!File.Exists(cachePath))
            {
                Directory.CreateDirectory(_cacheDir);
                var client = httpClientFactory.CreateClient("images");
                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(cachePath, bytes);
            }
            var data = await File.ReadAllBytesAsync(cachePath);
            return $"data:{GetMimeType(cachePath)};base64,{Convert.ToBase64String(data)}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image cache miss for {Url}", url);
            return url;
        }
    }

    private string GetCachePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var ext = GetExtension(url);
        return Path.Combine(_cacheDir, $"{hash}.{ext}");
    }

    private static string GetExtension(string url)
    {
        var path = url.Split('?')[0];
        var ext = Path.GetExtension(path).TrimStart('.').ToLower();
        return ext is "jpg" or "jpeg" or "png" or "gif" or "webp" or "avif" or "svg" ? ext : "jpg";
    }

    private static string GetMimeType(string cachePath) =>
        Path.GetExtension(cachePath).ToLower() switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            _ => "image/jpeg"
        };
}
