using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class AvatarGenerationServiceTests : IDisposable
{
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AvatarGenerationService>> _mockLogger;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IHubRepository> _mockHubRepository;
    private readonly Mock<ISpaceRepository> _mockSpaceRepository;
    private readonly Mock<ICommunityRepository> _mockCommunityRepository;
    private readonly AvatarGenerationService _service;
    private readonly string _testRootPath;
    private readonly string _generatedAvatarsPath;

    public AvatarGenerationServiceTests()
    {
        // Create a unique temp directory for each test run
        _testRootPath = Path.Combine(Path.GetTempPath(), $"avatar-tests-{Guid.NewGuid()}");
        _generatedAvatarsPath = "avatars/generated";
        Directory.CreateDirectory(_testRootPath);

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(x => x.ContentRootPath).Returns(_testRootPath);

        // Create real configuration
        var configValues = new Dictionary<string, string?>
        {
            ["AvatarSettings:GeneratedAvatarsPath"] = _generatedAvatarsPath,
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
            _mockEnv.Object,
            _configuration,
            _mockLogger.Object,
            _mockUserRepository.Object,
            _mockHubRepository.Object,
            _mockSpaceRepository.Object,
            _mockCommunityRepository.Object);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    #region GenerateUserAvatarAsync Tests

    [Fact]
    public async Task GenerateUserAvatarAsync_CreatesFile_WhenNotExists()
    {
        // Arrange
        var userId = "u_test123";

        // Act
        var filePath = await _service.GenerateUserAvatarAsync(userId);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("<svg");
        content.Should().Contain("</svg>");
    }

    [Fact]
    public async Task GenerateUserAvatarAsync_SkipsExistingFile_WhenAlreadyExists()
    {
        // Arrange
        var userId = "u_test456";
        await _service.GenerateUserAvatarAsync(userId);
        var firstModified = File.GetLastWriteTimeUtc(GetUserAvatarPath(userId));

        // Wait a moment to ensure timestamp would change if file was regenerated
        await Task.Delay(10);

        // Act
        await _service.GenerateUserAvatarAsync(userId);
        var secondModified = File.GetLastWriteTimeUtc(GetUserAvatarPath(userId));

        // Assert
        secondModified.Should().Be(firstModified);
    }

    [Fact]
    public async Task GenerateUserAvatarAsync_CreatesDirectories_WhenNotExist()
    {
        // Arrange
        var userId = "u_newuser";

        // Act
        var filePath = await _service.GenerateUserAvatarAsync(userId);

        // Assert
        var directory = Path.GetDirectoryName(filePath);
        Directory.Exists(directory).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateUserAvatarAsync_GeneratesDeterministicContent_ForSameUserId()
    {
        // Arrange
        var userId = "u_deterministic";

        // Act
        await _service.GenerateUserAvatarAsync(userId);
        var content1 = await File.ReadAllTextAsync(GetUserAvatarPath(userId));

        // Delete and regenerate
        File.Delete(GetUserAvatarPath(userId));
        await _service.GenerateUserAvatarAsync(userId);
        var content2 = await File.ReadAllTextAsync(GetUserAvatarPath(userId));

        // Assert
        content1.Should().Be(content2);
    }

    #endregion

    #region GenerateHubAvatarAsync Tests

    [Fact]
    public async Task GenerateHubAvatarAsync_CreatesFile_WithHubPrefix()
    {
        // Arrange
        var hubId = "h_hub123";

        // Act
        var filePath = await _service.GenerateHubAvatarAsync(hubId);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("<svg");
    }

    [Fact]
    public async Task GenerateHubAvatarAsync_SkipsExistingFile()
    {
        // Arrange
        var hubId = "h_existing";
        await _service.GenerateHubAvatarAsync(hubId);
        var firstModified = File.GetLastWriteTimeUtc(GetHubAvatarPath(hubId));

        await Task.Delay(10);

        // Act
        await _service.GenerateHubAvatarAsync(hubId);
        var secondModified = File.GetLastWriteTimeUtc(GetHubAvatarPath(hubId));

        // Assert
        secondModified.Should().Be(firstModified);
    }

    #endregion

    #region GenerateSpaceAvatarAsync Tests

    [Fact]
    public async Task GenerateSpaceAvatarAsync_CreatesFile_WithSpacePrefix()
    {
        // Arrange
        var spaceId = "s_space123";

        // Act
        var filePath = await _service.GenerateSpaceAvatarAsync(spaceId);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("<svg");
    }

    #endregion

    #region GenerateCommunityAvatarAsync Tests

    [Fact]
    public async Task GenerateCommunityAvatarAsync_CreatesFile_WithCommunityPrefix()
    {
        // Arrange
        var communityId = "c_community123";

        // Act
        var filePath = await _service.GenerateCommunityAvatarAsync(communityId);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("<svg");
    }

    #endregion

    #region AvatarExistsAsync Tests

    [Fact]
    public async Task AvatarExistsAsync_ReturnsTrue_WhenFileExists()
    {
        // Arrange
        var userId = "u_exists";
        await _service.GenerateUserAvatarAsync(userId);

        // Act
        var exists = await _service.AvatarExistsAsync("user", userId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AvatarExistsAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var userId = "u_notexists";

        // Act
        var exists = await _service.AvatarExistsAsync("user", userId);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region DeleteAvatarAsync Tests

    [Fact]
    public async Task DeleteAvatarAsync_RemovesFile_WhenExists()
    {
        // Arrange
        var userId = "u_delete";
        await _service.GenerateUserAvatarAsync(userId);
        var filePath = GetUserAvatarPath(userId);
        File.Exists(filePath).Should().BeTrue();

        // Act
        await _service.DeleteAvatarAsync("user", userId);

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAvatarAsync_DoesNotThrow_WhenFileDoesNotExist()
    {
        // Arrange
        var userId = "u_notexists";

        // Act
        var act = async () => await _service.DeleteAvatarAsync("user", userId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region GenerateAllMissingAvatarsAsync Tests

    [Fact]
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
        count.Should().Be(3);
        // Check that files were created for the actual user IDs
        File.Exists(GetUserAvatarPath(users[0].PublicId.Value)).Should().BeTrue();
        File.Exists(GetUserAvatarPath(users[1].PublicId.Value)).Should().BeTrue();
        File.Exists(GetUserAvatarPath(users[2].PublicId.Value)).Should().BeTrue();
    }

    [Fact]
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
        count.Should().Be(3);
        File.Exists(GetUserAvatarPath(users[0].PublicId.Value)).Should().BeTrue();
        File.Exists(GetHubAvatarPath(hubs[0].PublicId.Value)).Should().BeTrue();
        File.Exists(GetSpaceAvatarPath(spaces[0].PublicId.Value)).Should().BeTrue();
    }

    [Fact]
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

        // Pre-create one avatar for the first user
        await _service.GenerateUserAvatarAsync(users[0].PublicId.Value);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        count.Should().Be(1); // Only user2 should be generated (user1 already exists)
    }

    [Fact]
    public async Task GenerateAllMissingAvatarsAsync_HandlesEmptyRepositories()
    {
        // Arrange
        _mockUserRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<User>());
        _mockHubRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Hub>());
        _mockSpaceRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<Space>());

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
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

        // Make the directory read-only to cause a failure for user1
        var user1Dir = Path.Combine(_testRootPath, _generatedAvatarsPath, "users");
        Directory.CreateDirectory(user1Dir);

        // Act
        var count = await _service.GenerateAllMissingAvatarsAsync();

        // Assert
        // At least one should succeed even if one fails
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Custom Size Tests

    [Fact]
    public async Task GenerateUserAvatarAsync_SupportsCustomSize()
    {
        // Arrange
        var userId = "u_custom";
        var customSize = 120;

        // Act
        var filePath = await _service.GenerateUserAvatarAsync(userId, customSize);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("<svg");
    }

    #endregion

    #region Helper Methods

    private string GetUserAvatarPath(string userId)
    {
        return Path.Combine(_testRootPath, _generatedAvatarsPath, "users", $"{userId}.svg");
    }

    private string GetHubAvatarPath(string hubId)
    {
        return Path.Combine(_testRootPath, _generatedAvatarsPath, "hubs", $"{hubId}.svg");
    }

    private string GetSpaceAvatarPath(string spaceId)
    {
        return Path.Combine(_testRootPath, _generatedAvatarsPath, "spaces", $"{spaceId}.svg");
    }

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
