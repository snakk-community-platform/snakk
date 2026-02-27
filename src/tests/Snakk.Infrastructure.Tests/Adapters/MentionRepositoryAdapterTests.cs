using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Domain.Entities;

namespace Snakk.Infrastructure.Tests.Adapters;

public class MentionRepositoryAdapterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly MentionRepositoryAdapter _adapter;

    public MentionRepositoryAdapterTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        var databaseRepo = new MentionDatabaseRepository(_db.Context);
        _adapter = new MentionRepositoryAdapter(databaseRepo, _db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region GetByPostIdAsync Tests

    [Test]
    public async Task GetByPostIdAsync_NonExistentPost_ReturnsEmpty()
    {
        var result = (await _adapter.GetByPostIdAsync(PostId.From("post_nonexistent"))).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetByPostIdAsync_ValidPostWithMentions_ReturnsMappedDomainEntities()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentionedUser1 = await _builder.CreateUserAsync("Mentioned1");
        var mentionedUser2 = await _builder.CreateUserAsync("Mentioned2");

        await _builder.CreateMentionAsync(post.Id, mentionedUser1.Id);
        await _builder.CreateMentionAsync(post.Id, mentionedUser2.Id);

        var result = (await _adapter.GetByPostIdAsync(PostId.From(post.PublicId))).ToList();

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(m => m.PostId.Value == post.PublicId)).IsTrue();
    }

    #endregion

    #region AddRangeAsync Tests

    [Test]
    public async Task AddRangeAsync_ValidMentions_ResolveForeignKeysAndPersist()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentionedUser = await _builder.CreateUserAsync("MentionTarget");

        var domainMention = Mention.Create(
            PostId.From(post.PublicId),
            UserId.From(mentionedUser.PublicId));

        await _adapter.AddRangeAsync([domainMention]);

        var result = (await _adapter.GetByPostIdAsync(PostId.From(post.PublicId))).ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PostId.Value).IsEqualTo(post.PublicId);
        await Assert.That(result[0].MentionedUserId.Value).IsEqualTo(mentionedUser.PublicId);
    }

    [Test]
    public async Task AddRangeAsync_InvalidReferences_SkipsMentionsWithBadForeignKeys()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();

        // Create a mention referencing a non-existent user
        var badMention = Mention.Create(
            PostId.From(post.PublicId),
            UserId.From("u_nonexistent"));

        await _adapter.AddRangeAsync([badMention]);

        var result = (await _adapter.GetByPostIdAsync(PostId.From(post.PublicId))).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region DeleteByPostIdAsync Tests

    [Test]
    public async Task DeleteByPostIdAsync_ExistingMentions_RemovesAllMentionsForPost()
    {
        var (user, _, _, _, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var mentionedUser1 = await _builder.CreateUserAsync("Del1");
        var mentionedUser2 = await _builder.CreateUserAsync("Del2");

        await _builder.CreateMentionAsync(post.Id, mentionedUser1.Id);
        await _builder.CreateMentionAsync(post.Id, mentionedUser2.Id);

        // Verify mentions exist
        var beforeDelete = (await _adapter.GetByPostIdAsync(PostId.From(post.PublicId))).ToList();
        await Assert.That(beforeDelete.Count).IsEqualTo(2);

        await _adapter.DeleteByPostIdAsync(PostId.From(post.PublicId));

        var afterDelete = (await _adapter.GetByPostIdAsync(PostId.From(post.PublicId))).ToList();
        await Assert.That(afterDelete.Count).IsEqualTo(0);
    }

    #endregion
}
