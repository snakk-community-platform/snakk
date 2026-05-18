using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class ModerationUseCaseTests
{
    private readonly IModerationRepository _moderationRepository = Substitute.For<IModerationRepository>();
    private readonly IRevocationCache _revocationCache = Substitute.For<IRevocationCache>();
    private readonly IAuthVersionCache _authVersionCache = Substitute.For<IAuthVersionCache>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private ModerationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new ModerationUseCase(_moderationRepository, _revocationCache, _authVersionCache, _userRepository);
    }

    #region AssignRoleAsync Tests

    [Test]
    public async Task AssignRoleAsync_WithPermission_AssignsRole()
    {
        const string targetUser = "user-1";
        const string assigner = "admin-1";
        const string communityId = "community-1";
        var roleType = UserRoleType.SpaceMod;
        var expectedRole = new UserRoleDto("role-1", targetUser, "TestUser", "SpaceMod", communityId, "Community", null, null, null, null, assigner, "Admin", DateTime.UtcNow, null);

        _moderationRepository.CanAdministerAsync(assigner, communityId, null, null).Returns(true);
        _moderationRepository.AssignRoleAsync(targetUser, roleType, communityId, null, null, assigner).Returns(expectedRole);

        var result = await _useCase.AssignRoleAsync(targetUser, roleType, communityId, null, null, assigner);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.UserPublicId).IsEqualTo(targetUser);
        await Assert.That(result.Value.Role).IsEqualTo("SpaceMod");
    }

    [Test]
    public async Task AssignRoleAsync_WithoutPermission_ReturnsFailure()
    {
        const string targetUser = "user-1";
        const string assigner = "user-2";
        const string communityId = "community-1";
        _moderationRepository.CanAdministerAsync(assigner, communityId, null, null).Returns(false);

        var result = await _useCase.AssignRoleAsync(targetUser, UserRoleType.SpaceMod, communityId, null, null, assigner);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");
        await _moderationRepository.DidNotReceive().AssignRoleAsync(Arg.Any<string>(), Arg.Any<UserRoleType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Test]
    public async Task AssignRoleAsync_WhenExceptionThrown_ReturnsFailure()
    {
        const string assigner = "admin-1";
        const string communityId = "community-1";
        _moderationRepository.CanAdministerAsync(assigner, communityId, null, null).Returns<bool>(x => throw new Exception("Database error"));

        var result = await _useCase.AssignRoleAsync("user-1", UserRoleType.SpaceMod, communityId, null, null, assigner);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Database error");
    }

    #endregion

    #region RevokeRoleAsync Tests

    [Test]
    public async Task RevokeRoleAsync_WithValidRole_RevokesRole()
    {
        const string rolePublicId = "role-1";
        const string revoker = "admin-1";
        var role = new UserRoleDto(rolePublicId, "user-1", "TestUser", "SpaceMod", "community-1", "Community", null, null, null, null, "assigner", "Assigner", DateTime.UtcNow, null);

        _moderationRepository.GetRoleByPublicIdAsync(rolePublicId).Returns(role);
        _moderationRepository.CanAdministerAsync(revoker, "community-1", null, null).Returns(true);

        var result = await _useCase.RevokeRoleAsync(rolePublicId, revoker);

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).RevokeRoleAsync(rolePublicId, revoker);
    }

    [Test]
    public async Task RevokeRoleAsync_WithNonExistentRole_ReturnsFailure()
    {
        _moderationRepository.GetRoleByPublicIdAsync("non-existent").Returns((UserRoleDto?)null);

        var result = await _useCase.RevokeRoleAsync("non-existent", "admin-1");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task RevokeRoleAsync_WithAlreadyRevokedRole_ReturnsFailure()
    {
        var role = new UserRoleDto("role-1", "user-1", "TestUser", "SpaceMod", "community-1", "Community", null, null, null, null, "assigner", "Assigner", DateTime.UtcNow, DateTime.UtcNow);
        _moderationRepository.GetRoleByPublicIdAsync("role-1").Returns(role);

        var result = await _useCase.RevokeRoleAsync("role-1", "admin-1");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already revoked");
    }

    [Test]
    public async Task RevokeRoleAsync_WithoutPermission_ReturnsFailure()
    {
        var role = new UserRoleDto("role-1", "user-1", "TestUser", "SpaceMod", "community-1", "Community", null, null, null, null, "assigner", "Assigner", DateTime.UtcNow, null);
        _moderationRepository.GetRoleByPublicIdAsync("role-1").Returns(role);
        _moderationRepository.CanAdministerAsync("user-2", "community-1", null, null).Returns(false);

        var result = await _useCase.RevokeRoleAsync("role-1", "user-2");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");
        await _moderationRepository.DidNotReceive().RevokeRoleAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    #endregion

    #region GetUserRolesAsync Tests

    [Test]
    public async Task GetUserRolesAsync_ReturnsUserRoles()
    {
        var roles = new List<UserRoleDto>
        {
            new("role-1", "user-1", "TestUser", "SpaceMod", "community-1", "Community", null, null, null, null, "admin", "Admin", DateTime.UtcNow, null),
            new("role-2", "user-1", "TestUser", "HubMod", "community-1", "Community", "hub-1", "Hub", null, null, "admin", "Admin", DateTime.UtcNow, null)
        };
        _moderationRepository.GetActiveRolesForUserAsync("user-1").Returns(roles);

        var result = await _useCase.GetUserRolesAsync("user-1");

        await Assert.That(result).Count().IsEqualTo(2);
    }

    #endregion

    #region CanModerateAsync Tests

    [Test]
    public async Task CanModerateAsync_WithModeratorPermissions_ReturnsTrue()
    {
        _moderationRepository.CanModerateAsync("user-1", "community-1", null, null).Returns(true);

        var result = await _useCase.CanModerateAsync("user-1", "community-1");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanModerateAsync_WithoutPermissions_ReturnsFalse()
    {
        _moderationRepository.CanModerateAsync("user-1", "community-1", null, null).Returns(false);

        var result = await _useCase.CanModerateAsync("user-1", "community-1");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region CanAdministerAsync Tests

    [Test]
    public async Task CanAdministerAsync_WithAdminPermissions_ReturnsTrue()
    {
        _moderationRepository.CanAdministerAsync("admin-1", "community-1", null, null).Returns(true);

        var result = await _useCase.CanAdministerAsync("admin-1", "community-1");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CanAdministerAsync_WithoutPermissions_ReturnsFalse()
    {
        _moderationRepository.CanAdministerAsync("user-1", "community-1", null, null).Returns(false);

        var result = await _useCase.CanAdministerAsync("user-1", "community-1");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region BanUserAsync Tests

    [Test]
    public async Task BanUserAsync_WithPermission_BansUser()
    {
        const string targetUser = "user-1";
        const string banner = "mod-1";
        const string communityId = "community-1";
        var expectedBan = new UserBanDto("ban-1", targetUser, "TestUser", "WriteOnly", communityId, "Community", null, null, null, null, "Spam", DateTime.UtcNow, null, banner, "Moderator", null, null, null);

        _moderationRepository.CanModerateAsync(banner, communityId, null, null).Returns(true);
        _moderationRepository.CanModerateAsync(targetUser, communityId, null, null).Returns(false);
        _moderationRepository.BanUserAsync(targetUser, BanType.WriteOnly, communityId, null, null, "Spam", null, banner).Returns(expectedBan);

        var result = await _useCase.BanUserAsync(targetUser, BanType.WriteOnly, communityId, null, null, "Spam", null, banner);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.UserPublicId).IsEqualTo(targetUser);
    }

    [Test]
    public async Task BanUserAsync_WithoutPermission_ReturnsFailure()
    {
        _moderationRepository.CanModerateAsync("user-1", "community-1", null, null).Returns(false);

        var result = await _useCase.BanUserAsync("target-user", BanType.WriteOnly, "community-1", null, null, null, null, "user-1");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");
    }

    [Test]
    public async Task BanUserAsync_TargetIsModerator_ReturnsFailure()
    {
        const string targetMod = "mod-target";
        const string banner = "mod-1";
        const string communityId = "community-1";
        _moderationRepository.CanModerateAsync(banner, communityId, null, null).Returns(true);
        _moderationRepository.CanModerateAsync(targetMod, communityId, null, null).Returns(true);

        var result = await _useCase.BanUserAsync(targetMod, BanType.WriteOnly, communityId, null, null, null, null, banner);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("moderator privileges");
    }

    [Test]
    public async Task BanUserAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.CanModerateAsync("mod-1", "community-1", null, null).Returns<bool>(x => throw new Exception("Database error"));

        var result = await _useCase.BanUserAsync("user-1", BanType.WriteOnly, "community-1", null, null, null, null, "mod-1");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Database error");
    }

    #endregion

    #region UnbanUserAsync Tests

    [Test]
    public async Task UnbanUserAsync_WithValidBan_UnbansUser()
    {
        const string banPublicId = "ban-1";
        const string unbanner = "mod-1";
        var ban = new UserBanDto(banPublicId, "user-1", "TestUser", "WriteOnly", "community-1", "Community", null, null, null, null, "Spam", DateTime.UtcNow, null, "original-mod", "OriginalMod", null, null, null);

        _moderationRepository.GetBanByPublicIdAsync(banPublicId).Returns(ban);
        _moderationRepository.CanModerateAsync(unbanner, "community-1", null, null).Returns(true);

        var result = await _useCase.UnbanUserAsync(banPublicId, unbanner);

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).UnbanUserAsync(banPublicId, unbanner);
    }

    [Test]
    public async Task UnbanUserAsync_WithNonExistentBan_ReturnsFailure()
    {
        _moderationRepository.GetBanByPublicIdAsync("non-existent").Returns((UserBanDto?)null);

        var result = await _useCase.UnbanUserAsync("non-existent", "mod-1");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Ban not found");
    }

    [Test]
    public async Task UnbanUserAsync_WithAlreadyUnbannedUser_ReturnsFailure()
    {
        var ban = new UserBanDto("ban-1", "user-1", "TestUser", "WriteOnly", "community-1", "Community", null, null, null, null, "Spam", DateTime.UtcNow, null, "mod-1", "Mod", DateTime.UtcNow, "mod-2", "Mod2");
        _moderationRepository.GetBanByPublicIdAsync("ban-1").Returns(ban);

        var result = await _useCase.UnbanUserAsync("ban-1", "mod-1");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("already unbanned");
    }

    [Test]
    public async Task UnbanUserAsync_WithoutPermission_ReturnsFailure()
    {
        var ban = new UserBanDto("ban-1", "user-1", "TestUser", "WriteOnly", "community-1", "Community", null, null, null, null, "Spam", DateTime.UtcNow, null, "mod-1", "Mod", null, null, null);
        _moderationRepository.GetBanByPublicIdAsync("ban-1").Returns(ban);
        _moderationRepository.CanModerateAsync("user-2", "community-1", null, null).Returns(false);

        var result = await _useCase.UnbanUserAsync("ban-1", "user-2");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("permission");
    }

    #endregion

    #region IsUserBannedAsync Tests

    [Test]
    public async Task IsUserBannedAsync_WhenBanned_ReturnsTrue()
    {
        _moderationRepository.IsUserBannedAsync("user-1", null, null, "space-1").Returns(true);

        var result = await _useCase.IsUserBannedAsync("user-1", "space-1");

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsUserBannedAsync_WhenNotBanned_ReturnsFalse()
    {
        _moderationRepository.IsUserBannedAsync("user-1", null, null, null).Returns(false);

        var result = await _useCase.IsUserBannedAsync("user-1");

        await Assert.That(result).IsFalse();
    }

    #endregion

    #region CreateReportAsync Tests

    [Test]
    public async Task CreateReportAsync_WithReportedPost_CreatesReport()
    {
        const string reporter = "user-1";
        const string reportedPost = "post-1";
        var expectedReport = new ReportDto("report-1", "Pending", reporter, reportedPost, null, null, "reason-1", "Spam content", DateTime.UtcNow, null, null, null);
        _moderationRepository.CreateReportAsync(reporter, reportedPost, null, null, "reason-1", "Spam content").Returns(expectedReport);

        var result = await _useCase.CreateReportAsync(reporter, reportedPost, null, null, "reason-1", "Spam content");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.ReporterUserPublicId).IsEqualTo(reporter);
    }

    [Test]
    public async Task CreateReportAsync_WithNoReportedContent_ReturnsFailure()
    {
        var result = await _useCase.CreateReportAsync("user-1", null, null, null, null, "Some details");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Must specify content to report");
    }

    [Test]
    public async Task CreateReportAsync_WithReportedDiscussion_CreatesReport()
    {
        var expectedReport = new ReportDto("report-1", "Pending", "user-1", null, "discussion-1", null, null, "Inappropriate title", DateTime.UtcNow, null, null, null);
        _moderationRepository.CreateReportAsync("user-1", null, "discussion-1", null, null, "Inappropriate title").Returns(expectedReport);

        var result = await _useCase.CreateReportAsync("user-1", null, "discussion-1", null, null, "Inappropriate title");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.ReportedDiscussionPublicId).IsEqualTo("discussion-1");
    }

    [Test]
    public async Task CreateReportAsync_WithReportedUser_CreatesReport()
    {
        var expectedReport = new ReportDto("report-1", "Pending", "user-1", null, null, "user-2", null, "Harassment", DateTime.UtcNow, null, null, null);
        _moderationRepository.CreateReportAsync("user-1", null, null, "user-2", null, "Harassment").Returns(expectedReport);

        var result = await _useCase.CreateReportAsync("user-1", null, null, "user-2", null, "Harassment");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.ReportedUserPublicId).IsEqualTo("user-2");
    }

    [Test]
    public async Task CreateReportAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.CreateReportAsync(Arg.Any<string>(), "post-1", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns<ReportDto>(x => throw new Exception("DB error"));

        var result = await _useCase.CreateReportAsync("user-1", "post-1", null, null, null, null);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("DB error");
    }

    #endregion

    #region ResolveReportAsync Tests

    [Test]
    public async Task ResolveReportAsync_WithPendingReport_ResolvesReport()
    {
        var report = new ReportDto("report-1", "Pending", "user-1", "post-1", null, null, null, null, DateTime.UtcNow, null, null, null);
        _moderationRepository.GetReportByPublicIdAsync("report-1").Returns(report);

        var result = await _useCase.ResolveReportAsync("report-1", "mod-1", "Action taken", false);

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).ResolveReportAsync("report-1", "mod-1", "Action taken", false);
    }

    [Test]
    public async Task ResolveReportAsync_WithNonExistentReport_ReturnsFailure()
    {
        _moderationRepository.GetReportByPublicIdAsync("non-existent").Returns((ReportDto?)null);

        var result = await _useCase.ResolveReportAsync("non-existent", "mod-1", null, false);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Report not found");
    }

    [Test]
    public async Task ResolveReportAsync_WithNonPendingReport_ReturnsFailure()
    {
        var report = new ReportDto("report-1", "Resolved", "user-1", "post-1", null, null, null, null, DateTime.UtcNow, DateTime.UtcNow, "mod-1", "Done");
        _moderationRepository.GetReportByPublicIdAsync("report-1").Returns(report);

        var result = await _useCase.ResolveReportAsync("report-1", "mod-1", null, false);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not pending");
    }

    [Test]
    public async Task ResolveReportAsync_WithDismiss_ResolvesAsDismissed()
    {
        var report = new ReportDto("report-1", "Pending", "user-1", "post-1", null, null, null, null, DateTime.UtcNow, null, null, null);
        _moderationRepository.GetReportByPublicIdAsync("report-1").Returns(report);

        var result = await _useCase.ResolveReportAsync("report-1", "mod-1", "Not a violation", true);

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).ResolveReportAsync("report-1", "mod-1", "Not a violation", true);
    }

    #endregion

    #region AddReportCommentAsync Tests

    [Test]
    public async Task AddReportCommentAsync_WithValidData_AddsComment()
    {
        var expectedComment = new ReportCommentDto("comment-1", "mod-1", "Moderator", "Looking into this", DateTime.UtcNow, null);
        _moderationRepository.AddReportCommentAsync("report-1", "mod-1", "Looking into this").Returns(expectedComment);

        var result = await _useCase.AddReportCommentAsync("report-1", "mod-1", "Looking into this");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Content).IsEqualTo("Looking into this");
    }

    [Test]
    public async Task AddReportCommentAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.AddReportCommentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns<ReportCommentDto>(x => throw new Exception("Report not found"));

        var result = await _useCase.AddReportCommentAsync("invalid", "mod-1", "content");

        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetReportsForModeratorAsync Tests

    [Test]
    public async Task GetReportsForModeratorAsync_ReturnsPagedResults()
    {
        var reports = new List<ReportListDto>
        {
            new("report-1", "Pending", "user-1", "User1", "post-1", "snippet", null, null, null, null, "Spam", null, DateTime.UtcNow, null, null, null, null, null, null, null, null, null, null, 0)
        };
        var pagedResult = new PagedResult<ReportListDto> { Items = reports, Offset = 0, PageSize = 20, HasMoreItems = false };
        _moderationRepository.GetReportsForModeratorAsync("mod-1", null, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetReportsForModeratorAsync("mod-1", null, 0, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    #endregion

    #region GetReportDetailAsync Tests

    [Test]
    public async Task GetReportDetailAsync_WithExistingReport_ReturnsDetail()
    {
        var detail = new ReportDetailDto("report-1", "Pending", "user-1", "User1", "post-1", "Full content", null, null, null, null, "Spam", "Spam description", null, DateTime.UtcNow, null, null, null, null, null, null, null, null, null, null, []);
        _moderationRepository.GetReportDetailByPublicIdAsync("report-1").Returns(detail);

        var result = await _useCase.GetReportDetailAsync("report-1");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo("report-1");
    }

    [Test]
    public async Task GetReportDetailAsync_WithNonExistentReport_ReturnsNull()
    {
        _moderationRepository.GetReportDetailByPublicIdAsync("non-existent").Returns((ReportDetailDto?)null);

        var result = await _useCase.GetReportDetailAsync("non-existent");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetPendingReportCountAsync Tests

    [Test]
    public async Task GetPendingReportCountAsync_ReturnsCount()
    {
        _moderationRepository.GetPendingReportCountForModeratorAsync("mod-1").Returns(5);

        var result = await _useCase.GetPendingReportCountAsync("mod-1");

        await Assert.That(result).IsEqualTo(5);
    }

    #endregion

    #region ModeratorDeletePostAsync Tests

    [Test]
    public async Task ModeratorDeletePostAsync_WithValidData_DeletesPost()
    {
        var result = await _useCase.ModeratorDeletePostAsync("post-1", "mod-1", "Rule violation");

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).ModeratorDeletePostAsync("post-1", "mod-1", "Rule violation");
    }

    [Test]
    public async Task ModeratorDeletePostAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.ModeratorDeletePostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns<Task>(x => throw new Exception("Post not found"));

        var result = await _useCase.ModeratorDeletePostAsync("invalid", "mod-1", null);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post not found");
    }

    #endregion

    #region ModeratorDeleteDiscussionAsync Tests

    [Test]
    public async Task ModeratorDeleteDiscussionAsync_WithValidData_DeletesDiscussion()
    {
        var result = await _useCase.ModeratorDeleteDiscussionAsync("discussion-1", "mod-1", "Spam thread");

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).ModeratorDeleteDiscussionAsync("discussion-1", "mod-1", "Spam thread");
    }

    [Test]
    public async Task ModeratorDeleteDiscussionAsync_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.ModeratorDeleteDiscussionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns<Task>(x => throw new Exception("Discussion not found"));

        var result = await _useCase.ModeratorDeleteDiscussionAsync("invalid", "mod-1", null);

        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region LockDiscussionAsync (Moderation) Tests

    [Test]
    public async Task LockDiscussionAsync_Moderation_WithValidData_LocksDiscussion()
    {
        var result = await _useCase.LockDiscussionAsync("discussion-1", "mod-1", "Heated discussion");

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).LockDiscussionAsync("discussion-1", "mod-1", "Heated discussion");
    }

    [Test]
    public async Task LockDiscussionAsync_Moderation_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.LockDiscussionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns<Task>(x => throw new Exception("Not found"));

        var result = await _useCase.LockDiscussionAsync("invalid", "mod-1", null);

        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region UnlockDiscussionAsync (Moderation) Tests

    [Test]
    public async Task UnlockDiscussionAsync_Moderation_WithValidData_UnlocksDiscussion()
    {
        var result = await _useCase.UnlockDiscussionAsync("discussion-1", "mod-1");

        await Assert.That(result.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).UnlockDiscussionAsync("discussion-1", "mod-1");
    }

    [Test]
    public async Task UnlockDiscussionAsync_Moderation_WhenExceptionThrown_ReturnsFailure()
    {
        _moderationRepository.UnlockDiscussionAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<Task>(x => throw new Exception("Not found"));

        var result = await _useCase.UnlockDiscussionAsync("invalid", "mod-1");

        await Assert.That(result.IsSuccess).IsFalse();
    }

    #endregion

    #region GetReportReasonsAsync Tests

    [Test]
    public async Task GetReportReasonsAsync_ReturnsReasons()
    {
        var reasons = new List<ReportReasonDto>
        {
            new("reason-1", "Spam", "Unwanted content", null, null, null, 1),
            new("reason-2", "Harassment", "Abusive behavior", null, null, null, 2)
        };
        _moderationRepository.GetReportReasonsForScopeAsync(null, null, null).Returns(reasons);

        var result = await _useCase.GetReportReasonsAsync();

        await Assert.That(result).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetReportReasonsAsync_WithSpaceScope_PassesSpaceId()
    {
        var reasons = new List<ReportReasonDto> { new("reason-1", "Off Topic", "Not relevant", null, null, "space-1", 1) };
        _moderationRepository.GetReportReasonsForScopeAsync(null, null, "space-1").Returns(reasons);

        var result = await _useCase.GetReportReasonsAsync("space-1");

        await Assert.That(result).Count().IsEqualTo(1);
    }

    #endregion

    #region GetModerationLogAsync Tests

    [Test]
    public async Task GetModerationLogAsync_WithSpaceId_ReturnsSpaceLogs()
    {
        var logs = new List<ModerationLogDto> { new("log-1", "mod-1", "Moderator", "DeletePost", "post-1", null, null, null, null, null, null, null, null, "space-1", "Space", null, null, DateTime.UtcNow) };
        var pagedResult = new PagedResult<ModerationLogDto> { Items = logs, Offset = 0, PageSize = 20, HasMoreItems = false };
        _moderationRepository.GetModerationLogForSpaceAsync("space-1", 0, 20).Returns(pagedResult);

        var result = await _useCase.GetModerationLogAsync(null, null, "space-1", 0, 20);

        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task GetModerationLogAsync_WithHubId_ReturnsHubLogs()
    {
        var pagedResult = new PagedResult<ModerationLogDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _moderationRepository.GetModerationLogForHubAsync("hub-1", 0, 20).Returns(pagedResult);

        var result = await _useCase.GetModerationLogAsync(null, "hub-1", null, 0, 20);

        await _moderationRepository.Received(1).GetModerationLogForHubAsync("hub-1", 0, 20);
    }

    [Test]
    public async Task GetModerationLogAsync_WithCommunityId_ReturnsCommunityLogs()
    {
        var pagedResult = new PagedResult<ModerationLogDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _moderationRepository.GetModerationLogForCommunityAsync("community-1", 0, 20).Returns(pagedResult);

        var result = await _useCase.GetModerationLogAsync("community-1", null, null, 0, 20);

        await _moderationRepository.Received(1).GetModerationLogForCommunityAsync("community-1", 0, 20);
    }

    [Test]
    public async Task GetModerationLogAsync_SpaceIdTakesPrecedence_OverHubAndCommunity()
    {
        var pagedResult = new PagedResult<ModerationLogDto> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _moderationRepository.GetModerationLogForSpaceAsync("space-1", 0, 20).Returns(pagedResult);

        var result = await _useCase.GetModerationLogAsync("community-1", "hub-1", "space-1", 0, 20);

        await _moderationRepository.Received(1).GetModerationLogForSpaceAsync("space-1", 0, 20);
        await _moderationRepository.DidNotReceive().GetModerationLogForHubAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>());
        await _moderationRepository.DidNotReceive().GetModerationLogForCommunityAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Test]
    public async Task GetModerationLogAsync_WithNoScope_ReturnsEmptyResult()
    {
        var result = await _useCase.GetModerationLogAsync(null, null, null, 0, 20);

        await Assert.That(result.Items).Count().IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion
}
