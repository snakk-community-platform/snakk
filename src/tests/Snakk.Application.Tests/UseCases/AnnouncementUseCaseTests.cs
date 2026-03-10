using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Application.Tests.UseCases;

public class AnnouncementUseCaseTests
{
    private readonly Mock<IAnnouncementRepository> _mockAnnouncementRepository = new();
    private readonly Mock<IUserRepository> _mockUserRepository = new();
    private readonly Mock<IMarkupParser> _mockMarkupParser = new();
    private AnnouncementUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockMarkupParser
            .Setup(m => m.ToHtml(It.IsAny<string>()))
            .Returns((string s) => $"<p>{s}</p>");

        _useCase = new AnnouncementUseCase(
            _mockAnnouncementRepository.Object,
            _mockUserRepository.Object,
            _mockMarkupParser.Object);
    }

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_WithValidParameters_CreatesAnnouncement()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateTestUser(userId);

        _mockUserRepository.Setup(r => r.GetByPublicIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _useCase.CreateAsync(
            AnnouncementScopeEnum.Community,
            "community-id",
            userId,
            "Test Announcement",
            "Hello world");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Title).IsEqualTo("Test Announcement");
        await Assert.That(result.Value.RenderedContent).IsEqualTo("<p>Hello world</p>");

        _mockMarkupParser.Verify(m => m.ToHtml("Hello world"), Times.Once);
        _mockAnnouncementRepository.Verify(r => r.AddAsync(It.IsAny<Announcement>()), Times.Once);
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
            AnnouncementScopeEnum.Community,
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
            AnnouncementScopeEnum.Community,
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
    public async Task UpdateAsync_WithValidParameters_UpdatesAnnouncement()
    {
        // Arrange
        var existingAnnouncement = CreateTestAnnouncement();

        _mockAnnouncementRepository.Setup(r => r.GetByPublicIdAsync(existingAnnouncement.PublicId))
            .ReturnsAsync(existingAnnouncement);

        // Act
        var result = await _useCase.UpdateAsync(
            existingAnnouncement.PublicId,
            "Updated Title",
            "Updated content",
            AnnouncementTypeEnum.Warning,
            null, null, true, 0);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo("Updated Title");
        await Assert.That(result.Value.RenderedContent).IsEqualTo("<p>Updated content</p>");

        _mockMarkupParser.Verify(m => m.ToHtml("Updated content"), Times.Once);
        _mockAnnouncementRepository.Verify(r => r.UpdateAsync(It.IsAny<Announcement>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_WithNonExistentAnnouncement_ReturnsFailure()
    {
        // Arrange
        var announcementId = AnnouncementId.New();

        _mockAnnouncementRepository.Setup(r => r.GetByPublicIdAsync(announcementId))
            .ReturnsAsync((Announcement?)null);

        // Act
        var result = await _useCase.UpdateAsync(
            announcementId,
            "Title",
            "Content",
            AnnouncementTypeEnum.Info,
            null, null, true, 0);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_WithExistingAnnouncement_Succeeds()
    {
        // Arrange
        var existingAnnouncement = CreateTestAnnouncement();

        _mockAnnouncementRepository.Setup(r => r.GetByPublicIdAsync(existingAnnouncement.PublicId))
            .ReturnsAsync(existingAnnouncement);

        // Act
        var result = await _useCase.DeleteAsync(existingAnnouncement.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _mockAnnouncementRepository.Verify(r => r.DeleteAsync(existingAnnouncement.PublicId), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_WithNonExistentAnnouncement_ReturnsFailure()
    {
        // Arrange
        var announcementId = AnnouncementId.New();

        _mockAnnouncementRepository.Setup(r => r.GetByPublicIdAsync(announcementId))
            .ReturnsAsync((Announcement?)null);

        // Act
        var result = await _useCase.DeleteAsync(announcementId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_WithExistingAnnouncement_ReturnsSuccess()
    {
        // Arrange
        var existingAnnouncement = CreateTestAnnouncement();

        _mockAnnouncementRepository.Setup(r => r.GetByPublicIdAsync(existingAnnouncement.PublicId))
            .ReturnsAsync(existingAnnouncement);

        // Act
        var result = await _useCase.GetByIdAsync(existingAnnouncement.PublicId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.PublicId).IsEqualTo(existingAnnouncement.PublicId);
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentAnnouncement_ReturnsFailure()
    {
        // Arrange
        var announcementId = AnnouncementId.New();

        _mockAnnouncementRepository.Setup(r => r.GetByPublicIdAsync(announcementId))
            .ReturnsAsync((Announcement?)null);

        // Act
        var result = await _useCase.GetByIdAsync(announcementId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    private static Announcement CreateTestAnnouncement() =>
        Announcement.Create(
            AnnouncementScopeEnum.Community,
            "community-id",
            UserId.New(),
            "Test Announcement",
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
