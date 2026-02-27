using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

namespace Snakk.Infrastructure.Tests.Helpers;

public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SnakkDbContext Context { get; }

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new SnakkDbContext(options);
        Context.Database.EnsureCreated();
    }

    public SnakkDbContext CreateSeparateContext()
    {
        var options = new DbContextOptionsBuilder<SnakkDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new SnakkDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
