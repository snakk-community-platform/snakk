using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class PostRevisionTests
{
    #region Create Tests

    [Test]
    public async Task Create_SetsAllProperties()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var revision = PostRevision.Create(postId, "Test content", userId, 1);

        await Assert.That(revision.PostId).IsEqualTo(postId);
        await Assert.That(revision.Content).IsEqualTo("Test content");
        await Assert.That(revision.EditedByUserId).IsEqualTo(userId);
        await Assert.That(revision.RevisionNumber).IsEqualTo(1);
    }

    [Test]
    public async Task Create_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var revision = PostRevision.Create(PostId.New(), "content", UserId.New(), 1);
        var after = DateTime.UtcNow;

        await Assert.That(revision.CreatedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(revision.CreatedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Create_EmptyContent_ThrowsArgumentException()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        await Assert.That(() => PostRevision.Create(postId, "", userId, 1))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_NullContent_ThrowsArgumentException()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        await Assert.That(() => PostRevision.Create(postId, null!, userId, 1))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WhitespaceContent_ThrowsArgumentException()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        await Assert.That(() => PostRevision.Create(postId, "   ", userId, 1))
            .Throws<ArgumentException>();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_SetsAllProperties()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        var createdAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        var revision = PostRevision.Rehydrate(postId, "Rehydrated content", userId, 3, createdAt);

        await Assert.That(revision.PostId).IsEqualTo(postId);
        await Assert.That(revision.Content).IsEqualTo("Rehydrated content");
        await Assert.That(revision.EditedByUserId).IsEqualTo(userId);
        await Assert.That(revision.RevisionNumber).IsEqualTo(3);
        await Assert.That(revision.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Rehydrate_PreservesCreatedAt()
    {
        var specificDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var revision = PostRevision.Rehydrate(PostId.New(), "content", UserId.New(), 1, specificDate);

        await Assert.That(revision.CreatedAt).IsEqualTo(specificDate);
    }

    [Test]
    public async Task Rehydrate_WithRevisionNumberZero_Works()
    {
        var revision = PostRevision.Rehydrate(PostId.New(), "content", UserId.New(), 0, DateTime.UtcNow);

        await Assert.That(revision.RevisionNumber).IsEqualTo(0);
    }

    [Test]
    public async Task Rehydrate_WithLargeRevisionNumber_Works()
    {
        var revision = PostRevision.Rehydrate(PostId.New(), "content", UserId.New(), 99999, DateTime.UtcNow);

        await Assert.That(revision.RevisionNumber).IsEqualTo(99999);
    }

    #endregion
}
