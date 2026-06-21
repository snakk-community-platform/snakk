using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Infrastructure.Adapters;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;

namespace Snakk.Infrastructure.Tests.Adapters;

public class DiscussionReadStateRepositoryAdapterTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly DiscussionReadStateRepositoryAdapter _adapter;
    private readonly ServiceProvider _cacheProvider;

    public DiscussionReadStateRepositoryAdapterTests()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        _cacheProvider = services.BuildServiceProvider();

        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _adapter = new DiscussionReadStateRepositoryAdapter(_db.Context, _cacheProvider.GetRequiredService<HybridCache>());
    }

    public void Dispose()
    {
        _db.Dispose();
        _cacheProvider.Dispose();
    }

    #region GetAsync Tests

    [Test]
    public async Task GetAsync_NonExistentState_ReturnsNull()
    {
        var result = await _adapter.GetAsync(
            UserId.From("u_nonexistent"),
            DiscussionId.From("disc_nonexistent"));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAsync_ExistingState_ReturnsMappedDomainEntity()
    {
        var lastReadAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        _db.Context.DiscussionReadStates.Add(new DiscussionReadStateDatabaseEntity
        {
            UserId = "u_abc123",
            DiscussionId = "disc_xyz789",
            LastReadPostId = "post_last456",
            LastReadAt = lastReadAt
        });
        await _db.Context.SaveChangesAsync();

        var result = await _adapter.GetAsync(
            UserId.From("u_abc123"),
            DiscussionId.From("disc_xyz789"));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.UserId.Value).IsEqualTo("u_abc123");
        await Assert.That(result.DiscussionId.Value).IsEqualTo("disc_xyz789");
        await Assert.That(result.LastReadPostId).IsNotNull();
        await Assert.That(result.LastReadPostId!.Value).IsEqualTo("post_last456");
        await Assert.That(result.LastReadAt).IsEqualTo(lastReadAt);
    }

    #endregion

    #region SaveAsync Tests

    [Test]
    public async Task SaveAsync_NewReadState_InsertsIntoDB()
    {
        var readState = DiscussionReadState.Create(
            UserId.From("u_new123"),
            DiscussionId.From("disc_new456"),
            PostId.From("post_new789"));

        await _adapter.SaveAsync(readState);

        // Verify using a separate context to ensure it was actually persisted
        using var verifyContext = _db.CreateSeparateContext();
        var entity = await verifyContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs => rs.UserId == "u_new123" && rs.DiscussionId == "disc_new456");

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity!.LastReadPostId).IsEqualTo("post_new789");
    }

    [Test]
    public async Task SaveAsync_ExistingReadState_UpdatesExistingRecord()
    {
        // Insert initial state
        _db.Context.DiscussionReadStates.Add(new DiscussionReadStateDatabaseEntity
        {
            UserId = "u_update123",
            DiscussionId = "disc_update456",
            LastReadPostId = "post_old",
            LastReadAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.Context.SaveChangesAsync();

        // Update via adapter
        var updatedReadState = DiscussionReadState.Create(
            UserId.From("u_update123"),
            DiscussionId.From("disc_update456"),
            PostId.From("post_new"));

        await _adapter.SaveAsync(updatedReadState);

        // Verify the update using a separate context
        using var verifyContext = _db.CreateSeparateContext();
        var entity = await verifyContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs => rs.UserId == "u_update123" && rs.DiscussionId == "disc_update456");

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity!.LastReadPostId).IsEqualTo("post_new");

        // Ensure there is only one record (upsert, not duplicate insert)
        var count = await verifyContext.DiscussionReadStates
            .CountAsync(rs => rs.UserId == "u_update123" && rs.DiscussionId == "disc_update456");
        await Assert.That(count).IsEqualTo(1);
    }

    #endregion

    #region GetReadStatesForDiscussionsAsync Tests

    [Test]
    public async Task GetReadStatesForDiscussionsAsync_CalculatesPostNumbersCorrectly()
    {
        // Set up a full hierarchy with multiple posts
        var (user, _, _, _, discussion, firstPost) = await _builder.CreateFullHierarchyAsync();

        // Create additional posts with sequential timestamps
        var post2 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Second post");
        var post3 = await _builder.CreatePostAsync(discussion.Id, user.Id, "Third post");

        // Mark read state at post2
        _db.Context.DiscussionReadStates.Add(new DiscussionReadStateDatabaseEntity
        {
            UserId = user.PublicId,
            DiscussionId = discussion.PublicId,
            LastReadPostId = post2.PublicId,
            LastReadAt = DateTime.UtcNow
        });
        await _db.Context.SaveChangesAsync();

        var result = await _adapter.GetReadStatesForDiscussionsAsync(
            UserId.From(user.PublicId),
            [discussion.PublicId]);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].DiscussionId).IsEqualTo(discussion.PublicId);
        // Post number should be 2 since post2 is the 2nd non-deleted post created at or before post2's time
        await Assert.That(result[0].LastReadPostNumber).IsEqualTo(2);
    }

    #endregion
}
