using Microsoft.EntityFrameworkCore;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// CounterService uses ExecuteUpdateAsync which is not supported by the InMemory provider.
/// These tests cover the early-return logic when entities are not found.
/// Full integration tests would require a real database provider.
/// </summary>
public class CounterServiceTests : IDisposable
{
    private readonly string _dbName = $"CounterServiceTests_{Guid.NewGuid()}";
    private readonly CounterService _service;

    public CounterServiceTests()
    {
        _service = new CounterService(new InMemoryDbContextFactory(_dbName));
    }

    public void Dispose()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(_dbName).Options;
        using var db = new SnakkDbContext(options);
        db.Database.EnsureDeleted();
    }

    private sealed class InMemoryDbContextFactory(string dbName) : IDbContextFactory<SnakkDbContext>
    {
        public SnakkDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<SnakkDbContext>()
                .UseInMemoryDatabase(dbName).Options);
    }

    #region IncrementDiscussionCountAsync Tests

    [Test]
    public async Task IncrementDiscussionCountAsync_NonexistentSpace_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementDiscussionCountAsync(SpaceId.From("nonexistent"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementDiscussionCountAsync Tests

    [Test]
    public async Task DecrementDiscussionCountAsync_NonexistentSpace_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementDiscussionCountAsync(SpaceId.From("nonexistent"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region IncrementPostCountAsync Tests

    [Test]
    public async Task IncrementPostCountAsync_NonexistentDiscussion_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementPostCountAsync(DiscussionId.From("nonexistent"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementPostCountAsync Tests

    [Test]
    public async Task DecrementPostCountAsync_NonexistentDiscussion_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementPostCountAsync(DiscussionId.From("nonexistent"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region IncrementReactionCountAsync Tests

    [Test]
    public async Task IncrementReactionCountAsync_NonexistentDiscussion_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementReactionCountAsync(
            PostId.From("nonexistent"), DiscussionId.From("nonexistent"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementReactionCountAsync Tests

    [Test]
    public async Task DecrementReactionCountAsync_NonexistentDiscussion_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementReactionCountAsync(
            PostId.From("nonexistent"), DiscussionId.From("nonexistent"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
