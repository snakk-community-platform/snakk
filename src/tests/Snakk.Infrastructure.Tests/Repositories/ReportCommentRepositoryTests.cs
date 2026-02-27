using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class ReportCommentRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ReportCommentRepository _repository;

    public ReportCommentRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new ReportCommentRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region Helper Methods

    private async Task<(ReportDatabaseEntity Report, UserDatabaseEntity Reporter, UserDatabaseEntity Commenter)> CreateReportWithContextAsync()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var reporter = await _builder.CreateUserAsync("Reporter");
        var commenter = await _builder.CreateUserAsync("Commenter");
        var reason = await _builder.CreateReportReasonAsync();

        var report = await _builder.CreateReportAsync(
            reporter.Id, reason.Id,
            reportedPostId: post.Id,
            communityId: community.Id);

        return (report, reporter, commenter);
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingNonDeletedComment_ReturnsWithAuthorUser()
    {
        var (report, _, commenter) = await CreateReportWithContextAsync();
        var comment = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Test comment");

        var result = await _repository.GetByPublicIdAsync(comment.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(comment.PublicId);
        await Assert.That(result.Content).IsEqualTo("Test comment");
        await Assert.That(result.AuthorUser).IsNotNull();
        await Assert.That(result.AuthorUser.DisplayName).IsEqualTo("Commenter");
        await Assert.That(result.Report).IsNotNull();
        await Assert.That(result.Report.Id).IsEqualTo(report.Id);
    }

    [Test]
    public async Task GetByPublicIdAsync_DeletedComment_ReturnsNull()
    {
        var (report, _, commenter) = await CreateReportWithContextAsync();
        var comment = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Deleted comment");
        comment.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetByPublicIdAsync(comment.PublicId);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetCommentsForReportAsync Tests

    [Test]
    public async Task GetCommentsForReportAsync_MultipleComments_ReturnsNonDeletedOrderedByCreatedAt()
    {
        var (report, _, commenter) = await CreateReportWithContextAsync();

        var comment1 = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "First comment");
        comment1.CreatedAt = DateTime.UtcNow.AddMinutes(-10);

        var comment2 = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Second comment");
        comment2.CreatedAt = DateTime.UtcNow.AddMinutes(-5);

        var comment3 = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Third comment");
        comment3.CreatedAt = DateTime.UtcNow;

        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetCommentsForReportAsync(report.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0].Content).IsEqualTo("First comment");
        await Assert.That(result[1].Content).IsEqualTo("Second comment");
        await Assert.That(result[2].Content).IsEqualTo("Third comment");
        // Verify AuthorUser is included
        await Assert.That(result[0].AuthorUser).IsNotNull();
    }

    [Test]
    public async Task GetCommentsForReportAsync_DeletedCommentsExcluded()
    {
        var (report, _, commenter) = await CreateReportWithContextAsync();

        await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Active comment");
        var deletedComment = await _builder.CreateReportCommentAsync(report.Id, commenter.Id, "Deleted comment");
        deletedComment.IsDeleted = true;
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetCommentsForReportAsync(report.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Content).IsEqualTo("Active comment");
    }

    [Test]
    public async Task GetCommentsForReportAsync_NoComments_ReturnsEmpty()
    {
        var (report, _, _) = await CreateReportWithContextAsync();

        var result = (await _repository.GetCommentsForReportAsync(report.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion
}
