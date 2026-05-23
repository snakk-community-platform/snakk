using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Snakk.Infrastructure.Database;

public class DataProtectionDbContext(DbContextOptions<DataProtectionDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    // EnsureCreated() is a no-op when the database already exists (which it always
    // does after the first run). Use explicit DDL instead — idempotent on every restart.
    public async Task EnsureSchemaAsync()
    {
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
