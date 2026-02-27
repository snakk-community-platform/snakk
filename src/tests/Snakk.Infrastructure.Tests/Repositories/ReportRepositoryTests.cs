using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;
using Snakk.Shared.Models;

namespace Snakk.Infrastructure.Tests.Repositories;

public class ReportRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ReportRepository _repository;

    public ReportRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new ReportRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingReportOnPost_ReturnsWithNavigationProperties()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var reporter = await _builder.CreateUserAsync("Reporter");
        var reason = await _builder.CreateReportReasonAsync("Spam");

        var report = await _builder.CreateReportAsync(
            reporter.Id, reason.Id,
            reportedPostId: post.Id,
            communityId: community.Id,
            hubId: hub.Id,
            spaceId: space.Id);

        var result = await _repository.GetByPublicIdAsync(report.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(report.PublicId);
        await Assert.That(result.ReporterUser).IsNotNull();
        await Assert.That(result.ReporterUser.DisplayName).IsEqualTo("Reporter");
        await Assert.That(result.ReportedPost).IsNotNull();
        await Assert.That(result.ReportedPost!.Id).IsEqualTo(post.Id);
        await Assert.That(result.Reason).IsNotNull();
        await Assert.That(result.Reason!.Name).IsEqualTo("Spam");
        await Assert.That(result.Community).IsNotNull();
        await Assert.That(result.Hub).IsNotNull();
        await Assert.That(result.Space).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdWithCommentsAsync Tests

    [Test]
    public async Task GetByPublicIdWithCommentsAsync_ExistingWithComments_ReturnsWithNonDeletedComments()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var reporter = await _builder.CreateUserAsync("Reporter");
        var commenter = await _builder.CreateUserAsync("Commenter");
        var reason = await _builder.CreateReportReasonAsync();

        var report = await _builder.CreateReportAsync(
            reporter.Id, reason.Id,
            reportedPostId: post.Id,
            communityId: community.Id);

        var comment1 = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "First comment");
        var comment2 = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Second comment");

        var result = await _repository.GetByPublicIdWithCommentsAsync(report.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Comments.Count).IsEqualTo(2);
        foreach (var comment in result.Comments)
        {
            await Assert.That(comment.AuthorUser).IsNotNull();
            await Assert.That(comment.AuthorUser.DisplayName).IsEqualTo("Commenter");
        }
    }

    [Test]
    public async Task GetByPublicIdWithCommentsAsync_DeletedCommentsAreExcluded()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var reporter = await _builder.CreateUserAsync("Reporter");
        var commenter = await _builder.CreateUserAsync("Commenter");
        var reason = await _builder.CreateReportReasonAsync();

        var report = await _builder.CreateReportAsync(
            reporter.Id, reason.Id,
            reportedPostId: post.Id,
            communityId: community.Id);

        var activeComment = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Active comment");
        var deletedComment = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Deleted comment");
        deletedComment.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        // Use a separate context to avoid change tracker interference with filtered includes
        using var separateContext = _db.CreateSeparateContext();
        var separateRepository = new ReportRepository(separateContext);
        var result = await separateRepository.GetByPublicIdWithCommentsAsync(report.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Comments.Count).IsEqualTo(1);
        await Assert.That(result.Comments.First().Content).IsEqualTo("Active comment");
    }

    #endregion

    #region GetReportsForCommunityAsync Tests

    [Test]
    public async Task GetReportsForCommunityAsync_ReturnsReportsForCommunity()
    {
        var reporter = await _builder.CreateUserAsync("Reporter");
        var reason = await _builder.CreateReportReasonAsync();
        var community1 = await _builder.CreateCommunityAsync("Community One");
        var community2 = await _builder.CreateCommunityAsync("Community Two");

        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community1.Id);
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community1.Id);
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community2.Id);

        var result = await _repository.GetReportsForCommunityAsync(community1.Id, null, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task GetReportsForCommunityAsync_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        var reporter = await _builder.CreateUserAsync("Reporter");
        var resolver = await _builder.CreateUserAsync("Resolver");
        var reason = await _builder.CreateReportReasonAsync();
        var community = await _builder.CreateCommunityAsync();

        // Create pending report (StatusId = 1 by default from builder)
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);

        // Create resolved report
        var resolvedReport = await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);
        resolvedReport.StatusId = (int)ReportStatusEnum.Resolved;
        resolvedReport.ResolvedByUserId = resolver.Id;
        resolvedReport.ResolvedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        var pendingResult = await _repository.GetReportsForCommunityAsync(community.Id, (int)ReportStatusEnum.Pending, 0, 10);
        var resolvedResult = await _repository.GetReportsForCommunityAsync(community.Id, (int)ReportStatusEnum.Resolved, 0, 10);

        await Assert.That(pendingResult.Items.Count()).IsEqualTo(1);
        await Assert.That(resolvedResult.Items.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task GetReportsForCommunityAsync_WithoutStatusFilter_ReturnsAllStatuses()
    {
        var reporter = await _builder.CreateUserAsync("Reporter");
        var resolver = await _builder.CreateUserAsync("Resolver");
        var reason = await _builder.CreateReportReasonAsync();
        var community = await _builder.CreateCommunityAsync();

        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);

        var resolvedReport = await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);
        resolvedReport.StatusId = (int)ReportStatusEnum.Resolved;
        resolvedReport.ResolvedByUserId = resolver.Id;
        resolvedReport.ResolvedAt = DateTime.UtcNow;
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetReportsForCommunityAsync(community.Id, null, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
    }

    #endregion

    #region GetReportsForHubAsync Tests

    [Test]
    public async Task GetReportsForHubAsync_IncludesHubLevelAndSpaceLevelReports()
    {
        var reporter = await _builder.CreateUserAsync("Reporter");
        var reason = await _builder.CreateReportReasonAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);

        // Hub-level report
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id, hubId: hub.Id);

        // Space-level report within the hub
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id, spaceId: space.Id);

        var result = await _repository.GetReportsForHubAsync(hub.Id, null, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
    }

    #endregion

    #region GetReportsForSpaceAsync Tests

    [Test]
    public async Task GetReportsForSpaceAsync_ReturnsOnlyReportsForSpecificSpace()
    {
        var reporter = await _builder.CreateUserAsync("Reporter");
        var reason = await _builder.CreateReportReasonAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space1 = await _builder.CreateSpaceAsync(hub.Id);
        var space2 = await _builder.CreateSpaceAsync(hub.Id);

        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id, spaceId: space1.Id);
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id, spaceId: space1.Id);
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id, spaceId: space2.Id);

        var result = await _repository.GetReportsForSpaceAsync(space1.Id, null, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
    }

    #endregion

    #region GetPendingReportCountForModeratorAsync Tests

    [Test]
    public async Task GetPendingReportCountForModeratorAsync_GlobalAdmin_SeesAllPendingReports()
    {
        var admin = await _builder.CreateUserAsync("Admin");
        var reporter = await _builder.CreateUserAsync("Reporter");
        var reason = await _builder.CreateReportReasonAsync();
        var community1 = await _builder.CreateCommunityAsync();
        var community2 = await _builder.CreateCommunityAsync();

        await _builder.AssignRoleAsync(admin.Id, UserRoleTypeEnum.GlobalAdmin, admin.Id);

        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community1.Id);
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community2.Id);
        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community1.Id);

        var count = await _repository.GetPendingReportCountForModeratorAsync(admin.Id);

        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task GetPendingReportCountForModeratorAsync_UserWithNoRoles_ReturnsZero()
    {
        var user = await _builder.CreateUserAsync("RegularUser");
        var reporter = await _builder.CreateUserAsync("Reporter");
        var reason = await _builder.CreateReportReasonAsync();
        var community = await _builder.CreateCommunityAsync();

        await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);

        var count = await _repository.GetPendingReportCountForModeratorAsync(user.Id);

        await Assert.That(count).IsEqualTo(0);
    }

    #endregion

    #region GetReportsResolvedByUserAsync Tests

    [Test]
    public async Task GetReportsResolvedByUserAsync_ReturnsReportsResolvedByUser()
    {
        var reporter = await _builder.CreateUserAsync("Reporter");
        var resolver1 = await _builder.CreateUserAsync("Resolver1");
        var resolver2 = await _builder.CreateUserAsync("Resolver2");
        var reason = await _builder.CreateReportReasonAsync();
        var community = await _builder.CreateCommunityAsync();

        var report1 = await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);
        report1.StatusId = (int)ReportStatusEnum.Resolved;
        report1.ResolvedByUserId = resolver1.Id;
        report1.ResolvedAt = DateTime.UtcNow;

        var report2 = await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);
        report2.StatusId = (int)ReportStatusEnum.Resolved;
        report2.ResolvedByUserId = resolver1.Id;
        report2.ResolvedAt = DateTime.UtcNow;

        var report3 = await _builder.CreateReportAsync(reporter.Id, reason.Id, reportedUserId: reporter.Id, communityId: community.Id);
        report3.StatusId = (int)ReportStatusEnum.Resolved;
        report3.ResolvedByUserId = resolver2.Id;
        report3.ResolvedAt = DateTime.UtcNow;

        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetReportsResolvedByUserAsync(resolver1.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task GetReportsResolvedByUserAsync_NoResolvedReports_ReturnsEmptyResult()
    {
        var resolver = await _builder.CreateUserAsync("Resolver");

        var result = await _repository.GetReportsResolvedByUserAsync(resolver.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion
}
