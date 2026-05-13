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

namespace Snakk.Application.Tests.UseCases;

public class PostUseCaseTests
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
            Substitute.For<IDiscussionRepository>(),
            Substitute.For<IRealtimeNotifier>(),
            Substitute.For<ICounterService>());

        _markupParser.ToHtml(Arg.Any<string>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");
        _markupParser.ToHtml(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");

        _useCase = new PostUseCase(
            _postRepository, _discussionRepository, _spaceRepository, _userRepository, _followRepository,
            _eventDispatcher, _realtimeNotifier, _counterService, _mediaService,
            _markupParser, new ContentNormalizer(), _moderationRepository, _reactionUseCase);
    }

    #region CreatePostAsync Tests

    [Test]
    public async Task CreatePostAsync_WithValidParameters_CreatesPost()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        const string content = "Test post content";
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.CreatePostAsync(discussionId, userId, content);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Content).IsEqualTo(content);
        await Assert.That(result.Value.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(result.Value.CreatedByUserId).IsEqualTo(userId);

        await _postRepository.Received(1).AddAsync(Arg.Any<Post>());
        await _discussionRepository.Received(1).UpdateAsync(discussion);
        await _counterService.Received(1).IncrementPostCountAsync(discussionId);
        await _eventDispatcher.Received(1).DispatchAsync(Arg.Any<IEnumerable<IDomainEvent>>());
        await _realtimeNotifier.Received(1).NotifyPostCreatedAsync(Arg.Any<Post>(), user, discussion);
    }

    [Test]
    public async Task CreatePostAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.CreatePostAsync(discussionId, userId, "content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion");
        await Assert.That(result.Error).Contains("not found");
        await _postRepository.DidNotReceive().AddAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task CreatePostAsync_WithLockedDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.CreatePostAsync(discussionId, userId, "content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("locked");
        await _postRepository.DidNotReceive().AddAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task CreatePostAsync_WithNonExistentUser_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId).Returns((User?)null);

        var result = await _useCase.CreatePostAsync(discussionId, userId, "content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User");
        await Assert.That(result.Error).Contains("not found");
        await _postRepository.DidNotReceive().AddAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task CreatePostAsync_WithReplyToPost_ValidatesReplyToPostExists()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);
        _postRepository.GetByPublicIdAsync(replyToPostId).Returns((Post?)null);

        var result = await _useCase.CreatePostAsync(discussionId, userId, "content", replyToPostId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Reply-to post");
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task CreatePostAsync_WithValidReplyToPost_CreatesReply()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var replyToPostId = PostId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var replyToPost = Post.Create(discussionId, UserId.New(), "Original post", "<p>Original post</p>");

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);
        _postRepository.GetByPublicIdAsync(replyToPostId).Returns(replyToPost);

        var result = await _useCase.CreatePostAsync(discussionId, userId, "Reply content", replyToPostId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.ReplyToPostId!).IsEqualTo(replyToPostId);
    }

    #endregion

    #region UpdatePostAsync Tests

    [Test]
    public async Task UpdatePostAsync_WithValidParameters_UpdatesPost()
    {
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "Original content", "<p>Original content</p>");
        const string newContent = "Updated content";
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UpdatePostAsync(post.PublicId, userId, newContent);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Content).IsEqualTo(newContent);

        await _postRepository.Received(1).UpdateAsync(post);
        await _postRepository.Received(1).AddRevisionAsync(Arg.Any<PostRevision>());
        await _eventDispatcher.Received(1).DispatchAsync(Arg.Any<IEnumerable<IDomainEvent>>());
        await _realtimeNotifier.Received(1).NotifyPostEditedAsync(post, user, discussion);
    }

    [Test]
    public async Task UpdatePostAsync_WithNonExistentPost_ReturnsFailure()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        _postRepository.GetByPublicIdAsync(postId).Returns((Post?)null);

        var result = await _useCase.UpdatePostAsync(postId, userId, "new content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post");
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task UpdatePostAsync_ByNonAuthor_ReturnsFailure()
    {
        var authorId = UserId.New();
        var differentUserId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, authorId, "Original content", "<p>Original content</p>");
        var user = User.CreateWithEmail("DifferentUser", "different@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);
        _userRepository.GetByPublicIdAsync(differentUserId).Returns(user);
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UpdatePostAsync(post.PublicId, differentUserId, "new content");

        await Assert.That(result.IsSuccess).IsFalse();
        await _postRepository.DidNotReceive().UpdateAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task UpdatePostAsync_OnDeletedPost_ReturnsFailure()
    {
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "Original content", "<p>Original content</p>");
        post.SoftDelete(userId);
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");

        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UpdatePostAsync(post.PublicId, userId, "new content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("deleted");
    }

    #endregion

    #region DeletePostAsync Tests

    [Test]
    public async Task DeletePostAsync_WithinFiveMinutes_PerformsHardDelete()
    {
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, userId, "Test content", "<p>Test content</p>");
        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);

        var result = await _useCase.DeletePostAsync(post.PublicId, userId);

        await Assert.That(result.IsSuccess).IsTrue();
        await _postRepository.Received(1).DeleteAsync(post);
        await _postRepository.DidNotReceive().UpdateAsync(Arg.Any<Post>());
        await _counterService.Received(1).DecrementPostCountAsync(discussionId);
        await _realtimeNotifier.Received(1).NotifyPostDeletedAsync(post.PublicId, discussionId, true);
    }

    [Test]
    public async Task DeletePostAsync_AfterFiveMinutes_PerformsSoftDelete()
    {
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var sixMinutesAgo = DateTime.UtcNow.AddMinutes(-6);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Test content", "<p>Test content</p>", sixMinutesAgo);
        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);

        var result = await _useCase.DeletePostAsync(post.PublicId, userId);

        await Assert.That(result.IsSuccess).IsTrue();
        await _postRepository.Received(1).UpdateAsync(post);
        await _postRepository.DidNotReceive().DeleteAsync(Arg.Any<Post>());
        await _counterService.Received(1).DecrementPostCountAsync(discussionId);
        await _realtimeNotifier.Received(1).NotifyPostDeletedAsync(post.PublicId, discussionId, false);
    }

    [Test]
    public async Task DeletePostAsync_ByNonAuthor_ReturnsFailure()
    {
        var authorId = UserId.New();
        var differentUserId = UserId.New();
        var discussionId = DiscussionId.New();
        var post = Post.Create(discussionId, authorId, "Test content", "<p>Test content</p>");
        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);

        var result = await _useCase.DeletePostAsync(post.PublicId, differentUserId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("only delete your own posts");
        await _postRepository.DidNotReceive().DeleteAsync(Arg.Any<Post>());
        await _postRepository.DidNotReceive().UpdateAsync(Arg.Any<Post>());
    }

    [Test]
    public async Task DeletePostAsync_WithNonExistentPost_ReturnsFailure()
    {
        var postId = PostId.New();
        var userId = UserId.New();
        _postRepository.GetByPublicIdAsync(postId).Returns((Post?)null);

        var result = await _useCase.DeletePostAsync(postId, userId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post");
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPostAsync Tests

    [Test]
    public async Task GetPostAsync_WithExistingPost_ReturnsPost()
    {
        var post = Post.Create(DiscussionId.New(), UserId.New(), "Test content", "<p>Test content</p>");
        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);

        var result = await _useCase.GetPostAsync(post.PublicId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(post);
    }

    [Test]
    public async Task GetPostAsync_WithNonExistentPost_ReturnsFailure()
    {
        var postId = PostId.New();
        _postRepository.GetByPublicIdAsync(postId).Returns((Post?)null);

        var result = await _useCase.GetPostAsync(postId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post");
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPostsByDiscussionAsync Tests

    [Test]
    public async Task GetPostsByDiscussionAsync_ReturnsPagedResults()
    {
        var discussionId = DiscussionId.New();
        var posts = new List<Post>
        {
            Post.Create(discussionId, UserId.New(), "Post 1", "<p>Post 1</p>"),
            Post.Create(discussionId, UserId.New(), "Post 2", "<p>Post 2</p>")
        };
        var pagedResult = new PagedResult<Post> { Items = posts, Offset = 0, PageSize = 20, HasMoreItems = false };
        _postRepository.GetPagedByDiscussionIdAsync(discussionId, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetPostsByDiscussionAsync(discussionId, 0, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(20);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetPostHistoryAsync Tests

    [Test]
    public async Task GetPostHistoryAsync_ReturnsRevisions()
    {
        var postId = PostId.New();
        var revisions = new List<PostRevision>
        {
            PostRevision.Create(postId, "Old content 1", UserId.New(), 1),
            PostRevision.Create(postId, "Old content 2", UserId.New(), 2)
        };
        _postRepository.GetRevisionsAsync(postId).Returns(revisions);

        var result = await _useCase.GetPostHistoryAsync(postId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(2);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task CreatePostAsync_UpdatesDiscussionActivity()
    {
        var discussionId = DiscussionId.New();
        var userId = UserId.New();
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        var originalActivity = discussion.LastActivityAt;

        Thread.Sleep(10);

        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        await _useCase.CreatePostAsync(discussionId, userId, "content");

        await Assert.That(discussion.LastActivityAt!.Value).IsGreaterThan(originalActivity ?? DateTime.MinValue);
    }

    [Test]
    public async Task DeletePostAsync_ExactlyFiveMinutesAgo_PerformsSoftDelete()
    {
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var post = Post.Rehydrate(PostId.New(), discussionId, userId, "Test content", "<p>Test content</p>", fiveMinutesAgo);
        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);

        var result = await _useCase.DeletePostAsync(post.PublicId, userId);

        await Assert.That(result.IsSuccess).IsTrue();
        await _postRepository.Received(1).UpdateAsync(post);
        await _postRepository.DidNotReceive().DeleteAsync(Arg.Any<Post>());
    }

    #endregion
}
