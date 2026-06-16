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
    [Migration("20260615000002_AddCommunityVisibilityNameIndex")]
    public partial class AddCommunityVisibilityNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Covers GetPublicListedAsync: WHERE VisibilityId = 1 AND IsDeleted = FALSE ORDER BY Name.
            // Without this, the query does a full table scan + filesort on every community list load.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Community_VisibilityId_Name"
                ON "Community" ("VisibilityId", "Name")
                WHERE "IsDeleted" = FALSE
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Community_VisibilityId_Name""");
        }
    }
}
