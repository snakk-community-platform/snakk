using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Snakk.Application.Services;
using Snakk.Infrastructure.BackgroundJobs;

namespace Snakk.Infrastructure.Tests.BackgroundJobs;

public class AvatarGenerationHostedServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<AvatarGenerationHostedService>> _mockLogger;
    private readonly Mock<IAvatarGenerationService> _mockAvatarService;

    public AvatarGenerationHostedServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<AvatarGenerationHostedService>>();
        _mockAvatarService = new Mock<IAvatarGenerationService>();

        // Setup service provider hierarchy
        _mockScope.Setup(x => x.ServiceProvider).Returns(Mock.Of<IServiceProvider>(sp =>
            sp.GetService(typeof(IAvatarGenerationService)) == _mockAvatarService.Object));

        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockScopeFactory.Object);

        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
    }

    private IConfiguration CreateConfiguration(bool generateOnStartup)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["AvatarSettings:GenerateOnStartup"] = generateOnStartup.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task StartAsync_GeneratesAvatars_WhenEnabled()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService.Setup(x => x.GenerateAllMissingAvatarsAsync()).ReturnsAsync(42);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockAvatarService.Verify(x => x.GenerateAllMissingAvatarsAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_SkipsGeneration_WhenDisabled()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: false);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockAvatarService.Verify(x => x.GenerateAllMissingAvatarsAsync(), Times.Never);
    }

    [Fact]
    public async Task StartAsync_LogsSuccess_WhenGenerationCompletes()
    {
        // Arrange
        var avatarCount = 100;
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService.Setup(x => x.GenerateAllMissingAvatarsAsync()).ReturnsAsync(avatarCount);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully generated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService
            .Setup(x => x.GenerateAllMissingAvatarsAsync())
            .ThrowsAsync(new IOException("Disk full"));

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        var act = async () => await service.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_LogsError_WhenGenerationFails()
    {
        // Arrange
        var exception = new IOException("Disk full");
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService
            .Setup(x => x.GenerateAllMissingAvatarsAsync())
            .ThrowsAsync(exception);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to generate avatars")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_LogsStartMessage()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService.Setup(x => x.GenerateAllMissingAvatarsAsync()).ReturnsAsync(10);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting avatar generation")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_LogsDisabledMessage_WhenDisabled()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: false);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: false);
        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        var act = async () => await service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_CreatesNewScope()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService.Setup(x => x.GenerateAllMissingAvatarsAsync()).ReturnsAsync(10);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockScopeFactory.Verify(x => x.CreateScope(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_DisposesScope_AfterGeneration()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService.Setup(x => x.GenerateAllMissingAvatarsAsync()).ReturnsAsync(10);

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockScope.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_LogsTimingInformation()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _mockAvatarService
            .Setup(x => x.GenerateAllMissingAvatarsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100); // Simulate work
                return 50;
            });

        var service = new AvatarGenerationHostedService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ms")), // Should log milliseconds
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
