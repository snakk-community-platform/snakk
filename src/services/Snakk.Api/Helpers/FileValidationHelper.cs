namespace Snakk.Api.Helpers;

public static class FileValidationHelper
{
    private static readonly Dictionary<string, List<byte[]>> _fileSignatures = new()
    {
        {
            ".jpg",
            [
                [0xFF, 0xD8, 0xFF, 0xE0],
                [0xFF, 0xD8, 0xFF, 0xE1],
                [0xFF, 0xD8, 0xFF, 0xE8]
            ]
        },
        {
            ".jpeg",
            [
                [0xFF, 0xD8, 0xFF, 0xE0],
                [0xFF, 0xD8, 0xFF, 0xE1],
                [0xFF, 0xD8, 0xFF, 0xE8]
            ]
        },
        {
            ".png",
            [
                [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
            ]
        },
        {
            ".gif",
            [
                [0x47, 0x49, 0x46, 0x38, 0x37, 0x61],
                [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]
            ]
        },
        {
            ".webp",
            [
                [0x52, 0x49, 0x46, 0x46] // RIFF
            ]
        }
    };

    // WebP container signature: "WEBP" at byte offset 8 (after the RIFF header + size).
    private static readonly byte[] _webpFormat = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    public static async Task<bool> IsValidImageFileAsync(IFormFile file, string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (!_fileSignatures.TryGetValue(ext, out var signatures))
            return false;

        // Read 12 bytes so WebP's format marker at offset 8 can be checked.
        // ReadAtLeastAsync loops over short reads (a single ReadAsync may return
        // fewer bytes than available); throwOnEndOfStream:false returns what EOF
        // allowed so truncated files are detected by count, not by exception.
        using var stream = file.OpenReadStream();
        var headerBytes = new byte[12];
        var bytesRead = await stream.ReadAtLeastAsync(headerBytes, headerBytes.Length, throwOnEndOfStream: false);

        // No real image in the accepted set is shorter than 8 bytes — anything
        // smaller is truncated garbage even when the magic-byte prefix matches.
        if (bytesRead < 8)
            return false;

        var prefixMatches = signatures.Any(signature =>
            bytesRead >= signature.Length
            && headerBytes.Take(signature.Length).SequenceEqual(signature));

        if (!prefixMatches)
            return false;

        // RIFF is shared by WAV/AVI/WebP — require the "WEBP" format marker too.
        if (ext == ".webp")
            return bytesRead >= 12 && headerBytes.Skip(8).Take(4).SequenceEqual(_webpFormat);

        return true;
    }
}
