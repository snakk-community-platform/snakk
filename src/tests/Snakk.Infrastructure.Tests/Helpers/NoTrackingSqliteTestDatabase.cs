using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Tests.Helpers;

/// <summary>
/// A SQLite-backed test database that mirrors the production
/// <c>QueryTrackingBehavior.NoTrackingWithIdentityResolution</c> setting from
/// <c>Snakk.Api/ServiceCollectionExtensions.cs</c>. Required for regression tests
/// of the silent-no-op-SaveChanges class-of-bug, since:
///
/// - The InMemory provider keeps tracked entities across queries within the same
///   context (via the change tracker's identity map), so a service that does
///   `Add` + `Save` then later reads + mutates + `Save` will appear to work even
///   when production (with NoTracking) silently drops the second `Save`.
/// - The default <see cref="SqliteTestDatabase"/> uses EF's default
///   <c>TrackAll</c> behavior, so it has the same blind spot.
///
/// Tests using <em>this</em> helper see the production behavior: every query
/// returns untracked entities unless the call site explicitly opts back in with
/// <c>.AsTracking()</c>, which is exactly the contract the production fixes
/// re-establish.
/// </summary>
public sealed class NoTrackingSqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SnakkDbContext Context { get; }

    public NoTrackingSqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Context = NewContext();
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// A fresh context sharing the same SQLite connection. Useful for tests that
    /// need to assert "the mutation made by the service-under-test is visible to
    /// a separate request" — production semantics, where each request gets its
    /// own scoped DbContext.
    /// </summary>
    public SnakkDbContext CreateSeparateContext() => NewContext();

    private SnakkDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseSqlite(_connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
            .Options;
        return new SnakkDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
