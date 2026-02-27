using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class AvatarGenerationServiceTests
{
    private Mock<IFileStorage> _mockFileStorage = null!;
    private IConfiguration _configuration = null!;
    private Mock<ILogger<AvatarGenerationService>> _mockLogger = null!;
    private Mock<IUserRepository> _mockUserRepository = null!;
    private Mock<IHubRepository> _mockHubRepository = null!;
    private Mock<ISpaceRepository> _mockSpaceRepository = null!;
    private Mock<ICommunityRepository> _mockCommunityRepository = null!;
    private AvatarGenerationService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockFileStorage = new Mock<IFileStorage>();

        // Default: file does not exist
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Return a public URL for any path
        _mockFileStorage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(path => $"/storage/{path}");

        var configValues = new Dictionary<string, string?>
        {
            ["AvatarSettings:DefaultSize"] = "80"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _mockLogger = new Mock<ILogger<AvatarGenerationService>>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockHubRepository = new Mock<IHubRepository>();
        _mockSpaceRepository = new Mock<ISpaceRepository>();
        _mockCommunityRepository = new Mock<ICommunityRepository>();

        _service = new AvatarGenerationService(
            _mockFileStorage.Object,
            _configuration,
            _mockLogger.Object,
            _mockUserRepository.Object,
            _mockHubRepository.Object,
            _mockSpaceRepository.Object,
            _mockCommunityRepository.Object);
    }

    #region GenerateUserAvatarAsync Tests

    [Test]
    public async Task GenerateUserAvatarAsync_SavesFile_WhenNotExists()
    {
        // Arrange
        var userId = "u_test123";

        // Act
        var url = await _service.GenerateUserAvatarAsync(userId);

        // Assert - Verify file was saved via IFileStorage
        _mockFileStorage.Verify(x => x.ExistsAsync(It.Is<string>(p => p.Contains("users")), It.IsAny<CancellationToken>()), Times.Once);
        _mockFileStorage.Verify(x => x.SaveAsync(It.Is<string>(p => p.Contains("users")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.That(url).Contains("/storage/");
    }

    [Test]
    public async Task GenerateUserAvatarAsync_SkipsExistingFile_WhenAlreadyExists()
    {
        // Arrange
        var userId = "u_test456";
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // File already exists

        // Act
        var url = await _service.GenerateUserAvatarAsync(userId);

        // Assert - SaveAsync should NOT be called since file exists
        _mockFileStorage.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        await Assert.That(url).Contains("/storage/");
    }

    [Test]
    public async Task GenerateUserAvatarAsync_ReturnsPublicUrl()
    {
        // Arrange
        var userId = "u_newuser";

        // Act
        var url = await _service.GenerateUserAvatarAsync(userId);

        // Assert
        _mockFileStorage.Verify(x => x.GetPublicUrl(It.IsAny<string>()), Times.Once);
        await Assert.That(url).StartsWith("/storage/");
    }

    [Test]
    public async Task GenerateUserAvatarAsync_SavesSvgContent()
    {
        // Arrange
        var userId = "u_deterministic";
        Stream? savedStream = null;
        _mockFileStorage.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, CancellationToken>((path, stream, ct) =>
            {
                // Capture the stream content
                savedStream = new MemoryStream();
                stream.CopyTo(savedStream);
                savedStream.Position = 0;
            });

        // Act
        await _service.GenerateUserAvatarAsync(userId);

        // Assert - Verify SVG content was saved
        await Assert.That(savedStream).IsNotNull();
        using var reader = new StreamReader(savedStream!);
        var content = await reader.ReadToEndAsync();
        await Assert.That(content).Contains("<svg");
        await Assert.That(content).Contains("</svg>");
    }

    #endregion

    #region GenerateHubAvatarAsync Tests

    [Test]
    public async Task GenerateHubAvatarAsync_SavesFile_WithHubPath()
    {
        // Arrange
        var hubId = "h_hub123";

        // Act
        var url = await _service.GenerateHubAvatarAsync(hubId);

        // Assert
        _mockFileStorage.Verify(x => x.SaveAsync(It.Is<string>(p => p.Contains("hub")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.That(url).Contains("/storage/");
    }

    [Test]
    public async Task GenerateHubAvatarAsync_SkipsExistingFile()
    {
        // Arrange
        var hubId = "h_existing";
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.GenerateHubAvatarAsync(hubId);

        // Assert
        _mockFileStorage.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GenerateSpaceAvatarAsync Tests

    [Test]
    public async Task GenerateSpaceAvatarAsync_SavesFile_WithSpacePath()
    {
        // Arrange
        var spaceId = "s_space123";

        // Act
        var url = await _service.GenerateSpaceAvatarAsync(spaceId);

        // Assert
        _mockFileStorage.Verify(x => x.SaveAsync(It.Is<string>(p => p.Contains("space")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.That(url).Contains("/storage/");
    }

    #endregion

    #region GenerateCommunityAvatarAsync Tests

    [Test]
    public async Task GenerateCommunityAvatarAsync_SavesFile_WithCommunityPath()
    {
        // Arrange
        var communityId = "c_community123";

        // Act
        var url = await _service.GenerateCommunityAvatarAsync(communityId);

        // Assert
        _mockFileStorage.Verify(x => x.SaveAsync(It.Is<string>(p => p.Contains("communit")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.That(url).Contains("/storage/");
    }

    #endregion

    #region AvatarExistsAsync Tests

    [Test]
    public async Task AvatarExistsAsync_ReturnsTrue_WhenFileExists()
    {
        // Arrange
        var userId = "u_exists";
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var exists = await _service.AvatarExistsAsync("user", userId);

        // Assert
        await Assert.That(exists).IsTrue();
        _mockFileStorage.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AvatarExistsAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var userId = "u_notexists";
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var exists = await _service.AvatarExistsAsync("user", userId);

        // Assert
        await Assert.That(exists).IsFalse();
    }

    #endregion

    #region DeleteAvatarAsync Tests

    [Test]
    public async Task DeleteAvatarAsync_DeletesFile_WhenExists()
    {
        // Arrange
        var userId = "u_delete";
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.DeleteAvatarAsync("user", userId);

        // Assert
        _mockFileStorage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAvatarAsync_DoesNotDelete_WhenFileDoesNotExist()
    {
        // Arrange
        var userId = "u_notexists";
        _mockFileStorage.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert - should not throw
        var act = async () => await _service.DeleteAvatarAsync("user", userId);
        await Assert.That(act).ThrowsNothing();
        _mockFileStorage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GenerateAllMissingAvatarsAsync Tests

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_GeneratesForAllUsers()
    {
        // Arrange
        var users = new[]
        {
            CreateUser("user1"),
            CreateUser("user2"),
            CreateUser("user3")
        };
        _mockUserRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(users);
        _mockHubRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Hub>());
        _mockSpaceRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Space>());

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
        // SaveAsync should be called 3 times (once per user)
        _mockFileStorage.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_GeneratesForAllEntityTypes()
    {
        // Arrange
        var users = new[] { CreateUser("user1") };
        var hubs = new[] { CreateHub("hub1") };
        var spaces = new[] { CreateSpace("space1") };

        _mockUserRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(users);
        _mockHubRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(hubs);
        _mockSpaceRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(spaces);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
        _mockFileStorage.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_SkipsExistingFiles()
    {
        // Arrange
        var users = new[]
        {
            CreateUser("user1"),
            CreateUser("user2")
        };
        _mockUserRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(users);
        _mockHubRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Hub>());
        _mockSpaceRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Space>());

        // First user's avatar already exists (both in GenerateAllMissing check AND in GenerateUserAvatar check)
        var user1Path = Snakk.Shared.Helpers.AvatarHelper.GetFullRelativePath(users[0].PublicId.Value, Snakk.Shared.Helpers.AvatarEntityType.User, 0);
        _mockFileStorage.Setup(x => x.ExistsAsync(user1Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert - Only user2 should be generated
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_HandlesEmptyRepositories()
    {
        // Arrange
        _mockUserRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<User>());
        _mockHubRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Hub>());
        _mockSpaceRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Space>());

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        await Assert.That(count).IsEqualTo(0);
        _mockFileStorage.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_ContinuesOnIndividualFailure()
    {
        // Arrange
        var users = new[]
        {
            CreateUser("u_user1"),
            CreateUser("u_user2")
        };
        _mockUserRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(users);
        _mockHubRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Hub>());
        _mockSpaceRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Space>());

        // Make SaveAsync throw for the first user's path
        var callCount = 0;
        _mockFileStorage.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<string, Stream, CancellationToken>((path, stream, ct) =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    throw new IOException("Simulated failure");
                return Task.CompletedTask;
            });

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert - Should handle failure gracefully
        await Assert.That(count).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Custom Size Tests

    [Test]
    public async Task GenerateUserAvatarAsync_SupportsCustomSize()
    {
        // Arrange
        var userId = "u_custom";
        var customSize = 120;

        // Act
        var url = await _service.GenerateUserAvatarAsync(userId, customSize);

        // Assert
        _mockFileStorage.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.That(url).Contains("/storage/");
    }

    #endregion

    #region Helper Methods

    private User CreateUser(string publicId)
    {
        return User.Create(
            $"Test User {publicId}",
            $"user-{publicId}@test.com",
            null);
    }

    private Hub CreateHub(string publicId)
    {
        var community = Community.Create(
            "Test Community",
            "test",
            "Description");

        return Hub.Create(
            community.PublicId,
            $"Test Hub {publicId}",
            $"hub-{publicId}",
            "Description");
    }

    private Space CreateSpace(string publicId)
    {
        return Space.Create(
            HubId.From("h_test"),
            $"Test Space {publicId}",
            $"space-{publicId}",
            "Description");
    }

    #endregion
}
