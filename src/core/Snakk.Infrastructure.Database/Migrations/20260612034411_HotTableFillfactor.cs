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
    [Migration("20260612034411_HotTableFillfactor")]
    public partial class HotTableFillfactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set fillfactor=90 on hot-update tables that have denormalized counter columns.
            // These tables receive frequent UPDATE statements (PostCount, ReactionCount,
            // FollowerCount, TrendScore, EngagementScore, LastActivityAt, etc.) and leaving
            // room in each page reduces UPDATE-triggered page splits and HOT-chain fragmentation.
            // fillfactor=90 leaves 10% of each page free for in-page updates.

            migrationBuilder.Sql(@"ALTER TABLE ""Discussion"" SET (fillfactor = 90)");
            migrationBuilder.Sql(@"ALTER TABLE ""User"" SET (fillfactor = 90)");
            migrationBuilder.Sql(@"ALTER TABLE ""Space"" SET (fillfactor = 90)");
            migrationBuilder.Sql(@"ALTER TABLE ""Hub"" SET (fillfactor = 90)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Discussion"" RESET (fillfactor)");
            migrationBuilder.Sql(@"ALTER TABLE ""User"" RESET (fillfactor)");
            migrationBuilder.Sql(@"ALTER TABLE ""Space"" RESET (fillfactor)");
            migrationBuilder.Sql(@"ALTER TABLE ""Hub"" RESET (fillfactor)");
        }
    }
}
