using Moq;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class ModerationUseCaseTests
{
    private readonly Mock<IModerationRepository> _mockModerationRepository = new();
    private ModerationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new ModerationUseCase(_mockModerationRepository.Object);
    }

    #region AssignRoleAsync Tests

    [Test]
    public async Task AssignRoleAsync_WithPermission_AssignsRole()
    {
        // Arrange
        const string targetUser = "user-1";
        const string assigner = "admin-1";
        const string communityId = "community-1";
        var roleType = UserRoleType.SpaceMod;

        var expectedRole = new UserRoleDto(
            "role-1", targetUser, "TestUser", "SpaceMod",
            communityId, "Community", null, null, null, null,
            assigner, "Admin", DateTime.UtcNow, null);

        _mockModerationRepository.Setup(r => r.CanAdministerAsync(assigner, communityId, null, null))
            .ReturnsAsync(true);
        _mockModerationRepository.Setup(r => r.AssignRoleAsync(targetUser, roleType, communityId, null, null, assigner))
            .ReturnsAsync(expectedRole);

        // Act
        var result = await _useCase.AssignRoleAsync(targetUser, roleType, communityId, null, null, assigner);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.UserPublicId).IsEqualTo(targetUser);
        await Assert.That(result.Value.Role).IsEqualTo("SpaceMod");
    }

    [Test]
    public async Task AssignRoleAsync_WithoutPermission_ReturnsFailure()
    {
        // Arrange
        const string targetUser = "user-1";
        const string assigner = "user-2";
        const string communityId = "community-1";

        _mockModerationRepository.Setup(r => r.CanAdministerAsync(assigner, communityId, null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.AssignRoleAsync(targetUser, UserRoleType.SpaceMod, communityId, null, null, assigner);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");

        _mockModerationRepository.Verify(r => r.AssignRoleAsync(
            It.IsAny<string>(), It.IsAny<UserRoleType>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AssignRoleAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        const string assigner = "admin-1";
        const string communityId = "community-1";

        _mockModerationRepository.Setup(r => r.CanAdministerAsync(assigner, communityId, null, null))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.AssignRoleAsync("user-1", UserRoleType.SpaceMod, communityId, null, null, assigner);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Database error");
    }

    #endregion

    #region RevokeRoleAsync Tests

    [Test]
    public async Task RevokeRoleAsync_WithValidRole_RevokesRole()
    {
        // Arrange
        const string rolePublicId = "role-1";
        const string revoker = "admin-1";

        var role = new UserRoleDto(
            rolePublicId, "user-1", "TestUser", "SpaceMod",
            "community-1", "Community", null, null, null, null,
            "assigner", "Assigner", DateTime.UtcNow, null);

        _mockModerationRepository.Setup(r => r.GetRoleByPublicIdAsync(rolePublicId))
            .ReturnsAsync(role);
        _mockModerationRepository.Setup(r => r.CanAdministerAsync(revoker, "community-1", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.RevokeRoleAsync(rolePublicId, revoker);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.RevokeRoleAsync(rolePublicId, revoker), Times.Once);
    }

    [Test]
    public async Task RevokeRoleAsync_WithNonExistentRole_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.GetRoleByPublicIdAsync("non-existent"))
            .ReturnsAsync((UserRoleDto?)null);

        // Act
        var result = await _useCase.RevokeRoleAsync("non-existent", "admin-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task RevokeRoleAsync_WithAlreadyRevokedRole_ReturnsFailure()
    {
        // Arrange
        var role = new UserRoleDto(
            "role-1", "user-1", "TestUser", "SpaceMod",
            "community-1", "Community", null, null, null, null,
            "assigner", "Assigner", DateTime.UtcNow, DateTime.UtcNow); // RevokedAt is set

        _mockModerationRepository.Setup(r => r.GetRoleByPublicIdAsync("role-1"))
            .ReturnsAsync(role);

        // Act
        var result = await _useCase.RevokeRoleAsync("role-1", "admin-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already revoked");
    }

    [Test]
    public async Task RevokeRoleAsync_WithoutPermission_ReturnsFailure()
    {
        // Arrange
        var role = new UserRoleDto(
            "role-1", "user-1", "TestUser", "SpaceMod",
            "community-1", "Community", null, null, null, null,
            "assigner", "Assigner", DateTime.UtcNow, null);

        _mockModerationRepository.Setup(r => r.GetRoleByPublicIdAsync("role-1"))
            .ReturnsAsync(role);
        _mockModerationRepository.Setup(r => r.CanAdministerAsync("user-2", "community-1", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.RevokeRoleAsync("role-1", "user-2");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");

        _mockModerationRepository.Verify(r => r.RevokeRoleAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region GetUserRolesAsync Tests

    [Test]
    public async Task GetUserRolesAsync_ReturnsUserRoles()
    {
        // Arrange
        var roles = new List<UserRoleDto>
        {
            new("role-1", "user-1", "TestUser", "SpaceMod",
                "community-1", "Community", null, null, null, null,
                "admin", "Admin", DateTime.UtcNow, null),
            new("role-2", "user-1", "TestUser", "HubMod",
                "community-1", "Community", "hub-1", "Hub", null, null,
                "admin", "Admin", DateTime.UtcNow, null)
        };

        _mockModerationRepository.Setup(r => r.GetActiveRolesForUserAsync("user-1"))
            .ReturnsAsync(roles);

        // Act
        var result = await _useCase.GetUserRolesAsync("user-1");

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
    }

    #endregion

    #region CanModerateAsync Tests

    [Test]
    public async Task CanModerateAsync_WithModeratorPermissions_ReturnsTrue()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CanModerateAsync("user-1", "community-1", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.CanModerateAsync("user-1", "community-1");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_WithoutPermissions_ReturnsFalse()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CanModerateAsync("user-1", "community-1", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.CanModerateAsync("user-1", "community-1");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region CanAdministerAsync Tests

    [Test]
    public async Task CanAdministerAsync_WithAdminPermissions_ReturnsTrue()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CanAdministerAsync("admin-1", "community-1", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.CanAdministerAsync("admin-1", "community-1");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanAdministerAsync_WithoutPermissions_ReturnsFalse()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CanAdministerAsync("user-1", "community-1", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.CanAdministerAsync("user-1", "community-1");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region BanUserAsync Tests

    [Test]
    public async Task BanUserAsync_WithPermission_BansUser()
    {
        // Arrange
        const string targetUser = "user-1";
        const string banner = "mod-1";
        const string communityId = "community-1";

        var expectedBan = new UserBanDto(
            "ban-1", targetUser, "TestUser", "WriteOnly",
            communityId, "Community", null, null, null, null,
            "Spam", DateTime.UtcNow, null, banner, "Moderator", null, null, null);

        _mockModerationRepository.Setup(r => r.CanModerateAsync(banner, communityId, null, null))
            .ReturnsAsync(true);
        _mockModerationRepository.Setup(r => r.CanModerateAsync(targetUser, communityId, null, null))
            .ReturnsAsync(false);
        _mockModerationRepository.Setup(r => r.BanUserAsync(targetUser, BanType.WriteOnly, communityId, null, null, "Spam", null, banner))
            .ReturnsAsync(expectedBan);

        // Act
        var result = await _useCase.BanUserAsync(targetUser, BanType.WriteOnly, communityId, null, null, "Spam", null, banner);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.UserPublicId).IsEqualTo(targetUser);
    }

    [Test]
    public async Task BanUserAsync_WithoutPermission_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CanModerateAsync("user-1", "community-1", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.BanUserAsync("target-user", BanType.WriteOnly, "community-1", null, null, null, null, "user-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");
    }

    [Test]
    public async Task BanUserAsync_TargetIsModerator_ReturnsFailure()
    {
        // Arrange
        const string targetMod = "mod-target";
        const string banner = "mod-1";
        const string communityId = "community-1";

        _mockModerationRepository.Setup(r => r.CanModerateAsync(banner, communityId, null, null))
            .ReturnsAsync(true);
        _mockModerationRepository.Setup(r => r.CanModerateAsync(targetMod, communityId, null, null))
            .ReturnsAsync(true); // Target is a moderator

        // Act
        var result = await _useCase.BanUserAsync(targetMod, BanType.WriteOnly, communityId, null, null, null, null, banner);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("moderator privileges");
    }

    [Test]
    public async Task BanUserAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CanModerateAsync("mod-1", "community-1", null, null))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _useCase.BanUserAsync("user-1", BanType.WriteOnly, "community-1", null, null, null, null, "mod-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Database error");
    }

    #endregion

    #region UnbanUserAsync Tests

    [Test]
    public async Task UnbanUserAsync_WithValidBan_UnbansUser()
    {
        // Arrange
        const string banPublicId = "ban-1";
        const string unbanner = "mod-1";

        var ban = new UserBanDto(
            banPublicId, "user-1", "TestUser", "WriteOnly",
            "community-1", "Community", null, null, null, null,
            "Spam", DateTime.UtcNow, null, "original-mod", "OriginalMod", null, null, null);

        _mockModerationRepository.Setup(r => r.GetBanByPublicIdAsync(banPublicId))
            .ReturnsAsync(ban);
        _mockModerationRepository.Setup(r => r.CanModerateAsync(unbanner, "community-1", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.UnbanUserAsync(banPublicId, unbanner);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.UnbanUserAsync(banPublicId, unbanner), Times.Once);
    }

    [Test]
    public async Task UnbanUserAsync_WithNonExistentBan_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.GetBanByPublicIdAsync("non-existent"))
            .ReturnsAsync((UserBanDto?)null);

        // Act
        var result = await _useCase.UnbanUserAsync("non-existent", "mod-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Ban not found");
    }

    [Test]
    public async Task UnbanUserAsync_WithAlreadyUnbannedUser_ReturnsFailure()
    {
        // Arrange
        var ban = new UserBanDto(
            "ban-1", "user-1", "TestUser", "WriteOnly",
            "community-1", "Community", null, null, null, null,
            "Spam", DateTime.UtcNow, null, "mod-1", "Mod",
            DateTime.UtcNow, "mod-2", "Mod2"); // UnbannedAt is set

        _mockModerationRepository.Setup(r => r.GetBanByPublicIdAsync("ban-1"))
            .ReturnsAsync(ban);

        // Act
        var result = await _useCase.UnbanUserAsync("ban-1", "mod-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already unbanned");
    }

    [Test]
    public async Task UnbanUserAsync_WithoutPermission_ReturnsFailure()
    {
        // Arrange
        var ban = new UserBanDto(
            "ban-1", "user-1", "TestUser", "WriteOnly",
            "community-1", "Community", null, null, null, null,
            "Spam", DateTime.UtcNow, null, "mod-1", "Mod", null, null, null);

        _mockModerationRepository.Setup(r => r.GetBanByPublicIdAsync("ban-1"))
            .ReturnsAsync(ban);
        _mockModerationRepository.Setup(r => r.CanModerateAsync("user-2", "community-1", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.UnbanUserAsync("ban-1", "user-2");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");
    }

    #endregion

    #region IsUserBannedAsync Tests

    [Test]
    public async Task IsUserBannedAsync_WhenBanned_ReturnsTrue()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.IsUserBannedAsync("user-1", null, null, "space-1"))
            .ReturnsAsync(true);

        // Act
        var result = await _useCase.IsUserBannedAsync("user-1", "space-1");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsUserBannedAsync_WhenNotBanned_ReturnsFalse()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.IsUserBannedAsync("user-1", null, null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _useCase.IsUserBannedAsync("user-1");

        // Assert
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region CreateReportAsync Tests

    [Test]
    public async Task CreateReportAsync_WithReportedPost_CreatesReport()
    {
        // Arrange
        const string reporter = "user-1";
        const string reportedPost = "post-1";

        var expectedReport = new ReportDto(
            "report-1", "Pending", reporter, reportedPost, null, null,
            "reason-1", "Spam content", DateTime.UtcNow, null, null, null);

        _mockModerationRepository.Setup(r => r.CreateReportAsync(reporter, reportedPost, null, null, "reason-1", "Spam content"))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _useCase.CreateReportAsync(reporter, reportedPost, null, null, "reason-1", "Spam content");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.ReporterUserPublicId).IsEqualTo(reporter);
    }

    [Test]
    public async Task CreateReportAsync_WithNoReportedContent_ReturnsFailure()
    {
        // Arrange - no post, discussion, or user specified
        var result = await _useCase.CreateReportAsync("user-1", null, null, null, null, "Some details");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Must specify content to report");
    }

    [Test]
    public async Task CreateReportAsync_WithReportedDiscussion_CreatesReport()
    {
        // Arrange
        var expectedReport = new ReportDto(
            "report-1", "Pending", "user-1", null, "discussion-1", null,
            null, "Inappropriate title", DateTime.UtcNow, null, null, null);

        _mockModerationRepository.Setup(r => r.CreateReportAsync("user-1", null, "discussion-1", null, null, "Inappropriate title"))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _useCase.CreateReportAsync("user-1", null, "discussion-1", null, null, "Inappropriate title");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.ReportedDiscussionPublicId).IsEqualTo("discussion-1");
    }

    [Test]
    public async Task CreateReportAsync_WithReportedUser_CreatesReport()
    {
        // Arrange
        var expectedReport = new ReportDto(
            "report-1", "Pending", "user-1", null, null, "user-2",
            null, "Harassment", DateTime.UtcNow, null, null, null);

        _mockModerationRepository.Setup(r => r.CreateReportAsync("user-1", null, null, "user-2", null, "Harassment"))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _useCase.CreateReportAsync("user-1", null, null, "user-2", null, "Harassment");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.ReportedUserPublicId).IsEqualTo("user-2");
    }

    [Test]
    public async Task CreateReportAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.CreateReportAsync(
                It.IsAny<string>(), "post-1", It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _useCase.CreateReportAsync("user-1", "post-1", null, null, null, null);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("DB error");
    }

    #endregion

    #region ResolveReportAsync Tests

    [Test]
    public async Task ResolveReportAsync_WithPendingReport_ResolvesReport()
    {
        // Arrange
        var report = new ReportDto(
            "report-1", "Pending", "user-1", "post-1", null, null,
            null, null, DateTime.UtcNow, null, null, null);

        _mockModerationRepository.Setup(r => r.GetReportByPublicIdAsync("report-1"))
            .ReturnsAsync(report);

        // Act
        var result = await _useCase.ResolveReportAsync("report-1", "mod-1", "Action taken", false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.ResolveReportAsync("report-1", "mod-1", "Action taken", false), Times.Once);
    }

    [Test]
    public async Task ResolveReportAsync_WithNonExistentReport_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.GetReportByPublicIdAsync("non-existent"))
            .ReturnsAsync((ReportDto?)null);

        // Act
        var result = await _useCase.ResolveReportAsync("non-existent", "mod-1", null, false);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Report not found");
    }

    [Test]
    public async Task ResolveReportAsync_WithNonPendingReport_ReturnsFailure()
    {
        // Arrange
        var report = new ReportDto(
            "report-1", "Resolved", "user-1", "post-1", null, null,
            null, null, DateTime.UtcNow, DateTime.UtcNow, "mod-1", "Done");

        _mockModerationRepository.Setup(r => r.GetReportByPublicIdAsync("report-1"))
            .ReturnsAsync(report);

        // Act
        var result = await _useCase.ResolveReportAsync("report-1", "mod-1", null, false);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not pending");
    }

    [Test]
    public async Task ResolveReportAsync_WithDismiss_ResolvesAsDismissed()
    {
        // Arrange
        var report = new ReportDto(
            "report-1", "Pending", "user-1", "post-1", null, null,
            null, null, DateTime.UtcNow, null, null, null);

        _mockModerationRepository.Setup(r => r.GetReportByPublicIdAsync("report-1"))
            .ReturnsAsync(report);

        // Act
        var result = await _useCase.ResolveReportAsync("report-1", "mod-1", "Not a violation", true);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.ResolveReportAsync("report-1", "mod-1", "Not a violation", true), Times.Once);
    }

    #endregion

    #region AddReportCommentAsync Tests

    [Test]
    public async Task AddReportCommentAsync_WithValidData_AddsComment()
    {
        // Arrange
        var expectedComment = new ReportCommentDto(
            "comment-1", "mod-1", "Moderator", "Looking into this", DateTime.UtcNow, null);

        _mockModerationRepository.Setup(r => r.AddReportCommentAsync("report-1", "mod-1", "Looking into this"))
            .ReturnsAsync(expectedComment);

        // Act
        var result = await _useCase.AddReportCommentAsync("report-1", "mod-1", "Looking into this");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Content).IsEqualTo("Looking into this");
    }

    [Test]
    public async Task AddReportCommentAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.AddReportCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Report not found"));

        // Act
        var result = await _useCase.AddReportCommentAsync("invalid", "mod-1", "content");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetReportsForModeratorAsync Tests

    [Test]
    public async Task GetReportsForModeratorAsync_ReturnsPagedResults()
    {
        // Arrange
        var reports = new List<ReportListDto>
        {
            new("report-1", "Pending", "user-1", "User1", "post-1", "snippet",
                null, null, null, null, "Spam", null, DateTime.UtcNow, null, null, null, null,
                null, null, null, null, null, null, 0)
        };

        var pagedResult = new PagedResult<ReportListDto>
        {
            Items = reports,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockModerationRepository.Setup(r => r.GetReportsForModeratorAsync("mod-1", null, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetReportsForModeratorAsync("mod-1", null, 0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    #endregion

    #region GetReportDetailAsync Tests

    [Test]
    public async Task GetReportDetailAsync_WithExistingReport_ReturnsDetail()
    {
        // Arrange
        var detail = new ReportDetailDto(
            "report-1", "Pending", "user-1", "User1", "post-1", "Full content",
            null, null, null, null, "Spam", "Spam description", null,
            DateTime.UtcNow, null, null, null, null, null, null, null, null,
            null, null, []);

        _mockModerationRepository.Setup(r => r.GetReportDetailByPublicIdAsync("report-1"))
            .ReturnsAsync(detail);

        // Act
        var result = await _useCase.GetReportDetailAsync("report-1");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo("report-1");
    }

    [Test]
    public async Task GetReportDetailAsync_WithNonExistentReport_ReturnsNull()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.GetReportDetailByPublicIdAsync("non-existent"))
            .ReturnsAsync((ReportDetailDto?)null);

        // Act
        var result = await _useCase.GetReportDetailAsync("non-existent");

        // Assert
        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetPendingReportCountAsync Tests

    [Test]
    public async Task GetPendingReportCountAsync_ReturnsCount()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.GetPendingReportCountForModeratorAsync("mod-1"))
            .ReturnsAsync(5);

        // Act
        var result = await _useCase.GetPendingReportCountAsync("mod-1");

        // Assert
        await Assert.That(result).IsEqualTo(5);
    }

    #endregion

    #region ModeratorDeletePostAsync Tests

    [Test]
    public async Task ModeratorDeletePostAsync_WithValidData_DeletesPost()
    {
        // Act
        var result = await _useCase.ModeratorDeletePostAsync("post-1", "mod-1", "Rule violation");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.ModeratorDeletePostAsync("post-1", "mod-1", "Rule violation"), Times.Once);
    }

    [Test]
    public async Task ModeratorDeletePostAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.ModeratorDeletePostAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Post not found"));

        // Act
        var result = await _useCase.ModeratorDeletePostAsync("invalid", "mod-1", null);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post not found");
    }

    #endregion

    #region ModeratorDeleteDiscussionAsync Tests

    [Test]
    public async Task ModeratorDeleteDiscussionAsync_WithValidData_DeletesDiscussion()
    {
        // Act
        var result = await _useCase.ModeratorDeleteDiscussionAsync("discussion-1", "mod-1", "Spam thread");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.ModeratorDeleteDiscussionAsync("discussion-1", "mod-1", "Spam thread"), Times.Once);
    }

    [Test]
    public async Task ModeratorDeleteDiscussionAsync_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.ModeratorDeleteDiscussionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Discussion not found"));

        // Act
        var result = await _useCase.ModeratorDeleteDiscussionAsync("invalid", "mod-1", null);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region LockDiscussionAsync (Moderation) Tests

    [Test]
    public async Task LockDiscussionAsync_Moderation_WithValidData_LocksDiscussion()
    {
        // Act
        var result = await _useCase.LockDiscussionAsync("discussion-1", "mod-1", "Heated discussion");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.LockDiscussionAsync("discussion-1", "mod-1", "Heated discussion"), Times.Once);
    }

    [Test]
    public async Task LockDiscussionAsync_Moderation_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.LockDiscussionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Not found"));

        // Act
        var result = await _useCase.LockDiscussionAsync("invalid", "mod-1", null);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region UnlockDiscussionAsync (Moderation) Tests

    [Test]
    public async Task UnlockDiscussionAsync_Moderation_WithValidData_UnlocksDiscussion()
    {
        // Act
        var result = await _useCase.UnlockDiscussionAsync("discussion-1", "mod-1");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();

        _mockModerationRepository.Verify(r => r.UnlockDiscussionAsync("discussion-1", "mod-1"), Times.Once);
    }

    [Test]
    public async Task UnlockDiscussionAsync_Moderation_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        _mockModerationRepository.Setup(r => r.UnlockDiscussionAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Not found"));

        // Act
        var result = await _useCase.UnlockDiscussionAsync("invalid", "mod-1");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetReportReasonsAsync Tests

    [Test]
    public async Task GetReportReasonsAsync_ReturnsReasons()
    {
        // Arrange
        var reasons = new List<ReportReasonDto>
        {
            new("reason-1", "Spam", "Unwanted content", null, null, null, 1),
            new("reason-2", "Harassment", "Abusive behavior", null, null, null, 2)
        };

        _mockModerationRepository.Setup(r => r.GetReportReasonsForScopeAsync(null, null, null))
            .ReturnsAsync(reasons);

        // Act
        var result = await _useCase.GetReportReasonsAsync();

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetReportReasonsAsync_WithSpaceScope_PassesSpaceId()
    {
        // Arrange
        var reasons = new List<ReportReasonDto>
        {
            new("reason-1", "Off Topic", "Not relevant", null, null, "space-1", 1)
        };

        _mockModerationRepository.Setup(r => r.GetReportReasonsForScopeAsync(null, null, "space-1"))
            .ReturnsAsync(reasons);

        // Act
        var result = await _useCase.GetReportReasonsAsync("space-1");

        // Assert
        await Assert.That(result).Count().IsEqualTo(1);
    }

    #endregion

    #region GetModerationLogAsync Tests

    [Test]
    public async Task GetModerationLogAsync_WithSpaceId_ReturnsSpaceLogs()
    {
        // Arrange
        var logs = new List<ModerationLogDto>
        {
            new("log-1", "mod-1", "Moderator", "DeletePost", "post-1", null, null, null, null,
                null, null, null, null, "space-1", "Space", null, null, DateTime.UtcNow)
        };

        var pagedResult = new PagedResult<ModerationLogDto>
        {
            Items = logs,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockModerationRepository.Setup(r => r.GetModerationLogForSpaceAsync("space-1", 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetModerationLogAsync(null, null, "space-1", 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task GetModerationLogAsync_WithHubId_ReturnsHubLogs()
    {
        // Arrange
        var pagedResult = new PagedResult<ModerationLogDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockModerationRepository.Setup(r => r.GetModerationLogForHubAsync("hub-1", 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetModerationLogAsync(null, "hub-1", null, 0, 20);

        // Assert
        _mockModerationRepository.Verify(r => r.GetModerationLogForHubAsync("hub-1", 0, 20), Times.Once);
    }

    [Test]
    public async Task GetModerationLogAsync_WithCommunityId_ReturnsCommunityLogs()
    {
        // Arrange
        var pagedResult = new PagedResult<ModerationLogDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockModerationRepository.Setup(r => r.GetModerationLogForCommunityAsync("community-1", 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetModerationLogAsync("community-1", null, null, 0, 20);

        // Assert
        _mockModerationRepository.Verify(r => r.GetModerationLogForCommunityAsync("community-1", 0, 20), Times.Once);
    }

    [Test]
    public async Task GetModerationLogAsync_SpaceIdTakesPrecedence_OverHubAndCommunity()
    {
        // Arrange
        var pagedResult = new PagedResult<ModerationLogDto>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockModerationRepository.Setup(r => r.GetModerationLogForSpaceAsync("space-1", 0, 20))
            .ReturnsAsync(pagedResult);

        // Act - all three IDs provided, space should take precedence
        var result = await _useCase.GetModerationLogAsync("community-1", "hub-1", "space-1", 0, 20);

        // Assert
        _mockModerationRepository.Verify(r => r.GetModerationLogForSpaceAsync("space-1", 0, 20), Times.Once);
        _mockModerationRepository.Verify(r => r.GetModerationLogForHubAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockModerationRepository.Verify(r => r.GetModerationLogForCommunityAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task GetModerationLogAsync_WithNoScope_ReturnsEmptyResult()
    {
        // Act - no scope specified
        var result = await _useCase.GetModerationLogAsync(null, null, null, 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion
}
