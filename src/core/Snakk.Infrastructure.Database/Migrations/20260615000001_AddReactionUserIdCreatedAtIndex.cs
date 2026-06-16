using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    // Raw-SQL migration with no Designer file: the [DbContext]/[Migration] attributes
    // (normally generated in the Designer) are REQUIRED for EF to discover and apply
    // a migration - without them it is silently skipped.
    [DbContext(typeof(SnakkDbContext))]
    [Migration("20260615000001_AddReactionUserIdCreatedAtIndex")]
    public partial class AddReactionUserIdCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Covers the /my/reactions/* queries: WHERE UserId = X ORDER BY CreatedAt DESC LIMIT N.
            // The simple IX_Reaction_UserId index forces a sort after the filter; this composite
            // index makes ORDER BY + LIMIT a pure descending index scan.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Reaction_UserId_CreatedAt"
                ON "Reaction" ("UserId", "CreatedAt" DESC)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Reaction_UserId_CreatedAt""");
        }
    }
}
