using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Models;

namespace Snakk.Infrastructure.Tests.Repositories;

public class PostRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly PostRepository _repository;

    public PostRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new PostRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingPost_ReturnsEntityWithDiscussionAndUser()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByIdAsync(post.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(post.Id);
        await Assert.That(result.PublicId).IsEqualTo(post.PublicId);
        await Assert.That(result.Discussion).IsNotNull();
        await Assert.That(result.Discussion.Id).IsEqualTo(discussion.Id);
        await Assert.That(result.CreatedByUser).IsNotNull();
        await Assert.That(result.CreatedByUser.Id).IsEqualTo(user.Id);
    }

    [Test]
    public async Task GetByIdAsync_PostWithReplyToPost_ReturnsWithReplyLoaded()
    {
        var (user, _, _, _, discussion, originalPost) = await _builder.CreateFullHierarchyAsync();

        var replyPost = new PostDatabaseEntity
        {
            PublicId = $"post_reply_{Guid.NewGuid():N}",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            Content = "Reply content",
            CreatedAt = DateTime.UtcNow,
            IsFirstPost = false,
            ReplyToPostId = originalPost.Id
        };
        _db.Context.Posts.Add(replyPost);
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(replyPost.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ReplyToPost).IsNotNull();
        await Assert.That(result.ReplyToPost!.Id).IsEqualTo(originalPost.Id);
        await Assert.That(result.ReplyToPostId).IsEqualTo(originalPost.Id);
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetForUpdateAsync Tests

    [Test]
    public async Task GetForUpdateAsync_ExistingPost_ReturnsEntity()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForUpdateAsync(post.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(post.PublicId);
        await Assert.That(result.Discussion).IsNotNull();
        await Assert.That(result.CreatedByUser).IsNotNull();
    }

    [Test]
    public async Task GetForUpdateAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetForUpdateAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_MultiplePosts_ReturnsAllWithIncludes()
    {
        var (user, _, _, _, discussion, post1) = await _builder.CreateFullHierarchyAsync();
        var post2 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Second post");
        var post3 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Third post");

        var result = (await _repository.GetAllAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.All(p => p.Discussion is not null)).IsTrue();
        await Assert.That(result.All(p => p.CreatedByUser is not null)).IsTrue();
    }

    #endregion

    #region GetForDisplayAsync Tests

    [Test]
    public async Task GetForDisplayAsync_ExistingPost_ReturnsDtoWithCorrectFields()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForDisplayAsync(post.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(post.PublicId);
        await Assert.That(result.Content).IsEqualTo(post.Content);
        await Assert.That(result.CreatedAt).IsEqualTo(post.CreatedAt);
        await Assert.That(result.IsFirstPost).IsEqualTo(post.IsFirstPost);
        await Assert.That(result.DiscussionPublicId).IsEqualTo(discussion.PublicId);
        await Assert.That(result.DiscussionTitle).IsEqualTo(discussion.Title);
        await Assert.That(result.CreatedByUserPublicId).IsEqualTo(user.PublicId);
        await Assert.That(result.CreatedByUserDisplayName).IsEqualTo(user.DisplayName);
    }

    [Test]
    public async Task GetForDisplayAsync_PostWithReply_ReplyToPostPublicIdIsPopulated()
    {
        var (user, _, _, _, discussion, originalPost) = await _builder.CreateFullHierarchyAsync();

        var replyPost = new PostDatabaseEntity
        {
            PublicId = $"post_reply_{Guid.NewGuid():N}",
            DiscussionId = discussion.Id,
            CreatedByUserId = user.Id,
            Content = "Reply content",
            CreatedAt = DateTime.UtcNow,
            IsFirstPost = false,
            ReplyToPostId = originalPost.Id
        };
        _db.Context.Posts.Add(replyPost);
        await _db.Context.SaveChangesAsync();

        var result = await _repository.GetForDisplayAsync(replyPost.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ReplyToPostPublicId).IsNotNull();
        await Assert.That(result.ReplyToPostPublicId).IsEqualTo(originalPost.PublicId);
    }

    [Test]
    public async Task GetForDisplayAsync_PostWithoutReply_ReplyToPostPublicIdIsNull()
    {
        var (_, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetForDisplayAsync(post.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ReplyToPostPublicId).IsNull();
    }

    [Test]
    public async Task GetForDisplayAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetForDisplayAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingPost_ReturnsEntity()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByPublicIdAsync(post.PublicId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo(post.PublicId);
        await Assert.That(result.Discussion).IsNotNull();
        await Assert.That(result.CreatedByUser).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent_public_id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetPagedByDiscussionIdAsync Tests

    [Test]
    public async Task GetPagedByDiscussionIdAsync_PaginationWithHasMoreItems()
    {
        var (user, _, _, _, discussion, _) = await _builder.CreateFullHierarchyAsync();

        // Create additional posts to exceed page size
        for (var i = 0; i < 3; i++)
        {
            var extraPost = new PostDatabaseEntity
            {
                PublicId = $"post_extra_{i}_{Guid.NewGuid():N}",
                DiscussionId = discussion.Id,
                CreatedByUserId = user.Id,
                Content = $"Extra post {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i + 1),
                IsFirstPost = false
            };
            _db.Context.Posts.Add(extraPost);
        }
        await _db.Context.SaveChangesAsync();

        // 4 total posts, request page size of 2
        var result = await _repository.GetPagedByDiscussionIdAsync(discussion.Id, 0, 2);

        await Assert.That(result.Items.Count()).IsEqualTo(2);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(2);
    }

    [Test]
    public async Task GetPagedByDiscussionIdAsync_EmptyDiscussion_ReturnsEmptyResult()
    {
        var user = await _builder.CreateUserAsync();
        var community = await _builder.CreateCommunityAsync();
        var hub = await _builder.CreateHubAsync(community.Id);
        var space = await _builder.CreateSpaceAsync(hub.Id);
        var discussion = await _builder.CreateDiscussionAsync(space.Id, user.Id);

        var result = await _repository.GetPagedByDiscussionIdAsync(discussion.Id, 0, 10);

        await Assert.That(result.Items.Count()).IsEqualTo(0);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

}
