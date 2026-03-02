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

    public static async Task<bool> IsValidImageFileAsync(IFormFile file, string extension)
    {
        if (!_fileSignatures.TryGetValue(extension.ToLowerInvariant(), out var signatures))
            return false;

        using var stream = file.OpenReadStream();
        var headerBytes = new byte[8];
        var bytesRead = await stream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length));

        if (bytesRead < headerBytes.Length)
            return false;

        return signatures.Any(signature =>
            headerBytes
                .Take(signature.Length)
                .SequenceEqual(signature));
    }
}
