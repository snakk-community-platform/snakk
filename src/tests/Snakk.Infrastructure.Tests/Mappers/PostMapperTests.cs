using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class PostMapperTests
{
    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_WithNewPost_MapsAllProperties()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Test content");

        // Act
        var entity = post.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(post.PublicId);
        await Assert.That(entity.Content).IsEqualTo("Test content");
        await Assert.That((DateTime.UtcNow - entity.CreatedAt).TotalSeconds).IsLessThan(1);
        // Note: LastModifiedAt is nullable and is NOT set when a post is created (only set when edited/deleted)
        await Assert.That(entity.LastModifiedAt).IsNull();
        await Assert.That(entity.EditedAt).IsNull();
        await Assert.That(entity.IsFirstPost).IsFalse();
        await Assert.That(entity.IsDeleted).IsFalse();
        await Assert.That(entity.RevisionCount).IsEqualTo(0);
    }

    [Test]
    public async Task ToPersistence_WithReply_MapsCorrectly()
    {
        // Arrange
        var replyToPostId = PostId.New();
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Reply content", replyToPostId: replyToPostId);

        // Act
        var entity = post.ToPersistence();

        // Assert
        await Assert.That(entity.Content).IsEqualTo("Reply content");
        // Note: ReplyToPostId is set by repository adapter, not by mapper
    }

    [Test]
    public async Task ToPersistence_WithEditedPost_MapsEditedAt()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original content");
        post.UpdateContent("Updated content", userId);

        // Act
        var entity = post.ToPersistence();

        // Assert
        await Assert.That(entity.Content).IsEqualTo("Updated content");
        await Assert.That(entity.EditedAt).IsNotNull();
        await Assert.That((DateTime.UtcNow - entity.EditedAt!.Value).TotalSeconds).IsLessThan(1);
        await Assert.That(entity.RevisionCount).IsEqualTo(1);
    }

    [Test]
    public async Task ToPersistence_WithSoftDeletedPost_MapsIsDeletedTrue()
    {
        // Arrange
        var userId = UserId.New();
        // Create old post (> 5 minutes ago) so it will be soft deleted
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), DiscussionId.New(), userId, "Content", sixMinutesAgo);
        post.SoftDelete(userId);

        // Act
        var entity = post.ToPersistence();

        // Assert
        await Assert.That(entity.IsDeleted).IsTrue();
    }

    [Test]
    public async Task ToPersistence_WithFirstPost_MapsIsFirstPostTrue()
    {
        // Arrange
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "Content",
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            isFirstPost: true,
            null,
            false,
            false);

        // Act
        var entity = post.ToPersistence();

        // Assert
        await Assert.That(entity.IsFirstPost).IsTrue();
    }

    #endregion

    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_WithBasicPost_ReconstructsPost()
    {
        // Arrange
        var discussionPublicId = Guid.NewGuid().ToString();
        var userPublicId = Guid.NewGuid().ToString();
        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "Test content",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow.AddHours(-1),
            EditedAt = null,
            IsFirstPost = false,
            IsDeleted = false,
            RevisionCount = 0,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = discussionPublicId,
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = null,
            ReplyToPostId = null
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That(post).IsNotNull();
        await Assert.That(post.PublicId.Value).IsEqualTo(entity.PublicId);
        await Assert.That(post.Content).IsEqualTo("Test content");
        await Assert.That(post.DiscussionId.Value).IsEqualTo(discussionPublicId);
        await Assert.That(post.CreatedByUserId.Value).IsEqualTo(userPublicId);
        await Assert.That(post.CreatedAt).IsEqualTo(entity.CreatedAt);
        await Assert.That(post.LastModifiedAt).IsEqualTo(entity.LastModifiedAt);
        await Assert.That(post.EditedAt).IsNull();
        await Assert.That(post.IsFirstPost).IsFalse();
        await Assert.That((object?)post.ReplyToPostId).IsNull();
        await Assert.That(post.IsDeleted).IsFalse();
        await Assert.That(post.RevisionCount).IsEqualTo(0);
    }

    [Test]
    public async Task FromPersistence_WithReply_MapsReplyToPostId()
    {
        // Arrange
        var replyToPostPublicId = Guid.NewGuid().ToString();
        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "Reply content",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            EditedAt = null,
            IsFirstPost = false,
            IsDeleted = false,
            RevisionCount = 0,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = new PostDatabaseEntity
            {
                PublicId = replyToPostPublicId,
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPostId = 123 // Database foreign key ID
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That(post.ReplyToPostId!).IsNotNull();
        await Assert.That(post.ReplyToPostId!.Value).IsEqualTo(replyToPostPublicId);
    }

    [Test]
    public async Task FromPersistence_WithNullReplyToPost_MapsNullReplyToPostId()
    {
        // Arrange
        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "Content",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            EditedAt = null,
            IsFirstPost = false,
            IsDeleted = false,
            RevisionCount = 0,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = null,
            ReplyToPostId = null
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That((object?)post.ReplyToPostId).IsNull();
    }

    [Test]
    public async Task FromPersistence_WithEditedPost_MapsEditedAt()
    {
        // Arrange
        var editedAt = DateTime.UtcNow.AddMinutes(-30);
        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "Edited content",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastModifiedAt = DateTime.UtcNow.AddMinutes(-30),
            EditedAt = editedAt,
            IsFirstPost = false,
            IsDeleted = false,
            RevisionCount = 2,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = null,
            ReplyToPostId = null
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That(post.EditedAt).IsEqualTo(editedAt);
        await Assert.That(post.RevisionCount).IsEqualTo(2);
    }

    [Test]
    public async Task FromPersistence_WithSoftDeletedPost_MapsIsDeletedTrue()
    {
        // Arrange
        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "[deleted]",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastModifiedAt = DateTime.UtcNow,
            EditedAt = null,
            IsFirstPost = false,
            IsDeleted = true,
            RevisionCount = 0,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = null,
            ReplyToPostId = null
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That(post.IsDeleted).IsTrue();
    }

    [Test]
    public async Task FromPersistence_WithFirstPost_MapsIsFirstPostTrue()
    {
        // Arrange
        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "First post content",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            EditedAt = null,
            IsFirstPost = true,
            IsDeleted = false,
            RevisionCount = 0,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = null,
            ReplyToPostId = null
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That(post.IsFirstPost).IsTrue();
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_WithNewPost_PreservesAllData()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var originalPost = Post.Create(discussionId, userId, "Test content");

        // Act
        var entity = originalPost.ToPersistence();
        // Simulate repository setting navigation properties
        entity.Discussion = new DiscussionDatabaseEntity
        {
            PublicId = discussionId.Value,
            Slug = "test",
            Title = "test",
            CreatedAt = DateTime.UtcNow
        };
        entity.CreatedByUser = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        var reconstructedPost = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedPost.PublicId).IsEqualTo(originalPost.PublicId);
        await Assert.That(reconstructedPost.Content).IsEqualTo(originalPost.Content);
        await Assert.That(reconstructedPost.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(reconstructedPost.CreatedByUserId).IsEqualTo(userId);
        await Assert.That(reconstructedPost.IsFirstPost).IsEqualTo(originalPost.IsFirstPost);
        await Assert.That(reconstructedPost.IsDeleted).IsEqualTo(originalPost.IsDeleted);
        await Assert.That(reconstructedPost.RevisionCount).IsEqualTo(originalPost.RevisionCount);
        await Assert.That(reconstructedPost.EditedAt).IsEqualTo(originalPost.EditedAt);
    }

    [Test]
    public async Task RoundTrip_WithReply_PreservesReplyToPostId()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();
        var originalPost = Post.Create(discussionId, userId, "Reply content", replyToPostId: replyToPostId);

        // Act
        var entity = originalPost.ToPersistence();
        entity.Discussion = new DiscussionDatabaseEntity
        {
            PublicId = discussionId.Value,
            Slug = "test",
            Title = "test",
            CreatedAt = DateTime.UtcNow
        };
        entity.CreatedByUser = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.ReplyToPost = new PostDatabaseEntity
        {
            PublicId = replyToPostId.Value,
            Content = "test",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.ReplyToPostId = 123; // Database FK
        var reconstructedPost = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedPost.ReplyToPostId!).IsNotNull();
        await Assert.That(reconstructedPost.ReplyToPostId!).IsEqualTo(replyToPostId);
    }

    [Test]
    public async Task RoundTrip_WithEditedPost_PreservesEditedAtAndRevisionCount()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var originalPost = Post.Create(discussionId, userId, "Original content");
        originalPost.UpdateContent("Updated content", userId);

        // Act
        var entity = originalPost.ToPersistence();
        entity.Discussion = new DiscussionDatabaseEntity
        {
            PublicId = discussionId.Value,
            Slug = "test",
            Title = "test",
            CreatedAt = DateTime.UtcNow
        };
        entity.CreatedByUser = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        var reconstructedPost = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedPost.EditedAt).IsNotNull();
        await Assert.That(reconstructedPost.RevisionCount).IsEqualTo(1);
        await Assert.That(reconstructedPost.Content).IsEqualTo("Updated content");
    }

    [Test]
    public async Task RoundTrip_WithDeletedPost_PreservesIsDeleted()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var originalPost = Post.Rehydrate(PostId.New(), discussionId, userId, "Content", sixMinutesAgo);
        originalPost.SoftDelete(userId);

        // Act
        var entity = originalPost.ToPersistence();
        entity.Discussion = new DiscussionDatabaseEntity
        {
            PublicId = discussionId.Value,
            Slug = "test",
            Title = "test",
            CreatedAt = DateTime.UtcNow
        };
        entity.CreatedByUser = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        var reconstructedPost = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedPost.IsDeleted).IsTrue();
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task ToPersistence_PreservesCreatedAt()
    {
        // Arrange
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var post = Post.Rehydrate(
            PostId.New(),
            DiscussionId.New(),
            UserId.New(),
            "Content",
            specificTime);

        // Act
        var entity = post.ToPersistence();

        // Assert
        await Assert.That(entity.CreatedAt).IsEqualTo(specificTime);
    }

    [Test]
    public async Task FromPersistence_PreservesAllTimestamps()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastModifiedAt = new DateTime(2024, 1, 15, 11, 0, 0, DateTimeKind.Utc);
        var editedAt = new DateTime(2024, 1, 15, 11, 30, 0, DateTimeKind.Utc);

        var entity = new PostDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            Content = "Content",
            CreatedAt = createdAt,
            LastModifiedAt = lastModifiedAt,
            EditedAt = editedAt,
            IsFirstPost = false,
            IsDeleted = false,
            RevisionCount = 1,
            Discussion = new DiscussionDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test",
                Title = "test",
                CreatedAt = DateTime.UtcNow
            },
            CreatedByUser = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            ReplyToPost = null,
            ReplyToPostId = null
        };

        // Act
        var post = entity.FromPersistence();

        // Assert
        await Assert.That(post.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(post.LastModifiedAt).IsEqualTo(lastModifiedAt);
        await Assert.That(post.EditedAt).IsEqualTo(editedAt);
    }

    #endregion
}
