using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;

namespace Snakk.Infrastructure.Tests.Repositories;

public class MentionDatabaseRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly MentionDatabaseRepository _repository;

    public MentionDatabaseRepositoryTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new MentionDatabaseRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingMention_ReturnsWithPostAndMentionedUserLoaded()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentionedUser = await _builder.CreateUserAsync("MentionedUser");
        var mention = await _builder.CreateMentionAsync(post.Id, mentionedUser.Id);

        var result = await _repository.GetByIdAsync(mention.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(mention.Id);
        await Assert.That(result.Post).IsNotNull();
        await Assert.That(result.Post.Id).IsEqualTo(post.Id);
        await Assert.That(result.MentionedUser).IsNotNull();
        await Assert.That(result.MentionedUser.DisplayName).IsEqualTo("MentionedUser");
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(99999);

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_MultipleMentions_ReturnsAllWithIncludes()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentionedUser1 = await _builder.CreateUserAsync("Mentioned1");
        var mentionedUser2 = await _builder.CreateUserAsync("Mentioned2");

        await _builder.CreateMentionAsync(post.Id, mentionedUser1.Id);
        await _builder.CreateMentionAsync(post.Id, mentionedUser2.Id);

        var result = (await _repository.GetAllAsync()).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Post).IsNotNull();
        await Assert.That(result[0].MentionedUser).IsNotNull();
        await Assert.That(result[1].Post).IsNotNull();
        await Assert.That(result[1].MentionedUser).IsNotNull();
    }

    #endregion

    #region GetByPostIdAsync Tests

    [Test]
    public async Task GetByPostIdAsync_MultipleMentionsForPost_ReturnsAll()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentioned1 = await _builder.CreateUserAsync("User1");
        var mentioned2 = await _builder.CreateUserAsync("User2");
        var mentioned3 = await _builder.CreateUserAsync("User3");

        await _builder.CreateMentionAsync(post.Id, mentioned1.Id);
        await _builder.CreateMentionAsync(post.Id, mentioned2.Id);
        await _builder.CreateMentionAsync(post.Id, mentioned3.Id);

        var result = (await _repository.GetByPostIdAsync(post.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.All(m => m.PostId == post.Id)).IsTrue();
        await Assert.That(result.All(m => m.Post is not null)).IsTrue();
        await Assert.That(result.All(m => m.MentionedUser is not null)).IsTrue();
    }

    [Test]
    public async Task GetByPostIdAsync_NoMentions_ReturnsEmpty()
    {
        var (_, _, _, _, _, post) = await _builder.CreateFullHierarchyAsync();

        var result = (await _repository.GetByPostIdAsync(post.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region AddRangeAsync Tests

    [Test]
    public async Task AddRangeAsync_MultipleMentions_AllExistAfterSave()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentioned1 = await _builder.CreateUserAsync("Bulk1");
        var mentioned2 = await _builder.CreateUserAsync("Bulk2");

        var mentions = new List<MentionDatabaseEntity>
        {
            new()
            {
                PublicId = $"men_bulk_1",
                PostId = post.Id,
                MentionedUserId = mentioned1.Id,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                PublicId = $"men_bulk_2",
                PostId = post.Id,
                MentionedUserId = mentioned2.Id,
                CreatedAt = DateTime.UtcNow
            }
        };

        await _repository.AddRangeAsync(mentions);
        await _db.Context.SaveChangesAsync();

        var result = (await _repository.GetByPostIdAsync(post.Id)).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.Any(m => m.MentionedUserId == mentioned1.Id)).IsTrue();
        await Assert.That(result.Any(m => m.MentionedUserId == mentioned2.Id)).IsTrue();
    }

    #endregion

    #region DeleteByPostIdAsync Tests

    [Test]
    public async Task DeleteByPostIdAsync_DeletesAllMentionsForPost()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentioned1 = await _builder.CreateUserAsync("Del1");
        var mentioned2 = await _builder.CreateUserAsync("Del2");

        await _builder.CreateMentionAsync(post.Id, mentioned1.Id);
        await _builder.CreateMentionAsync(post.Id, mentioned2.Id);

        // Verify mentions exist before delete
        var beforeDelete = (await _repository.GetByPostIdAsync(post.Id)).ToList();
        await Assert.That(beforeDelete.Count).IsEqualTo(2);

        await _repository.DeleteByPostIdAsync(post.Id);

        // Use a separate context to verify deletion since GetByPostIdAsync uses AsNoTracking
        var afterDelete = (await _repository.GetByPostIdAsync(post.Id)).ToList();
        await Assert.That(afterDelete.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteByPostIdAsync_DoesNotAffectMentionsForOtherPosts()
    {
        var (user, _, _, _, discussion, post1) = await _builder.CreateFullHierarchyAsync();
        var post2 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Second post");
        var mentioned = await _builder.CreateUserAsync("Mentioned");

        await _builder.CreateMentionAsync(post1.Id, mentioned.Id);
        await _builder.CreateMentionAsync(post2.Id, mentioned.Id);

        await _repository.DeleteByPostIdAsync(post1.Id);

        var post1Mentions = (await _repository.GetByPostIdAsync(post1.Id)).ToList();
        var post2Mentions = (await _repository.GetByPostIdAsync(post2.Id)).ToList();

        await Assert.That(post1Mentions.Count).IsEqualTo(0);
        await Assert.That(post2Mentions.Count).IsEqualTo(1);
        await Assert.That(post2Mentions[0].PostId).IsEqualTo(post2.Id);
    }

    #endregion
}
