using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Comprehensive workflow tests for moderation scenarios
/// </summary>
public class ModerationWorkflowTests
{
    private readonly IModerationRepository _moderationRepository = Substitute.For<IModerationRepository>();
    private ModerationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new ModerationUseCase(_moderationRepository);
    }

    #region Report Created -> Moderator Reviews -> Resolves With Action

    [Test]
    public async Task Workflow_ReportCreated_ModeratorReviews_ResolvesWithAction()
    {
        // Step 1: User creates a report
        const string reporter = "user-reporter";
        const string reportedPost = "post-offensive";

        var expectedReport = new ReportDto(
            "report-1", "Pending", reporter, reportedPost, null, null,
            "reason-spam", "This is spam content", DateTime.UtcNow, null, null, null);

        _moderationRepository.CreateReportAsync(reporter, reportedPost, null, null, "reason-spam", "This is spam content")
            .Returns(expectedReport);

        var createResult = await _useCase.CreateReportAsync(reporter, reportedPost, null, null, "reason-spam", "This is spam content");

        await Assert.That(createResult.IsSuccess).IsTrue();
        await Assert.That(createResult.Value!.Status).IsEqualTo("Pending");

        // Step 2: Moderator reviews the report detail
        var reportDetail = new ReportDetailDto(
            "report-1", "Pending", reporter, "Reporter Name", reportedPost, "Offensive content here",
            null, null, null, null, "Spam", "Spam description", null,
            DateTime.UtcNow, null, null, null, null, null, null, null, null,
            null, null, []);

        _moderationRepository.GetReportDetailByPublicIdAsync("report-1")
            .Returns(reportDetail);

        var detail = await _useCase.GetReportDetailAsync("report-1");
        await Assert.That(detail).IsNotNull();
        await Assert.That(detail!.Status).IsEqualTo("Pending");

        // Step 3: Moderator resolves the report with an action
        _moderationRepository.GetReportByPublicIdAsync("report-1")
            .Returns(expectedReport);

        var resolveResult = await _useCase.ResolveReportAsync("report-1", "mod-1", "Post removed for spam", false);

        await Assert.That(resolveResult.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).ResolveReportAsync("report-1", "mod-1", "Post removed for spam", false);
    }

    #endregion

    #region Report Created -> Moderator Dismisses

    [Test]
    public async Task Workflow_ReportCreated_ModeratorDismisses()
    {
        // Step 1: Report is created
        const string reporter = "user-1";
        const string reportedUser = "user-2";

        var expectedReport = new ReportDto(
            "report-dismiss", "Pending", reporter, null, null, reportedUser,
            null, "I don't like them", DateTime.UtcNow, null, null, null);

        _moderationRepository.CreateReportAsync(reporter, null, null, reportedUser, null, "I don't like them")
            .Returns(expectedReport);

        var createResult = await _useCase.CreateReportAsync(reporter, null, null, reportedUser, null, "I don't like them");
        await Assert.That(createResult.IsSuccess).IsTrue();

        // Step 2: Moderator dismisses the report
        _moderationRepository.GetReportByPublicIdAsync("report-dismiss")
            .Returns(expectedReport);

        var dismissResult = await _useCase.ResolveReportAsync("report-dismiss", "mod-1", "Not a violation", true);

        await Assert.That(dismissResult.IsSuccess).IsTrue();
        await _moderationRepository.Received(1).ResolveReportAsync("report-dismiss", "mod-1", "Not a violation", true);
    }

    #endregion

    #region User Banned -> Tries to Create Content -> Verify Ban Check

    [Test]
    public async Task Workflow_UserBanned_BanCheckReturnsTrue()
    {
        // Step 1: Moderator bans user
        const string targetUser = "spammer";
        const string moderator = "mod-1";
        const string communityId = "community-1";

        var expectedBan = new UserBanDto(
            "ban-1", targetUser, "Spammer", "Full",
            communityId, "Community", null, null, null, null,
            "Repeated spam", DateTime.UtcNow, null, moderator, "Moderator", null, null, null);

        _moderationRepository.CanModerateAsync(moderator, communityId, null, null)
            .Returns(true);
        _moderationRepository.CanModerateAsync(targetUser, communityId, null, null)
            .Returns(false);
        _moderationRepository.BanUserAsync(targetUser, BanType.ReadWrite, communityId, null, null, "Repeated spam", null, moderator)
            .Returns(expectedBan);

        var banResult = await _useCase.BanUserAsync(targetUser, BanType.ReadWrite, communityId, null, null, "Repeated spam", null, moderator);
        await Assert.That(banResult.IsSuccess).IsTrue();

        // Step 2: Verify the user is banned when content creation is attempted
        _moderationRepository.IsUserBannedAsync(targetUser, null, null, null)
            .Returns(true);

        var isBanned = await _useCase.IsUserBannedAsync(targetUser);
        await Assert.That(isBanned).IsTrue();
    }

    [Test]
    public async Task Workflow_UserBanned_ThenUnbanned_BanCheckReturnsFalse()
    {
        // Step 1: User gets banned
        const string targetUser = "temp-banned";
        const string moderator = "mod-1";
        const string communityId = "community-1";

        var ban = new UserBanDto(
            "ban-temp", targetUser, "User", "WriteOnly",
            communityId, "Community", null, null, null, null,
            "Cool down", DateTime.UtcNow, null, moderator, "Moderator", null, null, null);

        _moderationRepository.CanModerateAsync(moderator, communityId, null, null)
            .Returns(true);
        _moderationRepository.CanModerateAsync(targetUser, communityId, null, null)
            .Returns(false);
        _moderationRepository.BanUserAsync(targetUser, BanType.WriteOnly, communityId, null, null, "Cool down", null, moderator)
            .Returns(ban);

        await _useCase.BanUserAsync(targetUser, BanType.WriteOnly, communityId, null, null, "Cool down", null, moderator);

        // Step 2: Moderator unbans the user
        _moderationRepository.GetBanByPublicIdAsync("ban-temp")
            .Returns(ban);

        var unbanResult = await _useCase.UnbanUserAsync("ban-temp", moderator);
        await Assert.That(unbanResult.IsSuccess).IsTrue();

        // Step 3: User is no longer banned
        _moderationRepository.IsUserBannedAsync(targetUser, null, null, null)
            .Returns(false);

        var isBanned = await _useCase.IsUserBannedAsync(targetUser);
        await Assert.That(isBanned).IsFalse();
    }

    #endregion

    #region Role Assigned -> Verify Permissions -> Role Revoked -> Verify Lost Permissions

    [Test]
    public async Task Workflow_RoleAssigned_VerifyPermissions_RoleRevoked_PermissionsLost()
    {
        // Step 1: Assign a SpaceMod role to a user
        const string targetUser = "user-promoted";
        const string admin = "admin-1";
        const string communityId = "community-1";
        const string spaceId = "space-1";

        var assignedRole = new UserRoleDto(
            "role-1", targetUser, "User", "SpaceMod",
            communityId, "Community", null, null, spaceId, "Space",
            admin, "Admin", DateTime.UtcNow, null);

        _moderationRepository.CanAdministerAsync(admin, communityId, null, spaceId)
            .Returns(true);
        _moderationRepository.AssignRoleAsync(targetUser, UserRoleType.SpaceMod, communityId, null, spaceId, admin)
            .Returns(assignedRole);

        var assignResult = await _useCase.AssignRoleAsync(targetUser, UserRoleType.SpaceMod, communityId, null, spaceId, admin);
        await Assert.That(assignResult.IsSuccess).IsTrue();

        // Step 2: Verify user now has moderation permissions
        _moderationRepository.CanModerateAsync(targetUser, null, null, spaceId)
            .Returns(true);

        var canModerate = await _useCase.CanModerateAsync(targetUser, spacePublicId: spaceId);
        await Assert.That(canModerate).IsTrue();

        // Step 3: Revoke the role
        _moderationRepository.GetRoleByPublicIdAsync("role-1")
            .Returns(assignedRole);
        _moderationRepository.CanAdministerAsync(admin, communityId, null, spaceId)
            .Returns(true);

        var revokeResult = await _useCase.RevokeRoleAsync("role-1", admin);
        await Assert.That(revokeResult.IsSuccess).IsTrue();

        // Step 4: Verify user no longer has moderation permissions
        _moderationRepository.CanModerateAsync(targetUser, null, null, spaceId)
            .Returns(false);

        var canModerateAfterRevoke = await _useCase.CanModerateAsync(targetUser, spacePublicId: spaceId);
        await Assert.That(canModerateAfterRevoke).IsFalse();
    }

    #endregion

    #region Report With Comment Workflow

    [Test]
    public async Task Workflow_ReportCreated_CommentAdded_Resolved()
    {
        // Step 1: Report created
        var report = new ReportDto(
            "report-comment", "Pending", "user-1", "post-1", null, null,
            "reason-1", "Potential harassment", DateTime.UtcNow, null, null, null);

        _moderationRepository.CreateReportAsync("user-1", "post-1", null, null, "reason-1", "Potential harassment")
            .Returns(report);

        var createResult = await _useCase.CreateReportAsync("user-1", "post-1", null, null, "reason-1", "Potential harassment");
        await Assert.That(createResult.IsSuccess).IsTrue();

        // Step 2: Moderator adds a comment
        var comment = new ReportCommentDto(
            "comment-1", "mod-1", "Moderator", "Investigating this report", DateTime.UtcNow, null);

        _moderationRepository.AddReportCommentAsync("report-comment", "mod-1", "Investigating this report")
            .Returns(comment);

        var commentResult = await _useCase.AddReportCommentAsync("report-comment", "mod-1", "Investigating this report");
        await Assert.That(commentResult.IsSuccess).IsTrue();
        await Assert.That(commentResult.Value!.Content).IsEqualTo("Investigating this report");

        // Step 3: Moderator resolves after investigation
        _moderationRepository.GetReportByPublicIdAsync("report-comment")
            .Returns(report);

        var resolveResult = await _useCase.ResolveReportAsync("report-comment", "mod-1", "Warning issued to user", false);
        await Assert.That(resolveResult.IsSuccess).IsTrue();
    }

    #endregion

    #region Cannot Ban a Moderator

    [Test]
    public async Task Workflow_CannotBanModeratorAtSameScope()
    {
        // Arrange - target user is a moderator at the same scope
        const string targetMod = "mod-target";
        const string banner = "mod-banner";
        const string communityId = "community-1";

        _moderationRepository.CanModerateAsync(banner, communityId, null, null)
            .Returns(true);
        _moderationRepository.CanModerateAsync(targetMod, communityId, null, null)
            .Returns(true);

        // Act
        var result = await _useCase.BanUserAsync(targetMod, BanType.ReadWrite, communityId, null, null, "Abuse", null, banner);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("moderator privileges");
    }

    #endregion

    #region Content Moderation Workflow

    [Test]
    public async Task Workflow_ModeratorDeletesPost_ThenLocksDiscussion()
    {
        // Step 1: Moderator deletes an offending post
        var deletePostResult = await _useCase.ModeratorDeletePostAsync("post-offensive", "mod-1", "Violates community guidelines");
        await Assert.That(deletePostResult.IsSuccess).IsTrue();

        // Step 2: Moderator locks the discussion to prevent further issues
        var lockResult = await _useCase.LockDiscussionAsync("discussion-heated", "mod-1", "Locked due to repeated violations");
        await Assert.That(lockResult.IsSuccess).IsTrue();

        // Verify both actions were called
        await _moderationRepository.Received(1).ModeratorDeletePostAsync("post-offensive", "mod-1", "Violates community guidelines");
        await _moderationRepository.Received(1).LockDiscussionAsync("discussion-heated", "mod-1", "Locked due to repeated violations");
    }

    [Test]
    public async Task Workflow_ModeratorLocksAndUnlocksDiscussion()
    {
        // Step 1: Lock discussion
        var lockResult = await _useCase.LockDiscussionAsync("discussion-1", "mod-1", "Heated debate");
        await Assert.That(lockResult.IsSuccess).IsTrue();

        // Step 2: Unlock after cooldown
        var unlockResult = await _useCase.UnlockDiscussionAsync("discussion-1", "mod-1");
        await Assert.That(unlockResult.IsSuccess).IsTrue();

        await _moderationRepository.Received(1).LockDiscussionAsync("discussion-1", "mod-1", "Heated debate");
        await _moderationRepository.Received(1).UnlockDiscussionAsync("discussion-1", "mod-1");
    }

    #endregion

    #region Moderation Log Priority

    [Test]
    public async Task Workflow_ModerationLog_SpaceScope_TakesPrecedenceOverHub()
    {
        // Arrange
        var spaceLog = new PagedResult<ModerationLogDto>
        {
            Items =
            [
                new("log-1", "mod-1", "Mod", "DeletePost", "post-1", null, null, null, null,
                    null, null, null, null, "space-1", "Space", null, null, DateTime.UtcNow)
            ],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _moderationRepository.GetModerationLogForSpaceAsync("space-1", 0, 20)
            .Returns(spaceLog);

        // Act - provide both space and hub, space should win
        var result = await _useCase.GetModerationLogAsync("community-1", "hub-1", "space-1", 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(1);
        await _moderationRepository.Received(1).GetModerationLogForSpaceAsync("space-1", 0, 20);
        await _moderationRepository.DidNotReceive().GetModerationLogForHubAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>());
    }

    #endregion
}
