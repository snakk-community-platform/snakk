using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class DiscussionReadStateTests
{
    #region Create Tests

    [Test]
    public async Task Create_SetsPropertiesCorrectly()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var lastReadPostId = PostId.New();

        // Act
        var readState = DiscussionReadState.Create(userId, discussionId, lastReadPostId);

        // Assert
        await Assert.That(readState.UserId).IsEqualTo(userId);
        await Assert.That(readState.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(readState.LastReadPostId).IsEqualTo(lastReadPostId);
        await Assert.That(readState.LastReadAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WithoutLastReadPostId_SetsItToNull()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var readState = DiscussionReadState.Create(userId, discussionId);

        // Assert
        await Assert.That((object?)readState.LastReadPostId).IsNull();
    }

    #endregion

    #region MarkAsRead Tests

    [Test]
    public async Task MarkAsRead_UpdatesLastReadPostIdAndLastReadAt()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var readState = DiscussionReadState.Create(userId, discussionId);
        var newPostId = PostId.New();

        // Act
        readState.MarkAsRead(newPostId);

        // Assert
        await Assert.That(readState.LastReadPostId).IsEqualTo(newPostId);
        await Assert.That(readState.LastReadAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task MarkAsRead_CanBeCalledMultipleTimes()
    {
        // Arrange
        var readState = DiscussionReadState.Create(UserId.New(), DiscussionId.New());
        var firstPostId = PostId.New();
        var secondPostId = PostId.New();

        // Act
        readState.MarkAsRead(firstPostId);
        readState.MarkAsRead(secondPostId);

        // Assert
        await Assert.That(readState.LastReadPostId).IsEqualTo(secondPostId);
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var lastReadPostId = PostId.New();
        var lastReadAt = DateTime.UtcNow.AddHours(-2);

        // Act
        var readState = DiscussionReadState.Rehydrate(userId, discussionId, lastReadPostId, lastReadAt);

        // Assert
        await Assert.That(readState.UserId).IsEqualTo(userId);
        await Assert.That(readState.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(readState.LastReadPostId).IsEqualTo(lastReadPostId);
        await Assert.That(readState.LastReadAt).IsEqualTo(lastReadAt);
    }

    #endregion
}
