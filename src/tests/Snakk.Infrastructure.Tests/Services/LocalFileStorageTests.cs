using Microsoft.Extensions.Configuration;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"snakk-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = _tempDir,
                ["FileStorage:PublicUrlBase"] = "/storage"
            })
            .Build();

        _storage = new LocalFileStorage(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Constructor Tests

    [Test]
    public async Task Constructor_ThrowsInvalidOperationException_WhenBasePathMissing()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert
        await Assert.That(() => new LocalFileStorage(config)).Throws<InvalidOperationException>();
    }

    #endregion

    #region SaveAsync Tests

    [Test]
    public async Task SaveAsync_FileExistsAfterSave()
    {
        // Arrange
        var content = new MemoryStream("hello world"u8.ToArray());

        // Act
        await _storage.SaveAsync("test-file.txt", content);

        // Assert
        var fullPath = Path.Combine(_tempDir, "test-file.txt");
        await Assert.That(File.Exists(fullPath)).IsTrue();
    }

    [Test]
    public async Task SaveAsync_CreatesSubdirectories()
    {
        // Arrange
        var content = new MemoryStream("nested content"u8.ToArray());

        // Act
        await _storage.SaveAsync("subdir/nested/test-file.txt", content);

        // Assert
        var fullPath = Path.Combine(_tempDir, "subdir", "nested", "test-file.txt");
        await Assert.That(File.Exists(fullPath)).IsTrue();
    }

    [Test]
    public async Task SaveAsync_ThrowsForEmptyPath()
    {
        // Arrange
        var content = new MemoryStream("data"u8.ToArray());

        // Act & Assert
        await Assert.That(() => _storage.SaveAsync("", content)).Throws<ArgumentException>();
    }

    #endregion

    #region ReadAsync Tests

    [Test]
    public async Task ReadAsync_ReturnsContentAfterSave()
    {
        // Arrange
        var originalContent = "file content here"u8.ToArray();
        await _storage.SaveAsync("read-test.txt", new MemoryStream(originalContent));

        // Act
        var result = await _storage.ReadAsync("read-test.txt");

        // Assert
        await Assert.That(result).IsNotNull();
        using var reader = new StreamReader(result!);
        var text = await reader.ReadToEndAsync();
        await Assert.That(text).IsEqualTo("file content here");
    }

    [Test]
    public async Task ReadAsync_ReturnsNull_ForNonExistentFile()
    {
        // Act
        var result = await _storage.ReadAsync("does-not-exist.txt");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ReadAsync_ThrowsForEmptyPath()
    {
        // Act & Assert
        await Assert.That(() => _storage.ReadAsync("")).Throws<ArgumentException>();
    }

    #endregion

    #region ExistsAsync Tests

    [Test]
    public async Task ExistsAsync_ReturnsTrue_AfterSave()
    {
        // Arrange
        await _storage.SaveAsync("exists-test.txt", new MemoryStream("data"u8.ToArray()));

        // Act
        var result = await _storage.ExistsAsync("exists-test.txt");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ExistsAsync_ReturnsFalse_BeforeSave()
    {
        // Act
        var result = await _storage.ExistsAsync("not-yet-saved.txt");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ExistsAsync_ThrowsForEmptyPath()
    {
        // Act & Assert
        await Assert.That(() => _storage.ExistsAsync("")).Throws<ArgumentException>();
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_FileGoneAfterDelete()
    {
        // Arrange
        await _storage.SaveAsync("delete-test.txt", new MemoryStream("data"u8.ToArray()));
        var existsBefore = await _storage.ExistsAsync("delete-test.txt");
        await Assert.That(existsBefore).IsTrue();

        // Act
        await _storage.DeleteAsync("delete-test.txt");

        // Assert
        var existsAfter = await _storage.ExistsAsync("delete-test.txt");
        await Assert.That(existsAfter).IsFalse();
    }

    [Test]
    public async Task DeleteAsync_DoesNotThrow_ForNonExistentFile()
    {
        // Act & Assert
        var act = () => _storage.DeleteAsync("nonexistent-file.txt");
        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region GetPublicUrl Tests

    [Test]
    public async Task GetPublicUrl_ReturnsStoragePathFormat()
    {
        // Act
        var result = _storage.GetPublicUrl("avatars/user_123.png");

        // Assert
        await Assert.That(result).IsEqualTo("/storage/avatars/user_123.png");
    }

    [Test]
    public async Task GetPublicUrl_NormalizesBackslashesToForwardSlashes()
    {
        // Act
        var result = _storage.GetPublicUrl("avatars\\generated\\user_123.svg");

        // Assert
        await Assert.That(result).IsEqualTo("/storage/avatars/generated/user_123.svg");
    }

    [Test]
    public async Task GetPublicUrl_ThrowsForEmptyPath()
    {
        // Act & Assert
        await Assert.That(() => _storage.GetPublicUrl("")).Throws<ArgumentException>();
    }

    #endregion
}
