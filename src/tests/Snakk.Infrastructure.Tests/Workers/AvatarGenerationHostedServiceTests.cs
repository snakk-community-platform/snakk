using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Snakk.Application.Services;
using Snakk.Worker.Workers;

namespace Snakk.Infrastructure.Tests.Workers;

public class AvatarGenerationHostedServiceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AvatarGenerationHostedService> _logger;
    private readonly IAvatarGenerationService _avatarService;

    public AvatarGenerationHostedServiceTests()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _scope = Substitute.For<IServiceScope>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _logger = Substitute.For<ILogger<AvatarGenerationHostedService>>();
        _avatarService = Substitute.For<IAvatarGenerationService>();

        // Setup service provider hierarchy
        var scopeServiceProvider = Substitute.For<IServiceProvider>();
        scopeServiceProvider.GetService(typeof(IAvatarGenerationService)).Returns(_avatarService);
        _scope.ServiceProvider.Returns(scopeServiceProvider);

        _serviceProvider
            .GetService(typeof(IServiceScopeFactory))
            .Returns(_scopeFactory);

        _scopeFactory.CreateScope().Returns(_scope);
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

    [Test]
    public async Task StartAsync_GeneratesAvatars_WhenEnabled()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService.GenerateAllMissingAvatarsAsync().Returns(42);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await _avatarService.Received(1).GenerateAllMissingAvatarsAsync();
    }

    [Test]
    public async Task StartAsync_SkipsGeneration_WhenDisabled()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: false);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await _avatarService.DidNotReceive().GenerateAllMissingAvatarsAsync();
    }

    [Test]
    public async Task StartAsync_LogsSuccess_WhenGenerationCompletes()
    {
        // Arrange
        var avatarCount = 100;
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService.GenerateAllMissingAvatarsAsync().Returns(avatarCount);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("Successfully generated")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task StartAsync_DoesNotThrow_WhenGenerationFails()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService
            .GenerateAllMissingAvatarsAsync()
            .Throws(new IOException("Disk full"));

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act & Assert
        var act = async () => await service.StartAsync(CancellationToken.None);
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task StartAsync_LogsError_WhenGenerationFails()
    {
        // Arrange
        var exception = new IOException("Disk full");
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService
            .GenerateAllMissingAvatarsAsync()
            .Throws(exception);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("Failed to generate avatars")),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task StartAsync_LogsStartMessage()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService.GenerateAllMissingAvatarsAsync().Returns(10);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("Starting avatar generation")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task StartAsync_LogsDisabledMessage_WhenDisabled()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: false);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("disabled")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: false);
        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act & Assert
        var act = async () => await service.StopAsync(CancellationToken.None);
        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task StartAsync_CreatesNewScope()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService.GenerateAllMissingAvatarsAsync().Returns(10);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _scopeFactory.Received(1).CreateScope();
    }

    [Test]
    public async Task StartAsync_DisposesScope_AfterGeneration()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService.GenerateAllMissingAvatarsAsync().Returns(10);

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _scope.Received(1).Dispose();
    }

    [Test]
    public async Task StartAsync_LogsTimingInformation()
    {
        // Arrange
        var configuration = CreateConfiguration(generateOnStartup: true);
        _avatarService
            .GenerateAllMissingAvatarsAsync()
            .Returns(async _ =>
            {
                await Task.Delay(100); // Simulate work
                return 50;
            });

        var service = new AvatarGenerationHostedService(
            _serviceProvider,
            _logger,
            configuration);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("ms")), // Should log milliseconds
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
