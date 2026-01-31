using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class PostMapperTests
{
    #region ToPersistence Tests

    [Fact]
    public void ToPersistence_WithNewPost_MapsAllProperties()
    {
        // Arrange
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Test content");

        // Act
        var entity = post.ToPersistence();

        // Assert
        entity.Should().NotBeNull();
        entity.PublicId.Should().Be(post.PublicId);
        entity.Content.Should().Be("Test content");
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        // Note: LastModifiedAt is nullable and is NOT set when a post is created (only set when edited/deleted)
        entity.LastModifiedAt.Should().BeNull();
        entity.EditedAt.Should().BeNull();
        entity.IsFirstPost.Should().BeFalse();
        entity.IsDeleted.Should().BeFalse();
        entity.RevisionCount.Should().Be(0);
    }

    [Fact]
    public void ToPersistence_WithReply_MapsCorrectly()
    {
        // Arrange
        var replyToPostId = PostId.New();
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Reply content", replyToPostId: replyToPostId);

        // Act
        var entity = post.ToPersistence();

        // Assert
        entity.Content.Should().Be("Reply content");
        // Note: ReplyToPostId is set by repository adapter, not by mapper
    }

    [Fact]
    public void ToPersistence_WithEditedPost_MapsEditedAt()
    {
        // Arrange
        var userId = UserId.New();
        var post = Post.Create(DiscussionId.New(), userId, "Original content");
        post.UpdateContent("Updated content", userId);

        // Act
        var entity = post.ToPersistence();

        // Assert
        entity.Content.Should().Be("Updated content");
        entity.EditedAt.Should().NotBeNull();
        entity.EditedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        entity.RevisionCount.Should().Be(1);
    }

    [Fact]
    public void ToPersistence_WithSoftDeletedPost_MapsIsDeletedTrue()
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
        entity.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void ToPersistence_WithFirstPost_MapsIsFirstPostTrue()
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
            0);

        // Act
        var entity = post.ToPersistence();

        // Assert
        entity.IsFirstPost.Should().BeTrue();
    }

    #endregion

    #region FromPersistence Tests

    [Fact]
    public void FromPersistence_WithBasicPost_ReconstructsPost()
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
        post.Should().NotBeNull();
        post.PublicId.Value.Should().Be(entity.PublicId);
        post.Content.Should().Be("Test content");
        post.DiscussionId.Value.Should().Be(discussionPublicId);
        post.CreatedByUserId.Value.Should().Be(userPublicId);
        post.CreatedAt.Should().Be(entity.CreatedAt);
        post.LastModifiedAt.Should().Be(entity.LastModifiedAt);
        post.EditedAt.Should().BeNull();
        post.IsFirstPost.Should().BeFalse();
        post.ReplyToPostId.Should().BeNull();
        post.IsDeleted.Should().BeFalse();
        post.RevisionCount.Should().Be(0);
    }

    [Fact]
    public void FromPersistence_WithReply_MapsReplyToPostId()
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
        post.ReplyToPostId.Should().NotBeNull();
        post.ReplyToPostId!.Value.Should().Be(replyToPostPublicId);
    }

    [Fact]
    public void FromPersistence_WithNullReplyToPost_MapsNullReplyToPostId()
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
        post.ReplyToPostId.Should().BeNull();
    }

    [Fact]
    public void FromPersistence_WithEditedPost_MapsEditedAt()
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
        post.EditedAt.Should().Be(editedAt);
        post.RevisionCount.Should().Be(2);
    }

    [Fact]
    public void FromPersistence_WithSoftDeletedPost_MapsIsDeletedTrue()
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
        post.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void FromPersistence_WithFirstPost_MapsIsFirstPostTrue()
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
        post.IsFirstPost.Should().BeTrue();
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_WithNewPost_PreservesAllData()
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
        reconstructedPost.PublicId.Should().Be(originalPost.PublicId);
        reconstructedPost.Content.Should().Be(originalPost.Content);
        reconstructedPost.DiscussionId.Should().Be(discussionId);
        reconstructedPost.CreatedByUserId.Should().Be(userId);
        reconstructedPost.IsFirstPost.Should().Be(originalPost.IsFirstPost);
        reconstructedPost.IsDeleted.Should().Be(originalPost.IsDeleted);
        reconstructedPost.RevisionCount.Should().Be(originalPost.RevisionCount);
        reconstructedPost.EditedAt.Should().Be(originalPost.EditedAt);
    }

    [Fact]
    public void RoundTrip_WithReply_PreservesReplyToPostId()
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
        reconstructedPost.ReplyToPostId.Should().NotBeNull();
        reconstructedPost.ReplyToPostId.Should().Be(replyToPostId);
    }

    [Fact]
    public void RoundTrip_WithEditedPost_PreservesEditedAtAndRevisionCount()
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
        reconstructedPost.EditedAt.Should().NotBeNull();
        reconstructedPost.RevisionCount.Should().Be(1);
        reconstructedPost.Content.Should().Be("Updated content");
    }

    [Fact]
    public void RoundTrip_WithDeletedPost_PreservesIsDeleted()
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
        reconstructedPost.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToPersistence_PreservesCreatedAt()
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
        entity.CreatedAt.Should().Be(specificTime);
    }

    [Fact]
    public void FromPersistence_PreservesAllTimestamps()
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
        post.CreatedAt.Should().Be(createdAt);
        post.LastModifiedAt.Should().Be(lastModifiedAt);
        post.EditedAt.Should().Be(editedAt);
    }

    #endregion
}
