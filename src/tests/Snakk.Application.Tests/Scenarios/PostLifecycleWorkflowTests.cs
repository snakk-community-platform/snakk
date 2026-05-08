using NSubstitute;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain;
using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Comprehensive workflow tests for post lifecycle scenarios
/// </summary>
public class PostLifecycleWorkflowTests
{
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IDiscussionRepository _discussionRepository = Substitute.For<IDiscussionRepository>();
    private readonly ISpaceRepository _spaceRepository = Substitute.For<ISpaceRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDomainEventDispatcher _eventDispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly ICounterService _counterService = Substitute.For<ICounterService>();
    private readonly IMediaService _mediaService = Substitute.For<IMediaService>();
    private readonly IMarkupParser _markupParser = Substitute.For<IMarkupParser>();
    private readonly IModerationRepository _moderationRepository = Substitute.For<IModerationRepository>();
    private ReactionUseCase _reactionUseCase = null!;
    private PostUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _reactionUseCase = Substitute.For<ReactionUseCase>(
            Substitute.For<IReactionRepository>(),
            Substitute.For<IPostRepository>(),
            Substitute.For<IRealtimeNotifier>(),
            Substitute.For<ICounterService>());

        _markupParser.ToHtml(Arg.Any<string>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");
        _markupParser.ToHtml(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");

        _useCase = new PostUseCase(
            _postRepository,
            _discussionRepository,
            _spaceRepository,
            _userRepository,
            _followRepository,
            _eventDispatcher,
            _realtimeNotifier,
            _counterService,
            _mediaService,
            _markupParser,
            new ContentNormalizer(),
            _moderationRepository,
            _reactionUseCase);
    }

    [Test]
    public async Task CompletePostLifecycle_CreateEditDelete_WorksEndToEnd()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId)
            .Returns(user);

        // Step 1: Create post
        var createResult = await _useCase.CreatePostAsync(discussionId, userId, "Original content");

        // Assert creation
        await Assert.That(createResult.IsSuccess).IsTrue();
        var post = createResult.Value!;
        await Assert.That(post.Content).IsEqualTo("Original content");
        await Assert.That(post.RevisionCount).IsEqualTo(0);
        await Assert.That(post.IsDeleted).IsFalse();

        await _postRepository.Received(1).AddAsync(Arg.Any<Post>());
        await _counterService.Received(1).IncrementPostCountAsync(discussionId);

        // Step 2: Edit post
        _postRepository.GetByPublicIdAsync(post.PublicId)
            .Returns(post);

        var updateResult = await _useCase.UpdatePostAsync(post.PublicId, userId, "Updated content");

        // Assert update
        await Assert.That(updateResult.IsSuccess).IsTrue();
        await Assert.That(post.Content).IsEqualTo("Updated content");
        await Assert.That(post.RevisionCount).IsEqualTo(1);
        await Assert.That(post.EditedAt).IsNotNull();

        await _postRepository.Received(1).UpdateAsync(post);
        await _postRepository.Received(1).AddRevisionAsync(Arg.Any<PostRevision>());

