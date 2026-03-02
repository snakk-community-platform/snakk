using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Infrastructure.Tests.Adapters;

public class PostRepositoryAdapterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly PostRepositoryAdapter _adapter;

    public PostRepositoryAdapterTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var databaseRepo = new PostRepository(_db.Context);
        _adapter = new PostRepositoryAdapter(databaseRepo, _db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingPost_ReturnsMappedDomainEntity()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _adapter.GetByPublicIdAsync(PostId.From(post.PublicId));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId.Value).IsEqualTo(post.PublicId);
        await Assert.That(result.Content).IsEqualTo(post.Content);
        await Assert.That(result.DiscussionId.Value).IsEqualTo(discussion.PublicId);
        await Assert.That(result.CreatedByUserId.Value).IsEqualTo(user.PublicId);
        await Assert.That(result.IsFirstPost).IsTrue();
    }

    #endregion

    #region GetByDiscussionIdAsync Tests

    [Test]
    public async Task GetByDiscussionIdAsync_ExistingDiscussion_ResolvesPublicIdAndReturnsPosts()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        // Create additional posts in the same discussion
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Second post");
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Third post");

        var result = (await _adapter.GetByDiscussionIdAsync(DiscussionId.From(discussion.PublicId))).ToList();

        await Assert.That(result).HasCount().EqualTo(3);
    }

    [Test]
    public async Task GetByDiscussionIdAsync_NonExistentDiscussion_ReturnsEmpty()
    {
        var result = (await _adapter.GetByDiscussionIdAsync(DiscussionId.From("nonexistent_discussion_id"))).ToList();

        await Assert.That(result).HasCount().EqualTo(0);
    }

    #endregion

    #region GetPagedByDiscussionIdAsync Tests

    [Test]
    public async Task GetPagedByDiscussionIdAsync_MultiplePosts_ReturnsPagedResults()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        // The hierarchy already created 1 post (isFirstPost=true)
        // Create 4 more posts
        for (var i = 2; i <= 5; i++)
        {
            await _builder.CreatePostAsync(discussion.Id, user.Id, $"Post content {i}");
        }

        // Request page of 3
        var result = await _adapter.GetPagedByDiscussionIdAsync(DiscussionId.From(discussion.PublicId), 0, 3);

        await Assert.That(result.Items.Count()).IsEqualTo(3);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.PageSize).IsEqualTo(3);
    }

    #endregion

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_WithReplyToPost_ResolvesForeignKeysIncludingOptionalReplyToPostId()
    {
        var (user, _, _, _, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();

        var domainPost = Post.Create(
            DiscussionId.From(discussion.PublicId),
            UserId.From(user.PublicId),
            "This is a reply",
            isFirstPost: false,
            replyToPostId: PostId.From(firstPost.PublicId));

        await _adapter.AddAsync(domainPost);

        // Verify persisted using a separate context
        var verifyContext = _db.CreateSeparateContext();
        var persisted = verifyContext.Posts.FirstOrDefault(p => p.PublicId == domainPost.PublicId.Value);

        await Assert.That(persisted).IsNotNull();
        await Assert.That(persisted!.Content).IsEqualTo("This is a reply");
        await Assert.That(persisted.DiscussionId).IsEqualTo(discussion.Id);
        await Assert.That(persisted.CreatedByUserId).IsEqualTo(user.Id);
        await Assert.That(persisted.ReplyToPostId).IsEqualTo(firstPost.Id);
    }

    #endregion

    #region GetFirstPostByDiscussionIdAsync Tests

    [Test]
    public async Task GetFirstPostByDiscussionIdAsync_ExistingDiscussion_ReturnsFirstPost()
    {
        var (user, _, _, _, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();
        // Add a non-first post
        await _builder.CreatePostAsync(discussion.Id, user.Id, "Not the first post", isFirstPost: false);

        var result = await _adapter.GetFirstPostByDiscussionIdAsync(DiscussionId.From(discussion.PublicId));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.IsFirstPost).IsTrue();
        await Assert.That(result.PublicId.Value).IsEqualTo(firstPost.PublicId);
    }

    #endregion

    #region GetPostNumberInDiscussionAsync Tests

    [Test]
    public async Task GetPostNumberInDiscussionAsync_MultiplePosts_CountsCorrectly()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();
        // Add more posts - the hierarchy already has 1 first post
        var post2 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Second post");
        var post3 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Third post");

        // Count posts created at or before the third post's time
        var result = await _adapter.GetPostNumberInDiscussionAsync(
            DiscussionId.From(discussion.PublicId),
            post3.CreatedAt);

        // All 3 posts should be counted (created at or before post3's time)
        await Assert.That(result).IsEqualTo(3);
    }

    #endregion

    #region GetByDiscussionIdAsync returns empty for nonexistent

    [Test]
    public async Task GetPagedByDiscussionIdAsync_NonExistentDiscussion_ReturnsEmptyPagedResult()
    {
        var result = await _adapter.GetPagedByDiscussionIdAsync(
            DiscussionId.From("nonexistent_discussion_id"), 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion
}
