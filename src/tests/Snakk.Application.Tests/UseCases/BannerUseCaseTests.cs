using NSubstitute;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Application.Tests.UseCases;

public class BannerUseCaseTests
{
    private readonly IBannerRepository _bannerRepository = Substitute.For<IBannerRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMarkupParser _markupParser = Substitute.For<IMarkupParser>();
    private BannerUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _markupParser.ToHtml(Arg.Any<string>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");

        _useCase = new BannerUseCase(_bannerRepository, _userRepository, _markupParser);
    }

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_WithValidParameters_CreatesBanner()
    {
        var userId = UserId.New();
        var user = CreateTestUser(userId);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.CreateAsync(BannerScopeEnum.Community, "community-id", userId, "Test Banner", "Hello world");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Title).IsEqualTo("Test Banner");
        await Assert.That(result.Value.RenderedContent).IsEqualTo("<p>Hello world</p>");
        _markupParser.Received(1).ToHtml("Hello world");
        await _bannerRepository.Received(1).AddAsync(Arg.Any<Banner>());
    }

    [Test]
    public async Task CreateAsync_WithNonExistentUser_ReturnsFailure()
    {
        var userId = UserId.New();
        _userRepository.GetByPublicIdAsync(userId).Returns((User?)null);

        var result = await _useCase.CreateAsync(BannerScopeEnum.Community, "community-id", userId, "Test", "Content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task CreateAsync_WithEmptyTitle_ReturnsFailure()
    {
        var userId = UserId.New();
        var user = CreateTestUser(userId);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.CreateAsync(BannerScopeEnum.Community, "community-id", userId, "", "Content");

        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_WithValidParameters_UpdatesBanner()
    {
        var existingBanner = CreateTestBanner();
        _bannerRepository.GetByPublicIdAsync(existingBanner.PublicId).Returns(existingBanner);

        var result = await _useCase.UpdateAsync(existingBanner.PublicId, "Updated Title", "Updated content", BannerTypeEnum.Warning, null, null, true, 0);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo("Updated Title");
        await Assert.That(result.Value.RenderedContent).IsEqualTo("<p>Updated content</p>");
        _markupParser.Received(1).ToHtml("Updated content");
        await _bannerRepository.Received(1).UpdateAsync(Arg.Any<Banner>());
    }

    [Test]
    public async Task UpdateAsync_WithNonExistentBanner_ReturnsFailure()
    {
        var announcementId = BannerId.New();
        _bannerRepository.GetByPublicIdAsync(announcementId).Returns((Banner?)null);

        var result = await _useCase.UpdateAsync(announcementId, "Title", "Content", BannerTypeEnum.Info, null, null, true, 0);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_WithExistingBanner_Succeeds()
    {
        var existingBanner = CreateTestBanner();
        _bannerRepository.GetByPublicIdAsync(existingBanner.PublicId).Returns(existingBanner);

        var result = await _useCase.DeleteAsync(existingBanner.PublicId);

        await Assert.That(result.IsSuccess).IsTrue();
        await _bannerRepository.Received(1).DeleteAsync(existingBanner.PublicId);
    }

    [Test]
    public async Task DeleteAsync_WithNonExistentBanner_ReturnsFailure()
    {
        var announcementId = BannerId.New();
        _bannerRepository.GetByPublicIdAsync(announcementId).Returns((Banner?)null);

        var result = await _useCase.DeleteAsync(announcementId);

        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_WithExistingBanner_ReturnsSuccess()
    {
        var existingBanner = CreateTestBanner();
        _bannerRepository.GetByPublicIdAsync(existingBanner.PublicId).Returns(existingBanner);

        var result = await _useCase.GetByIdAsync(existingBanner.PublicId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.PublicId).IsEqualTo(existingBanner.PublicId);
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentBanner_ReturnsFailure()
    {
        var announcementId = BannerId.New();
        _bannerRepository.GetByPublicIdAsync(announcementId).Returns((Banner?)null);

        var result = await _useCase.GetByIdAsync(announcementId);

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
            avatarThumbnailFileName: null,
            avatarMicroFileName: null,
            avatarRevision: 0,
            autoFollowOnReply: true,
            createdAt: DateTime.UtcNow);
}
