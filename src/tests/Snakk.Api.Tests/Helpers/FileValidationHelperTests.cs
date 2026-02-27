using Microsoft.AspNetCore.Http;
using Snakk.Api.Helpers;

namespace Snakk.Api.Tests.Helpers;

public class FileValidationHelperTests
{
    private static IFormFile CreateFormFile(byte[] content, string fileName)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    /// <summary>
    /// Creates a byte array with the given magic bytes padded to at least 8 bytes.
    /// </summary>
    private static byte[] CreateFileBytes(params byte[] magicBytes)
    {
        if (magicBytes.Length >= 8)
            return magicBytes;

        var result = new byte[8];
        Array.Copy(magicBytes, result, magicBytes.Length);
        return result;
    }

    [Test]
    public async Task ValidJpegE0_ReturnsTrue()
    {
        var bytes = CreateFileBytes(0xFF, 0xD8, 0xFF, 0xE0);
        var file = CreateFormFile(bytes, "photo.jpg");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".jpg");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidJpegE1_ReturnsTrue()
    {
        var bytes = CreateFileBytes(0xFF, 0xD8, 0xFF, 0xE1);
        var file = CreateFormFile(bytes, "photo.jpeg");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".jpeg");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidJpegE8_ReturnsTrue()
    {
        var bytes = CreateFileBytes(0xFF, 0xD8, 0xFF, 0xE8);
        var file = CreateFormFile(bytes, "photo.jpg");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".jpg");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidPng_ReturnsTrue()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var file = CreateFormFile(bytes, "image.png");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".png");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidGif87a_ReturnsTrue()
    {
        var bytes = CreateFileBytes(0x47, 0x49, 0x46, 0x38, 0x37, 0x61);
        var file = CreateFormFile(bytes, "image.gif");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".gif");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidGif89a_ReturnsTrue()
    {
        var bytes = CreateFileBytes(0x47, 0x49, 0x46, 0x38, 0x39, 0x61);
        var file = CreateFormFile(bytes, "image.gif");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".gif");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ValidWebp_ReturnsTrue()
    {
        var bytes = CreateFileBytes(0x52, 0x49, 0x46, 0x46);
        var file = CreateFormFile(bytes, "image.webp");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".webp");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task MismatchedExtension_ReturnsFalse()
    {
        // PNG magic bytes but .jpg extension
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var file = CreateFormFile(bytes, "fake.jpg");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".jpg");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UnknownExtension_ReturnsFalse()
    {
        var bytes = CreateFileBytes(0x42, 0x4D); // BMP magic bytes
        var file = CreateFormFile(bytes, "image.bmp");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".bmp");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TruncatedFile_ReturnsFalse()
    {
        // Only 4 bytes, less than the required 8 byte header read
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var file = CreateFormFile(bytes, "truncated.jpg");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".jpg");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task EmptyFile_ReturnsFalse()
    {
        var bytes = Array.Empty<byte>();
        var file = CreateFormFile(bytes, "empty.jpg");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".jpg");

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task CaseInsensitiveExtension_ReturnsTrue()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var file = CreateFormFile(bytes, "image.PNG");

        var result = await FileValidationHelper.IsValidImageFileAsync(file, ".PNG");

        await Assert.That(result).IsTrue();
    }
}
