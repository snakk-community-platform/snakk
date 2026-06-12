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
    [Migration("20260612050000_HotTableAutovacuum")]
    public partial class HotTableAutovacuum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Companion to HotTableFillfactor: tables whose rows are updated constantly
            // (denormalized counters, LastActivityAt) accumulate dead tuples far faster
            // than the autovacuum default (20% of the table) triggers cleanup. Vacuuming
            // at 5% keeps HOT-update chains short and pages reusable, preserving the
            // benefit of fillfactor=90.
            migrationBuilder.Sql(@"ALTER TABLE ""Discussion"" SET (autovacuum_vacuum_scale_factor = 0.05, autovacuum_analyze_scale_factor = 0.05)");
            migrationBuilder.Sql(@"ALTER TABLE ""User"" SET (autovacuum_vacuum_scale_factor = 0.05, autovacuum_analyze_scale_factor = 0.05)");
            migrationBuilder.Sql(@"ALTER TABLE ""Space"" SET (autovacuum_vacuum_scale_factor = 0.05, autovacuum_analyze_scale_factor = 0.05)");
            migrationBuilder.Sql(@"ALTER TABLE ""Hub"" SET (autovacuum_vacuum_scale_factor = 0.05, autovacuum_analyze_scale_factor = 0.05)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Discussion"" RESET (autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor)");
            migrationBuilder.Sql(@"ALTER TABLE ""User"" RESET (autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor)");
            migrationBuilder.Sql(@"ALTER TABLE ""Space"" RESET (autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor)");
            migrationBuilder.Sql(@"ALTER TABLE ""Hub"" RESET (autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor)");
        }
    }
}
