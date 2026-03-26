using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Application.Tests.UseCases;

public class BannerUseCaseTests
{
    private readonly Mock<IBannerRepository> _mockBannerRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IMarkupParser> _mockMarkupParser = new();
    private BannerUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockMarkupParser
            .Setup(m => m.ToHtml(It.IsAny<string>()))
            .Returns((string s) => $"<p>{s}</p>");

        _useCase = new BannerUseCase(
            _mockBannerRepository.Object,
            _mockUserRepository.Object,
            _mockMarkupParser.Object);
    }

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_WithValidParameters_CreatesBanner()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateTestUser(userId);

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.CreateAsync(
            BannerScopeEnum.Community,
            "community-id",
            userId,
            "Test Banner",
            "Hello world");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Title).IsEqualTo("Test Banner");
        await Assert.That(result.Value.RenderedContent).IsEqualTo("<p>Hello world</p>");

        _mockMarkupParser.Verify(m => m.ToHtml("Hello world"), Times.Once);
        _mockBannerRepository.Verify(r => r.AddAsync(It.IsAny<Banner>()), Times.Once);
    }

    [Test]
    public async Task CreateAsync_WithNonExistentUser_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _useCase.CreateAsync(
            BannerScopeEnum.Community,
            "community-id",
            userId,
            "Test",
            "Content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task CreateAsync_WithEmptyTitle_ReturnsFailure()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateTestUser(userId);

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.CreateAsync(
            BannerScopeEnum.Community,
            "community-id",
            userId,
            "",
            "Content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_WithValidParameters_UpdatesBanner()
    {
        // Arrange
        var existingBanner = CreateTestBanner();

        _mockBannerRepository.Setup(r => r.GetByPublicIdAsync(existingBanner.PublicId))
            .ReturnsAsync(existingBanner);

        // Act
        var result = await _useCase.UpdateAsync(
            existingBanner.PublicId,
            "Updated Title",
            "Updated content",
            BannerTypeEnum.Warning,
            null, null, true, 0);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo("Updated Title");
        await Assert.That(result.Value.RenderedContent).IsEqualTo("<p>Updated content</p>");

        _mockMarkupParser.Verify(m => m.ToHtml("Updated content"), Times.Once);
        _mockBannerRepository.Verify(r => r.UpdateAsync(It.IsAny<Banner>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_WithNonExistentBanner_ReturnsFailure()
    {
        // Arrange
        var announcementId = BannerId.New();

        _mockBannerRepository.Setup(r => r.GetByPublicIdAsync(announcementId))
            .ReturnsAsync((Banner?)null);

        // Act
        var result = await _useCase.UpdateAsync(
            announcementId,
            "Title",
            "Content",
            BannerTypeEnum.Info,
            null, null, true, 0);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_WithExistingBanner_Succeeds()
    {
        // Arrange
        var existingBanner = CreateTestBanner();

        _mockBannerRepository.Setup(r => r.GetByPublicIdAsync(existingBanner.PublicId))
            .ReturnsAsync(existingBanner);

        // Act
        var result = await _useCase.DeleteAsync(existingBanner.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockBannerRepository.Verify(r => r.DeleteAsync(existingBanner.PublicId), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_WithNonExistentBanner_ReturnsFailure()
    {
        // Arrange
        var announcementId = BannerId.New();

        _mockBannerRepository.Setup(r => r.GetByPublicIdAsync(announcementId))
            .ReturnsAsync((Banner?)null);

        // Act
        var result = await _useCase.DeleteAsync(announcementId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_WithExistingBanner_ReturnsSuccess()
    {
        // Arrange
        var existingBanner = CreateTestBanner();

        _mockBannerRepository.Setup(r => r.GetByPublicIdAsync(existingBanner.PublicId))
            .ReturnsAsync(existingBanner);

        // Act
        var result = await _useCase.GetByIdAsync(existingBanner.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.PublicId).IsEqualTo(existingBanner.PublicId);
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentBanner_ReturnsFailure()
    {
        // Arrange
        var announcementId = BannerId.New();

        _mockBannerRepository.Setup(r => r.GetByPublicIdAsync(announcementId))
            .ReturnsAsync((Banner?)null);

        // Act
        var result = await _useCase.GetByIdAsync(announcementId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    private static Banner CreateTestBanner() =>
        Banner.Create(
            BannerScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Test Banner",
            "Test content",
            "<p>Test content</p>");

    private static User CreateTestUser(UserId userId) =>
        User.Rehydrate(
            userId,
            "TestUser",
            "test@example.com",
            "hashed-password",
            emailVerified: true,
            emailVerificationToken: null,
            oauthProvider: null,
            oauthProviderId: null,
            role: null,
            avatarFileName: null,
            avatarRevision: 0,
            preferEndlessScroll: true,
            autoFollowOnReply: true,
            createdAt: DateTime.UtcNow);
}
