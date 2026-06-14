using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Snakk.DataProtection;

public class DataProtectionDbContext(DbContextOptions<DataProtectionDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    // EnsureCreated() is a no-op when the database already exists (which it always
    // does after the first run). Use explicit DDL instead — idempotent on every restart.
    public async Task EnsureSchemaAsync()
    {
        // Skip on non-relational providers (e.g. the InMemory provider used in tests);
        // EnsureCreated will materialize the table via metadata instead.
        if (!Database.IsRelational())
        {
            await Database.EnsureCreatedAsync();
            return;
        }

        await Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
                "Id"           serial NOT NULL,
                "FriendlyName" text   NULL,
                "Xml"          text   NULL,
                CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id")
            )
            """);
    }
}
