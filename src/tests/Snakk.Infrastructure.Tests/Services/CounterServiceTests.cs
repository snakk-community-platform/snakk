using Microsoft.EntityFrameworkCore;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Services;

namespace Snakk.Infrastructure.Tests.Services;

/// <summary>
/// CounterService uses ExecuteUpdateAsync which is not supported by the InMemory provider.
/// These tests cover the early-return logic when entities are not found.
/// Full integration tests would require a real database provider.
/// </summary>
public class CounterServiceTests : IDisposable
{
    private readonly SnakkDbContext _context;
    private readonly CounterService _service;

    public CounterServiceTests()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseInMemoryDatabase(databaseName: $"CounterServiceTests_{Guid.NewGuid()}")
            .Options;
        _context = new SnakkDbContext(options);
        _service = new CounterService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region IncrementDiscussionCountAsync Tests

    [Test]
    public async Task IncrementDiscussionCountAsync_NonexistentSpace_ReturnsWithoutError()
    {
        // When the space doesn't exist, the service should return early
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

    #region IncrementUniqueReactorCountAsync Tests

    [Test]
    public async Task IncrementUniqueReactorCountAsync_NonexistentDiscussion_ReturnsWithoutError()
    {
        var act = async () => await _service.IncrementUniqueReactorCountAsync(
            DiscussionId.From("nonexistent"), UserId.From("user1"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region DecrementUniqueReactorCountAsync Tests

    [Test]
    public async Task DecrementUniqueReactorCountAsync_NonexistentDiscussion_ReturnsWithoutError()
    {
        var act = async () => await _service.DecrementUniqueReactorCountAsync(
            DiscussionId.From("nonexistent"), UserId.From("user1"));

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
