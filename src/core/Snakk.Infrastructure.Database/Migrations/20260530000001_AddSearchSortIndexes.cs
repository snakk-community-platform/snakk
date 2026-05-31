using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchSortIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SearchDiscussionsAsync "popular" sort: ORDER BY PostCount DESC WHERE NOT IsDeleted.
            // FTS path uses the GIN index (already exists); this covers the no-query browse path.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Discussion_PostCount_IsDeleted"
                ON "Discussion" ("PostCount" DESC)
                WHERE NOT "IsDeleted"
                """);

            // SearchDiscussionsAsync "reactions" sort: ORDER BY ReactionCount DESC WHERE NOT IsDeleted.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Discussion_ReactionCount_IsDeleted"
                ON "Discussion" ("ReactionCount" DESC)
                WHERE NOT "IsDeleted"
                """);

            // SearchPostsAsync "popular" / "reactions" sort: ORDER BY ReactionCount DESC WHERE NOT IsDeleted.
            // Posts share ReactionCount for both sort options (no per-post reply count exists).
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Post_ReactionCount_IsDeleted"
                ON "Post" ("ReactionCount" DESC)
                WHERE NOT "IsDeleted"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Discussion_PostCount_IsDeleted""");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Discussion_ReactionCount_IsDeleted""");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Post_ReactionCount_IsDeleted""");
        }
    }
}
