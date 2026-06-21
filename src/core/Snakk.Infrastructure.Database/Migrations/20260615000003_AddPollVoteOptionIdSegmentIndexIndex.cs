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
    [Migration("20260615000003_AddPollVoteOptionIdSegmentIndexIndex")]
    public partial class AddPollVoteOptionIdSegmentIndexIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Covers the segment vote count aggregation in PollService.FetchCachedPollDataAsync:
            // WHERE OptionId IN (...) GROUP BY OptionId, SegmentIndex.
            // The existing IX_DiscussionTypePollVote_OptionId_UserId covers the filter
            // but includes UserId which forces a full-index scan for the GROUP BY.
            // This covering index lets Postgres satisfy the GROUP BY with a single pass.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_DiscussionTypePollVote_OptionId_SegmentIndex"
                ON "DiscussionTypePollVote" ("OptionId", "SegmentIndex")
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_DiscussionTypePollVote_OptionId_SegmentIndex""");
        }
    }
}