        // Step 3: Delete post (hard delete - within 5 minutes)
        var deleteResult = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert deletion
        await Assert.That(deleteResult.IsSuccess).IsTrue();
        await _postRepository.Received(1).DeleteAsync(post); // Hard delete
        await _counterService.Received(1).DecrementPostCountAsync(discussionId);
        await _realtimeNotifier.Received(1).NotifyPostDeletedAsync(post.PublicId, discussionId, true);
    }

    [Test]
    public async Task PostLifecycle_CreateEditMultipleTimes_MaintainsRevisionHistory()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId)
            .Returns(user);

        // Step 1: Create
        var createResult = await _useCase.CreatePostAsync(discussionId, userId, "Version 1");
        var post = createResult.Value!;

        _postRepository.GetByPublicIdAsync(post.PublicId)
            .Returns(post);

        // Step 2: Edit multiple times
        await _useCase.UpdatePostAsync(post.PublicId, userId, "Version 2");
        await _useCase.UpdatePostAsync(post.PublicId, userId, "Version 3");
        await _useCase.UpdatePostAsync(post.PublicId, userId, "Version 4");

        // Assert
        await Assert.That(post.Content).IsEqualTo("Version 4");
        await Assert.That(post.RevisionCount).IsEqualTo(3);
        await Assert.That(post.Revisions).Count().IsEqualTo(3);
        await Assert.That(post.Revisions.ElementAt(0).Content).IsEqualTo("Version 1");
        await Assert.That(post.Revisions.ElementAt(1).Content).IsEqualTo("Version 2");
        await Assert.That(post.Revisions.ElementAt(2).Content).IsEqualTo("Version 3");

        // Only new revisions are saved (not all revisions on every update)
        // First edit: 1 revision saved, Second edit: 1 revision saved, Third edit: 1 revision saved = 3 total calls
        await _postRepository.Received(3).AddRevisionAsync(Arg.Any<PostRevision>());
    }

    [Test]
    public async Task PostLifecycle_CreateOldPostThenDelete_PerformsSoftDelete()
    {
        // Arrange - Create post that's already > 5 minutes old
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var oldPost = Post.Rehydrate(PostId.New(), discussionId, userId, "Old content", "<p>Old content</p>", sixMinutesAgo);

        _postRepository.GetByPublicIdAsync(oldPost.PublicId)
            .Returns(oldPost);

        // Act - Delete old post
        var deleteResult = await _useCase.DeletePostAsync(oldPost.PublicId, userId);

        // Assert - Should be soft delete
        await Assert.That(deleteResult.IsSuccess).IsTrue();
        await Assert.That(oldPost.IsDeleted).IsTrue();
        await _postRepository.Received(1).UpdateAsync(oldPost); // Soft delete
        await _postRepository.DidNotReceive().DeleteAsync(Arg.Any<Post>()); // Not hard delete
        await _realtimeNotifier.Received(1).NotifyPostDeletedAsync(oldPost.PublicId, discussionId, false);
    }

    [Test]
    public async Task PostLifecycle_CreateReplyChain_MaintainsReplyReferences()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId)
            .Returns(user);

        // Step 1: Create original post
        var originalResult = await _useCase.CreatePostAsync(discussionId, userId, "Original post");
        var originalPost = originalResult.Value!;

        // Step 2: Create first reply
        _postRepository.GetByPublicIdAsync(originalPost.PublicId)
            .Returns(originalPost);

        var reply1Result = await _useCase.CreatePostAsync(discussionId, userId, "Reply 1", originalPost.PublicId);
        var reply1 = reply1Result.Value!;

        // Step 3: Create reply to reply
        _postRepository.GetByPublicIdAsync(reply1.PublicId)
            .Returns(reply1);

        var reply2Result = await _useCase.CreatePostAsync(discussionId, userId, "Reply 2", reply1.PublicId);
        var reply2 = reply2Result.Value!;

        // Assert reply chain
        await Assert.That((object?)originalPost.ReplyToPostId).IsNull();
        await Assert.That(reply1.ReplyToPostId!.Value).IsEqualTo(originalPost.PublicId.Value);
        await Assert.That(reply2.ReplyToPostId!.Value).IsEqualTo(reply1.PublicId.Value);

        await _postRepository.Received(3).AddAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task PostLifecycle_CreateInLockedDiscussion_Fails()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");
        discussion.Lock(); // Lock the discussion

        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);

        // Act
        var createResult = await _useCase.CreatePostAsync(discussionId, userId, "Blocked content");

        // Assert
        await Assert.That(createResult.IsSuccess).IsFalse();
        await Assert.That(createResult.Error).Contains("locked");
        await _postRepository.DidNotReceive().AddAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task PostLifecycle_EditDeletedPost_Fails()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Content", "<p>Content</p>", sixMinutesAgo);
        post.SoftDelete(userId); // Delete the post

        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), userId, "Test Discussion", "test-discussion");

        _postRepository.GetByPublicIdAsync(post.PublicId)
            .Returns(post);
        _userRepository.GetByPublicIdAsync(userId)
            .Returns(user);
        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);

        // Act
        var updateResult = await _useCase.UpdatePostAsync(post.PublicId, userId, "New content");

        // Assert
        await Assert.That(updateResult.IsSuccess).IsFalse();
        await Assert.That(updateResult.Error).Contains("deleted");
        await _postRepository.DidNotReceive().UpdateAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task PostLifecycle_NonAuthorEdit_Fails()
    {
        // Arrange
        var discussionId = DiscussionId.New();
        var authorId = UserId.New();
        var otherUserId = UserId.New();

        var post = Post.Create(discussionId, authorId, "Original content", "<p>Original content</p>");
        var otherUser = User.CreateWithEmail("OtherUser", "other@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), authorId, "Test Discussion", "test-discussion");

        _postRepository.GetByPublicIdAsync(post.PublicId)
            .Returns(post);
        _userRepository.GetByPublicIdAsync(otherUserId)
            .Returns(otherUser);
        _discussionRepository.GetByPublicIdAsync(discussionId)
            .Returns(discussion);

        // Act
        var updateResult = await _useCase.UpdatePostAsync(post.PublicId, otherUserId, "Unauthorized edit");

        // Assert
        await Assert.That(updateResult.IsSuccess).IsFalse();
        await _postRepository.DidNotReceive().UpdateAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task PostLifecycle_GetPostHistory_ReturnsAllRevisions()
    {
        // Arrange
        var postId = PostId.New();
        var revisions = new List<PostRevision>
        {
            PostRevision.Create(postId, "Version 1", UserId.New(), 1),
            PostRevision.Create(postId, "Version 2", UserId.New(), 2),
            PostRevision.Create(postId, "Version 3", UserId.New(), 3)
        };

        _postRepository.GetRevisionsAsync(postId)
            .Returns(revisions);

        // Act
        var history = await _useCase.GetPostHistoryAsync(postId);

        // Assert
        await Assert.That(history).Count().IsEqualTo(3);
    }

    [Test]
    public async Task PostLifecycle_CreateEditWaitSixMinutesDelete_PerformsSoftDelete()
    {
        // This test simulates the real-world scenario where a post is created,
        // edited, and then deleted after the 5-minute window has passed

        // Arrange
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Create post 6 minutes ago
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Original", "<p>Original</p>", sixMinutesAgo);

        // Edit it (still old)
        post.UpdateContent("Edited", "<p>Edited</p>", userId);

        _postRepository.GetByPublicIdAsync(post.PublicId)
            .Returns(post);

        // Act - Try to delete
        var deleteResult = await _useCase.DeletePostAsync(post.PublicId, userId);

        // Assert - Should be soft delete because post is > 5 minutes old
        await Assert.That(deleteResult.IsSuccess).IsTrue();
        await _postRepository.Received(1).UpdateAsync(post); // Soft delete
        await _postRepository.DidNotReceive().DeleteAsync(Arg.Any<Post>());
        await Assert.That(post.IsDeleted).IsTrue();
    }
}
