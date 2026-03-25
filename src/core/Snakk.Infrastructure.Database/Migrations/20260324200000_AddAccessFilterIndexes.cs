using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Partial indexes on IsRestricted — only cover restricted entities (typically very few).
            // These allow the query planner to short-circuit the EXISTS access-check subquery
            // for the 99% of discussions that live in unrestricted spaces/hubs/communities.
            migrationBuilder.Sql(
                "CREATE INDEX \"IX_Space_IsRestricted\" ON \"Space\" (\"Id\") WHERE \"IsRestricted\" = true;");

            migrationBuilder.Sql(
                "CREATE INDEX \"IX_Hub_IsRestricted\" ON \"Hub\" (\"Id\") WHERE \"IsRestricted\" = true;");

            migrationBuilder.Sql(
                "CREATE INDEX \"IX_Community_IsRestricted\" ON \"Community\" (\"Id\") WHERE \"IsRestricted\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Space_IsRestricted\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Hub_IsRestricted\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Community_IsRestricted\";");
        }
    }
}
