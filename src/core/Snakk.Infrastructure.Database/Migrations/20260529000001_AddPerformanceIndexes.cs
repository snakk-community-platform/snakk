using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GetSpacesByHubAsync: DISTINCT ON (SpaceId, activity) to fetch latest discussion per space
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Discussion_SpaceId_Activity"
                ON "Discussion" ("SpaceId", (COALESCE("LastActivityAt", "CreatedAt")) DESC NULLS LAST)
                WHERE NOT "IsDeleted"
                """);

            // GetLatestActiveSpacesAsync: GROUP BY SpaceId, MAX(activity) filtered by CommunityId
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Discussion_CommunityId_Activity"
                ON "Discussion" ("CommunityId", (COALESCE("LastActivityAt", "CreatedAt")) DESC NULLS LAST)
                WHERE NOT "IsDeleted"
                """);

            // GetTopActiveSpacesSinceAsync: range scan on (CommunityId, CreatedAt) with SpaceId as covering column
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Post_CommunityId_CreatedAt"
                ON "Post" ("CommunityId", "CreatedAt" DESC)
                INCLUDE ("SpaceId")
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Discussion_SpaceId_Activity""");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Discussion_CommunityId_Activity""");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Post_CommunityId_CreatedAt""");
        }
    }
}
