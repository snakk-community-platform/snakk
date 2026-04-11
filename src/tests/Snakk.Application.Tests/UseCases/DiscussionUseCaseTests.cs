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

public class DiscussionUseCaseTests
{
    private readonly IDiscussionRepository _discussionRepository = Substitute.For<IDiscussionRepository>();
    private readonly ISpaceRepository _spaceRepository = Substitute.For<ISpaceRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly IDomainEventDispatcher _eventDispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly ICounterService _counterService = Substitute.For<ICounterService>();
    private readonly IMarkupParser _markupParser = Substitute.For<IMarkupParser>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly IMediaService _mediaService = Substitute.For<IMediaService>();
    private readonly IModerationRepository _moderationRepository = Substitute.For<IModerationRepository>();
    private DiscussionUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _markupParser.ToHtml(Arg.Any<string>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");
        _markupParser.ToHtml(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(x => $"<p>{x.Arg<string>()}</p>");

        _realtimeNotifier
            .NotifyDiscussionCreatedAsync(Arg.Any<DiscussionId>(), Arg.Any<SpaceId>(), Arg.Any<User>())
            .Returns(Task.CompletedTask);

        _realtimeNotifier
            .NotifyDiscussionLockedAsync(Arg.Any<DiscussionId>())
            .Returns(Task.CompletedTask);

        _realtimeNotifier
            .NotifyDiscussionUnlockedAsync(Arg.Any<DiscussionId>())
            .Returns(Task.CompletedTask);

        _realtimeNotifier
            .NotifyDiscussionPinnedAsync(Arg.Any<DiscussionId>(), Arg.Any<SpaceId>(), Arg.Any<bool>())
            .Returns(Task.CompletedTask);

        _realtimeNotifier
            .NotifyDiscussionTitleUpdatedAsync(Arg.Any<DiscussionId>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        _useCase = new DiscussionUseCase(
            _discussionRepository, _spaceRepository, _userRepository, _postRepository,
            _eventDispatcher, _counterService, _markupParser, _realtimeNotifier,
            _mediaService, _moderationRepository);
    }

    #region CreateDiscussionAsync Tests

    [Test]
    public async Task CreateDiscussionAsync_WithValidParameters_CreatesDiscussion()
    {
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        const string title = "Test Discussion";
        const string slug = "test-discussion";
        const string firstPostContent = "This is the first post content.";
        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");

        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, title, slug, firstPostContent);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Title).IsEqualTo(title);
        await Assert.That(result.Value.Slug).IsEqualTo(slug);
        await Assert.That(result.Value.SpaceId).IsEqualTo(spaceId);
        await Assert.That(result.Value.CreatedByUserId).IsEqualTo(userId);

        await _discussionRepository.Received(1).AddAsync(Arg.Any<Discussion>());
        await _postRepository.Received(1).AddAsync(Arg.Any<Post>());
        await _counterService.Received(1).IncrementDiscussionCountAsync(spaceId);
        await _counterService.Received(1).IncrementPostCountAsync(Arg.Any<DiscussionId>());
        await _eventDispatcher.Received(2).DispatchAsync(Arg.Any<IEnumerable<IDomainEvent>>());
    }

