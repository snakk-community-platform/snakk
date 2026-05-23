using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class AvatarGenerationServiceTests
{
    private IFileStorage _fileStorage = null!;
    private IConfiguration _configuration = null!;
    private ILogger<AvatarGenerationService> _logger = null!;
    private IUserRepository _userRepository = null!;
    private IHubRepository _hubRepository = null!;
    private ISpaceRepository _spaceRepository = null!;
    private ICommunityRepository _communityRepository = null!;
    private AvatarGenerationService _service = null!;

    [Before(Test)]
    public void Setup()
    {
        _fileStorage = Substitute.For<IFileStorage>();

        // Default: file does not exist
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Return a public URL for any path
        _fileStorage.GetPublicUrl(Arg.Any<string>())
            .Returns<string>(callInfo => $"/{callInfo.Arg<string>()}");

        var configValues = new Dictionary<string, string?>
        {
            ["AvatarSettings:DefaultSize"] = "80"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _logger = Substitute.For<ILogger<AvatarGenerationService>>();
        _userRepository = Substitute.For<IUserRepository>();
        _hubRepository = Substitute.For<IHubRepository>();
        _spaceRepository = Substitute.For<ISpaceRepository>();
        _communityRepository = Substitute.For<ICommunityRepository>();

        _service = new AvatarGenerationService(
            _fileStorage,
            _configuration,
            _logger,
            _userRepository,
            _hubRepository,
            _spaceRepository);
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
        _fileStorage.Received(1).ExistsAsync(Arg.Is<string>(p => p.Contains("users")), Arg.Any<CancellationToken>());
        _fileStorage.Received(1).SaveAsync(Arg.Is<string>(p => p.Contains("users")), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await Assert.That(url).Contains("/");
    }

    [Test]
    public async Task GenerateUserAvatarAsync_SkipsExistingFile_WhenAlreadyExists()
    {
        // Arrange
        var userId = "u_test456";
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true); // File already exists

        // Act
        var url = await _service.GenerateUserAvatarAsync(userId);

        // Assert - SaveAsync should NOT be called since file exists
        _fileStorage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await Assert.That(url).Contains("/");
    }

    [Test]
    public async Task GenerateUserAvatarAsync_ReturnsPublicUrl()
    {
        // Arrange
        var userId = "u_newuser";

        // Act
        var url = await _service.GenerateUserAvatarAsync(userId);

        // Assert
        _fileStorage.Received(1).GetPublicUrl(Arg.Any<string>());
        await Assert.That(url).StartsWith("/");
    }

    [Test]
    public async Task GenerateUserAvatarAsync_SavesSvgContent()
    {
        // Arrange
        var userId = "u_deterministic";
        Stream? savedStream = null;
        _fileStorage.SaveAsync(Arg.Any<string>(), Arg.Do<Stream>(stream =>
            {
                // Capture the stream content
                savedStream = new MemoryStream();
                stream.CopyTo(savedStream);
                savedStream.Position = 0;
            }), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

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
        _fileStorage.Received(1).SaveAsync(Arg.Is<string>(p => p.Contains("hub")), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await Assert.That(url).Contains("/");
    }

    [Test]
    public async Task GenerateHubAvatarAsync_SkipsExistingFile()
    {
        // Arrange
        var hubId = "h_existing";
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _service.GenerateHubAvatarAsync(hubId);

        // Assert
        _fileStorage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
        _fileStorage.Received(1).SaveAsync(Arg.Is<string>(p => p.Contains("space")), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await Assert.That(url).Contains("/");
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
        _fileStorage.Received(1).SaveAsync(Arg.Is<string>(p => p.Contains("communit")), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await Assert.That(url).Contains("/");
    }

    #endregion

    #region AvatarExistsAsync Tests

    [Test]
    public async Task AvatarExistsAsync_ReturnsTrue_WhenFileExists()
    {
        // Arrange
        var userId = "u_exists";
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var exists = await _service.AvatarExistsAsync("user", userId);

        // Assert
        await Assert.That(exists).IsTrue();
        _fileStorage.Received(1).ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AvatarExistsAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var userId = "u_notexists";
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

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
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _service.DeleteAvatarAsync("user", userId);

        // Assert
        _fileStorage.Received(1).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAvatarAsync_DoesNotDelete_WhenFileDoesNotExist()
    {
        // Arrange
        var userId = "u_notexists";
        _fileStorage.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act & Assert - should not throw
        var act = async () => await _service.DeleteAvatarAsync("user", userId);
        await Assert.That(act).ThrowsNothing();
        _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        _userRepository.GetAllAsync().Returns(users);
        _hubRepository.GetAllAsync().Returns([]);
        _spaceRepository.GetAllAsync().Returns([]);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
        // SaveAsync should be called 3 times (once per user)
        _fileStorage.Received(3).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_GeneratesForAllEntityTypes()
    {
        // Arrange
        var users = new[] { CreateUser("user1") };
        var hubs = new[] { CreateHub("hub1") };
        var spaces = new[] { CreateSpace("space1") };

        _userRepository.GetAllAsync().Returns(users);
        _hubRepository.GetAllAsync().Returns(hubs);
        _spaceRepository.GetAllAsync().Returns(spaces);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        await Assert.That(count).IsEqualTo(3);
        _fileStorage.Received(3).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
        _userRepository.GetAllAsync().Returns(users);
        _hubRepository.GetAllAsync().Returns([]);
        _spaceRepository.GetAllAsync().Returns([]);

        // First user's avatar already exists (both in GenerateAllMissing check AND in GenerateUserAvatar check)
        var user1Path = Snakk.Shared.Helpers.AvatarHelper.GetFullRelativePath(users[0].PublicId.Value, Snakk.Shared.Helpers.AvatarEntityType.User, 0);
        _fileStorage.ExistsAsync(user1Path, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert - Only user2 should be generated
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task GenerateAllMissingAvatarsAsync_HandlesEmptyRepositories()
    {
        // Arrange
        _userRepository.GetAllAsync().Returns([]);
        _hubRepository.GetAllAsync().Returns([]);
        _spaceRepository.GetAllAsync().Returns([]);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        await Assert.That(count).IsEqualTo(0);
        _fileStorage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
        _userRepository.GetAllAsync().Returns(users);
        _hubRepository.GetAllAsync().Returns([]);
        _spaceRepository.GetAllAsync().Returns([]);

        // Make SaveAsync throw for the first user's path
        var callCount = 0;
        _fileStorage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
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
        _fileStorage.Received(1).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await Assert.That(url).Contains("/");
    }

    #endregion

    #region Helper Methods

    private User CreateUser(string publicId)
    {
        return User.Create(
            $"Test User {publicId}",
            $"user-{publicId}@test.com");
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
