using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Services;
using StackExchange.Redis;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// CounterService delegates all counter operations to Redis.
/// These tests verify the service doesn't throw for any valid call.
/// </summary>
public class CounterServiceTests : IDisposable
{
    private readonly string _dbName = $"CounterServiceTests_{Guid.NewGuid()}";
    private readonly IDatabase _redisDb = Substitute.For<IDatabase>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly CounterService _service;

    public CounterServiceTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redisDb);
        _service = new CounterService(new InMemoryDbContextFactory(_dbName), _redis);
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
    public async Task IncrementDiscussionCountAsync_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementDiscussionCountAsync(SpaceId.From("any-space"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementDiscussionCountAsync Tests

    [Test]
    public async Task DecrementDiscussionCountAsync_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementDiscussionCountAsync(SpaceId.From("any-space"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region IncrementPostCountAsync Tests

    [Test]
    public async Task IncrementPostCountAsync_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementPostCountAsync(DiscussionId.From("any-discussion"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementPostCountAsync Tests

    [Test]
    public async Task DecrementPostCountAsync_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementPostCountAsync(DiscussionId.From("any-discussion"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region IncrementReactionCountAsync Tests

    [Test]
    public async Task IncrementReactionCountAsync_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementReactionCountAsync(
            PostId.From("any-post"), DiscussionId.From("any-discussion"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementReactionCountAsync Tests

    [Test]
    public async Task DecrementReactionCountAsync_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementReactionCountAsync(
            PostId.From("any-post"), DiscussionId.From("any-discussion"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
