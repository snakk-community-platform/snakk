using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

public class PostHtmlRendererTests
{
    private readonly PostHtmlRenderer _renderer = new();


    #region Helper Methods

    private static Post CreatePost(
        string publicId = "post_123",
        string content = "Hello world",
        string? renderedContent = null,
        DateTime? editedAt = null,
        bool isFirstPost = false)
    {
        return Post.Rehydrate(
            PostId.From(publicId),
            DiscussionId.From("disc_1"),
            UserId.From("user_1"),
            content,
            renderedContent ?? $"<p>{content}</p>",
            new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            lastModifiedAt: null,
            editedAt: editedAt,
            isFirstPost: isFirstPost);
    }

    private static User CreateUser(
        string publicId = "user_1",
        string displayName = "TestUser")
    {
        return User.Rehydrate(
            UserId.From(publicId),
            displayName,
            email: "test@example.com",
            passwordHash: "hash",
            emailVerified: true,
            emailVerificationToken: null,
            oauthProvider: null,
            oauthProviderId: null,
            role: null,
            avatarFileName: null,
            avatarRevision: 0,
            autoFollowOnReply: true,
            createdAt: DateTime.UtcNow);
    }

    #endregion

    #region RenderPostCard Tests

    [Test]
    public async Task RenderPostCard_ContainsRenderedContent()
    {
        // Arrange
        var post = CreatePost(content: "My test content");
        var author = CreateUser();

        // Act
        var result = _renderer.RenderPostCard(post, author, "hub-slug", "space-slug", "discussion-slug", "temp_user_1");

        // Assert
        await Assert.That(result).Contains("<p>My test content</p>");
    }

    [Test]
    public async Task RenderPostCard_ContainsPostPublicIdInElementId()
    {
        // Arrange
        var post = CreatePost(publicId: "post_abc");
        var author = CreateUser();

        // Act
        var result = _renderer.RenderPostCard(post, author, "hub-slug", "space-slug", "discussion-slug", "temp_user_1");

        // Assert
        await Assert.That(result).Contains("post-post_abc");
    }

    [Test]
    public async Task RenderPostCard_ContainsEditedIndicator_WhenEditedAtHasValue()
    {
        // Arrange
        var post = CreatePost(editedAt: DateTime.UtcNow);
        var author = CreateUser();

        // Act
        var result = _renderer.RenderPostCard(post, author, "hub-slug", "space-slug", "discussion-slug", "temp_user_1");

        // Assert
        await Assert.That(result).Contains("(edited)");
    }

    [Test]
    public async Task RenderPostCard_DoesNotContainEditedIndicator_WhenEditedAtIsNull()
    {
        // Arrange
        var post = CreatePost(editedAt: null);
        var author = CreateUser();

        // Act
        var result = _renderer.RenderPostCard(post, author, "hub-slug", "space-slug", "discussion-slug", "temp_user_1");

        // Assert
        await Assert.That(result).DoesNotContain("(edited)");
    }

    [Test]
    public async Task RenderPostCard_ContainsOriginalPostBadge_WhenIsFirstPost()
    {
        // Arrange
        var post = CreatePost(isFirstPost: true);
        var author = CreateUser();

        // Act
        var result = _renderer.RenderPostCard(post, author, "hub-slug", "space-slug", "discussion-slug", "temp_user_1");

        // Assert
        await Assert.That(result).Contains("Original Post");
    }

    [Test]
    public async Task RenderPostCard_DoesNotContainOriginalPostBadge_WhenIsNotFirstPost()
    {
        // Arrange
        var post = CreatePost(isFirstPost: false);
        var author = CreateUser();

        // Act
        var result = _renderer.RenderPostCard(post, author, "hub-slug", "space-slug", "discussion-slug", "temp_user_1");

        // Assert
        await Assert.That(result).DoesNotContain("Original Post");
    }

    #endregion

    #region RenderPostContent Tests

    [Test]
    public async Task RenderPostContent_ContainsRenderedContent()
    {
        // Arrange
        var post = CreatePost(content: "Some markup content");

        // Act
        var result = _renderer.RenderPostContent(post);

        // Assert
        await Assert.That(result).Contains("<p>Some markup content</p>");
    }

    [Test]
    public async Task RenderPostContent_ContainsEditedIndicator_WhenEdited()
    {
        // Arrange
        var post = CreatePost(editedAt: DateTime.UtcNow);

        // Act
        var result = _renderer.RenderPostContent(post);

        // Assert
        await Assert.That(result).Contains("(edited)");
    }

    [Test]
    public async Task RenderPostContent_DoesNotContainEditedIndicator_WhenNotEdited()
    {
        // Arrange
        var post = CreatePost(editedAt: null);

        // Act
        var result = _renderer.RenderPostContent(post);

        // Assert
        await Assert.That(result).DoesNotContain("(edited)");
    }

    #endregion

    #region RenderTombstone Tests

    [Test]
    public async Task RenderTombstone_ContainsDeletedMessage()
    {
        // Act
        var result = _renderer.RenderTombstone();

        // Assert
        await Assert.That(result).Contains("[This post has been deleted]");
    }

    [Test]
    public async Task RenderTombstone_ContainsExpectedCssClasses()
    {
        // Act
        var result = _renderer.RenderTombstone();

        // Assert
        await Assert.That(result).Contains("card bg-base-100 shadow-md mb-4");
        await Assert.That(result).Contains("card-body");
    }

    #endregion
}
