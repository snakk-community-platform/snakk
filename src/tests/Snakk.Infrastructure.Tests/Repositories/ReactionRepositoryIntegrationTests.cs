using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class ReactionRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly ReactionDatabaseRepository _repository;

    public ReactionRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new ReactionDatabaseRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region Helper Methods

    private async Task<PostReactionDatabaseEntity> CreateReactionAsync(int userId, int postId, int typeId = 1)
    {
        var reaction = new PostReactionDatabaseEntity
        {
            PublicId = $"reaction_{Guid.NewGuid():N}",
            UserId = userId,
            PostId = postId,
            TypeId = typeId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Context.PostReactions.Add(reaction);
        await _db.Context.SaveChangesAsync();

        return reaction;
    }

    #endregion

    #region GetByUserPostAndTypeAsync Tests

    [Test]
    public async Task GetByUserPostAndTypeAsync_ExistingReaction_ReturnsEntity()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        await CreateReactionAsync(user.Id, post.Id, typeId: 1);

        var result = await _repository.GetByUserPostAndTypeAsync(user.Id, post.Id, 1);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId).IsEqualTo(user.Id);
        await Assert.That(result.PostId).IsEqualTo(post.Id);
        await Assert.That(result.TypeId).IsEqualTo(1);
    }

    [Test]
    public async Task GetByUserPostAndTypeAsync_NoReaction_ReturnsNull()
    {
        var (user, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetByUserPostAndTypeAsync(user.Id, post.Id, 1);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByUserPostAndTypeAsync_DifferentType_ReturnsNull()
    {
        var (user, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        await CreateReactionAsync(user.Id, post.Id, typeId: 1);

        var result = await _repository.GetByUserPostAndTypeAsync(user.Id, post.Id, 2);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByUserPostAndTypeAsync_IncludesNavigationProperties()
    {
        var (user, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        await CreateReactionAsync(user.Id, post.Id);

        var result = await _repository.GetByUserPostAndTypeAsync(user.Id, post.Id, 1);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.User).IsNotNull();
        await Assert.That(result.User.Id).IsEqualTo(user.Id);
        await Assert.That(result.Post).IsNotNull();
        await Assert.That(result.Post.Id).IsEqualTo(post.Id);
    }

    #endregion

    #region GetByPostIdAsync Tests

    [Test]
    public async Task GetByPostIdAsync_MultipleReactions_ReturnsAll()
    {
        var (user1, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        var user2 = await _builder.CreateUserAsync("User2");
        var user3 = await _builder.CreateUserAsync("User3");

        await CreateReactionAsync(user1.Id, post.Id, typeId: 1);
        await CreateReactionAsync(user2.Id, post.Id, typeId: 2);
        await CreateReactionAsync(user3.Id, post.Id, typeId: 1);

        var result = (await _repository.GetByPostIdAsync(post.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetByPostIdAsync_NoReactions_ReturnsEmpty()
    {
        var (_, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();

        var result = (await _repository.GetByPostIdAsync(post.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetByPostIdAsync_DoesNotReturnReactionsFromOtherPosts()
    {
        var (user, _, _, _, discussion, post1) = await _builder.CreateFullHierarchyAsync();
        var post2 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Second post");

        await CreateReactionAsync(user.Id, post1.Id, typeId: 1);
        await CreateReactionAsync(user.Id, post2.Id, typeId: 2);

        var result = (await _repository.GetByPostIdAsync(post1.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PostId).IsEqualTo(post1.Id);
    }

    #endregion

    #region GetCountsByPostIdAsync Tests

    [Test]
    public async Task GetCountsByPostIdAsync_MultipleTypes_ReturnsDictionaryWithCounts()
    {
        var (user1, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        var user2 = await _builder.CreateUserAsync("User2");
        var user3 = await _builder.CreateUserAsync("User3");
        var user4 = await _builder.CreateUserAsync("User4");

        // 2 thumbsUp (typeId=1), 1 heart (typeId=2), 1 laugh (typeId=3)
        await CreateReactionAsync(user1.Id, post.Id, typeId: 1);
        await CreateReactionAsync(user2.Id, post.Id, typeId: 1);
        await CreateReactionAsync(user3.Id, post.Id, typeId: 2);
        await CreateReactionAsync(user4.Id, post.Id, typeId: 3);

        var result = await _repository.GetCountsByPostIdAsync(post.Id);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[1]).IsEqualTo(2); // 2 thumbsUp
        await Assert.That(result[2]).IsEqualTo(1); // 1 heart
        await Assert.That(result[3]).IsEqualTo(1); // 1 laugh
    }

    [Test]
    public async Task GetCountsByPostIdAsync_NoReactions_ReturnsEmptyDictionary()
    {
        var (_, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetCountsByPostIdAsync(post.Id);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetUserReactionTypesForPostAsync Tests

    [Test]
    public async Task GetUserReactionTypesForPostAsync_UserHasReacted_ReturnsTypeIds()
    {
        var (user, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        await CreateReactionAsync(user.Id, post.Id, typeId: 2);
        await CreateReactionAsync(user.Id, post.Id, typeId: 5);

        var result = await _repository.GetUserReactionTypesForPostAsync(user.Id, post.Id);

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains(2);
        await Assert.That(result).Contains(5);
    }

    [Test]
    public async Task GetUserReactionTypesForPostAsync_UserHasNotReacted_ReturnsEmptyList()
    {
        var (user, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();

        var result = await _repository.GetUserReactionTypesForPostAsync(user.Id, post.Id);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task GetUserReactionTypesForPostAsync_OtherUserReaction_ReturnsEmptyList()
    {
        var (user1, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();
        var user2 = await _builder.CreateUserAsync("OtherUser");

        await CreateReactionAsync(user2.Id, post.Id, typeId: 1);

        var result = await _repository.GetUserReactionTypesForPostAsync(user1.Id, post.Id);

        await Assert.That(result).IsEmpty();
    }

    #endregion
}