    [Test]
    public async Task CreateDiscussionAsync_WithNonExistentSpace_ReturnsFailure()
    {
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns((Space?)null);

        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, "Title", "slug", "content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Space");
        await Assert.That(result.Error).Contains("not found");
        await _discussionRepository.DidNotReceive().AddAsync(Arg.Any<Discussion>());
    }

    [Test]
    public async Task CreateDiscussionAsync_WithNonExistentUser_ReturnsFailure()
    {
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);
        _userRepository.GetByPublicIdAsync(userId).Returns((User?)null);

        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, "Title", "slug", "content");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("User");
        await Assert.That(result.Error).Contains("not found");
        await _discussionRepository.DidNotReceive().AddAsync(Arg.Any<Discussion>());
    }

    [Test]
    public async Task CreateDiscussionAsync_ClearsDomainEventsAfterDispatching()
    {
        var spaceId = SpaceId.New();
        var userId = UserId.New();
        var space = Space.Create(HubId.New(), "Test Space", "test-space");
        var user = User.CreateWithEmail("TestUser", "test@example.com", "hash", "token");
        _spaceRepository.GetByPublicIdAsync(spaceId).Returns(space);
        _userRepository.GetByPublicIdAsync(userId).Returns(user);

        var result = await _useCase.CreateDiscussionAsync(spaceId, userId, "Title", "slug", "content");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DomainEvents).Count().IsEqualTo(0);
    }

    #endregion

    #region GetDiscussionAsync Tests

    [Test]
    public async Task GetDiscussionAsync_WithExistingDiscussion_ReturnsDiscussion()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var discussionId = discussion.PublicId;
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.GetDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(discussion);
    }

    [Test]
    public async Task GetDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.GetDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetDiscussionsBySpaceAsync Tests

    [Test]
    public async Task GetDiscussionsBySpaceAsync_ReturnsPagedResults()
    {
        var spaceId = SpaceId.New();
        var discussions = new List<Discussion>
        {
            Discussion.Create(spaceId, UserId.New(), "Discussion 1", "discussion-1"),
            Discussion.Create(spaceId, UserId.New(), "Discussion 2", "discussion-2")
        };
        var pagedResult = new PagedResult<Discussion> { Items = discussions, Offset = 0, PageSize = 20, HasMoreItems = false };
        _discussionRepository.GetBySpaceIdAsync(spaceId, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetDiscussionsBySpaceAsync(spaceId, 0, 20);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(2);
    }

    #endregion

    #region UpdateDiscussionTitleAsync Tests

    [Test]
    public async Task UpdateDiscussionTitleAsync_WithValidParameters_UpdatesTitle()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Old Title", "old-title");
        var discussionId = discussion.PublicId;
        const string newTitle = "New Title";
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UpdateDiscussionTitleAsync(discussionId, newTitle);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Title).IsEqualTo(newTitle);
        await _discussionRepository.Received(1).UpdateAsync(discussion);
    }

    [Test]
    public async Task UpdateDiscussionTitleAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.UpdateDiscussionTitleAsync(discussionId, "New Title");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    [Test]
    public async Task UpdateDiscussionTitleAsync_WithLockedDiscussion_ReturnsFailure()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Old Title", "old-title");
        discussion.Lock();
        var discussionId = discussion.PublicId;
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UpdateDiscussionTitleAsync(discussionId, "New Title");

        await Assert.That(result.IsSuccess).IsFalse();
        await _discussionRepository.DidNotReceive().UpdateAsync(Arg.Any<Discussion>());
    }

    #endregion

    #region PinDiscussionAsync Tests

    [Test]
    public async Task PinDiscussionAsync_WithExistingDiscussion_PinsDiscussion()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var discussionId = discussion.PublicId;
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.PinDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsPinned).IsTrue();
        await _discussionRepository.Received(1).UpdateAsync(discussion);
    }

    [Test]
    public async Task PinDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.PinDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UnpinDiscussionAsync Tests

    [Test]
    public async Task UnpinDiscussionAsync_WithPinnedDiscussion_UnpinsDiscussion()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Pin();
        var discussionId = discussion.PublicId;
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UnpinDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsPinned).IsFalse();
        await _discussionRepository.Received(1).UpdateAsync(discussion);
    }

    [Test]
    public async Task UnpinDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.UnpinDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region LockDiscussionAsync Tests

    [Test]
    public async Task LockDiscussionAsync_WithExistingDiscussion_LocksDiscussion()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        var discussionId = discussion.PublicId;
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.LockDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsLocked).IsTrue();
        await _discussionRepository.Received(1).UpdateAsync(discussion);
    }

    [Test]
    public async Task LockDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.LockDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region UnlockDiscussionAsync Tests

    [Test]
    public async Task UnlockDiscussionAsync_WithLockedDiscussion_UnlocksDiscussion()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test Discussion", "test-discussion");
        discussion.Lock();
        var discussionId = discussion.PublicId;
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns(discussion);

        var result = await _useCase.UnlockDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(discussion.IsLocked).IsFalse();
        await _discussionRepository.Received(1).UpdateAsync(discussion);
    }

    [Test]
    public async Task UnlockDiscussionAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.UnlockDiscussionAsync(discussionId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    #endregion

    #region GetPostNumberAsync Tests

    [Test]
    public async Task GetPostNumberAsync_WithValidParameters_ReturnsPostNumber()
    {
        var realDiscussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        var realPost = Post.Create(realDiscussion.PublicId, UserId.New(), "content", "<p>content</p>");

        _discussionRepository.GetByPublicIdAsync(realDiscussion.PublicId).Returns(realDiscussion);
        _postRepository.GetByPublicIdAsync(realPost.PublicId).Returns(realPost);
        _postRepository.GetPostNumberInDiscussionAsync(realDiscussion.PublicId, realPost.CreatedAt).Returns(5);

        var result = await _useCase.GetPostNumberAsync(realDiscussion.PublicId, realPost.PublicId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(5);
    }

    [Test]
    public async Task GetPostNumberAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        var postId = PostId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.GetPostNumberAsync(discussionId, postId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
    }

    [Test]
    public async Task GetPostNumberAsync_WithNonExistentPost_ReturnsFailure()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        var postId = PostId.New();
        _discussionRepository.GetByPublicIdAsync(discussion.PublicId).Returns(discussion);
        _postRepository.GetByPublicIdAsync(postId).Returns((Post?)null);

        var result = await _useCase.GetPostNumberAsync(discussion.PublicId, postId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post not found");
    }

    [Test]
    public async Task GetPostNumberAsync_WithPostInDifferentDiscussion_ReturnsFailure()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Discussion 1", "discussion-1");
        var otherDiscussion = Discussion.Create(SpaceId.New(), UserId.New(), "Discussion 2", "discussion-2");
        var post = Post.Create(otherDiscussion.PublicId, UserId.New(), "Post in other discussion", "<p>Post in other discussion</p>");

        _discussionRepository.GetByPublicIdAsync(discussion.PublicId).Returns(discussion);
        _postRepository.GetByPublicIdAsync(post.PublicId).Returns(post);

        var result = await _useCase.GetPostNumberAsync(discussion.PublicId, post.PublicId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Post does not belong to this discussion");
    }

    #endregion

    #region GetFirstPostPreviewAsync Tests

    [Test]
    public async Task GetFirstPostPreviewAsync_WithExistingFirstPost_ReturnsContent()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        var firstPost = Post.Create(discussion.PublicId, UserId.New(), "This is the first post content", "<p>This is the first post content</p>", isFirstPost: true);
        _discussionRepository.GetByPublicIdAsync(discussion.PublicId).Returns(discussion);
        _postRepository.GetFirstPostByDiscussionIdAsync(discussion.PublicId).Returns(firstPost);

        var result = await _useCase.GetFirstPostPreviewAsync(discussion.PublicId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo("This is the first post content");
    }

    [Test]
    public async Task GetFirstPostPreviewAsync_WithNonExistentDiscussion_ReturnsFailure()
    {
        var discussionId = DiscussionId.New();
        _discussionRepository.GetByPublicIdAsync(discussionId).Returns((Discussion?)null);

        var result = await _useCase.GetFirstPostPreviewAsync(discussionId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Discussion not found");
    }

    [Test]
    public async Task GetFirstPostPreviewAsync_WithNoFirstPost_ReturnsFailure()
    {
        var discussion = Discussion.Create(SpaceId.New(), UserId.New(), "Test", "test");
        _discussionRepository.GetByPublicIdAsync(discussion.PublicId).Returns(discussion);
        _postRepository.GetFirstPostByDiscussionIdAsync(discussion.PublicId).Returns((Post?)null);

        var result = await _useCase.GetFirstPostPreviewAsync(discussion.PublicId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("First post not found");
    }

    #endregion
}
