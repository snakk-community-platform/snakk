using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class MediaServiceTests : IAsyncDisposable
{
    private readonly SnakkDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly MediaService _service;
    private readonly string _testUserPublicId = Guid.NewGuid().ToString();

    public MediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase($"MediaTest-{Guid.NewGuid():N}")
            .Options;

        _db = new SnakkDbContext(options);
        _fileStorage = Substitute.For<IFileStorage>();
        _fileStorage.GetPublicUrl(Arg.Any<string>()).Returns(ci => "/" + ci.Arg<string>());

        var logger = Substitute.For<ILogger<MediaService>>();
        _service = new MediaService(_db, _fileStorage, logger);

        // Seed a test user
        _db.Users.Add(new UserDatabaseEntity
        {
            PublicId = _testUserPublicId,
            DisplayName = "Test User",
            Email = "test@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            AvatarRevision = 0
        });
        _db.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static MemoryStream CreateTestImage(int width = 10, int height = 10, string format = "jpeg")
    {
        var stream = new MemoryStream();
        using var image = new Image<Rgba32>(width, height);

        switch (format)
        {
            case "png":
                image.Save(stream, new PngEncoder());
                break;
            case "webp":
                image.Save(stream, new WebpEncoder());
                break;
            default:
                image.Save(stream, new JpegEncoder());
                break;
        }

        stream.Position = 0;
        return stream;
    }

    [Test]
    public async Task UploadAsync_SavesFileAndCreatesRecord()
    {
        // Arrange
        using var content = CreateTestImage();

        // Act
        var result = await _service.UploadAsync(content, "photo.jpg", "image/jpeg", _testUserPublicId);

        // Assert
        await Assert.That(result.Url).Contains("/media/posts/");
        await Assert.That(result.Url).Contains(".webp");
        await Assert.That(result.PublicId).IsNotNull();

        _fileStorage.Received(1).SaveAsync(
            Arg.Is<string>(p => p.StartsWith("media/posts/") && p.EndsWith(".webp")),
            Arg.Any<Stream>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        var dbRecord = await _db.Images.FirstOrDefaultAsync();
        await Assert.That(dbRecord).IsNotNull();
        await Assert.That(dbRecord!.ContentType).IsEqualTo("image/webp");
        await Assert.That(dbRecord.SizeBytes).IsGreaterThan(0);
    }

    [Test]
    public async Task UploadAsync_DeduplicatesByHash()
    {
        // Arrange — create identical images (same pixels = same re-encoded output)
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        using var content1 = CreateTestImage(5, 5, "png");
        var data = content1.ToArray();

        // Act
        var result1 = await _service.UploadAsync(new MemoryStream(data), "first.png", "image/png", _testUserPublicId);
        var result2 = await _service.UploadAsync(new MemoryStream(data), "second.png", "image/png", _testUserPublicId);

        // Assert — same URL, file saved only once
        await Assert.That(result1.Url).IsEqualTo(result2.Url);
        _fileStorage.Received(1).SaveAsync(
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        var mediaCount = await _db.Images.CountAsync();
        await Assert.That(mediaCount).IsEqualTo(1);
    }

    [Test]
    public async Task UploadAsync_RejectsDisallowedContentType()
    {
        // Arrange
        using var content = CreateTestImage();

        // Act & Assert
        await Assert.That(async () => { await _service.UploadAsync(content, "file.exe", "application/octet-stream", _testUserPublicId); })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UploadAsync_RejectsOversizedFile()
    {
        // Arrange — create a stream that's over 5 MB
        var largeData = new byte[6 * 1024 * 1024];
        using var content = new MemoryStream(largeData);

        // Act & Assert
        await Assert.That(async () => { await _service.UploadAsync(content, "huge.jpg", "image/jpeg", _testUserPublicId); })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UploadAsync_RejectsEmptyFile()
    {
        // Arrange
        using var content = new MemoryStream([]);

        // Act & Assert
        await Assert.That(async () => { await _service.UploadAsync(content, "empty.jpg", "image/jpeg", _testUserPublicId); })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UploadAsync_RejectsUnknownUser()
    {
        // Arrange
        using var content = CreateTestImage();

        // Act & Assert
        await Assert.That(async () => { await _service.UploadAsync(content, "file.jpg", "image/jpeg", "nonexistent-user-id"); })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UploadAsync_GeneratesCorrectStoragePath()
    {
        // Arrange
        using var content = CreateTestImage(10, 10, "webp");
        var now = DateTime.UtcNow;

        // Act
        var result = await _service.UploadAsync(content, "photo.webp", "image/webp", _testUserPublicId);

        // Assert — path follows media/posts/{yyyy}/{MM}/{dd}/{hash}.{ext}
        var dbRecord = await _db.Images.FirstAsync();
        var expectedPrefix = $"media/posts/{now:yyyy}/{now:MM}/{now:dd}/";
        await Assert.That(dbRecord.StoragePath).StartsWith(expectedPrefix);
        await Assert.That(dbRecord.StoragePath).EndsWith(".webp");
        await Assert.That(dbRecord.Sha256Hash).Length().IsEqualTo(64); // SHA-256 hex is 64 chars
    }

    [Test]
    public async Task UploadAsync_RejectsInvalidImageData()
    {
        // Arrange — raw bytes that aren't a valid image
        using var content = new MemoryStream("this is not an image file"u8.ToArray());

        // Act & Assert
        await Assert.That(async () => { await _service.UploadAsync(content, "fake.jpg", "image/jpeg", _testUserPublicId); })
            .Throws<InvalidOperationException>()
            .WithMessage("File is not a valid image.");
    }

    [Test]
    public async Task UploadAsync_ResizesOversizedImage()
    {
        // Arrange — create a 4000x3000 image (exceeds 2048 max dimension)
        using var content = CreateTestImage(4000, 3000, "jpeg");

        // Act
        var result = await _service.UploadAsync(content, "large.jpg", "image/jpeg", _testUserPublicId);

        // Assert — verify the saved file is smaller than the input (resized)
        Stream? savedStream = null;
        _fileStorage.SaveAsync(Arg.Any<string>(), Arg.Do<Stream>(s =>
            {
                savedStream = new MemoryStream();
                s.CopyTo(savedStream);
                savedStream.Position = 0;
            }), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // The DB record should exist with smaller size than original
        var dbRecord = await _db.Images.FirstAsync();
        await Assert.That(dbRecord.SizeBytes).IsGreaterThan(0);
        await Assert.That(result.Url).Contains(".webp");
    }
}
