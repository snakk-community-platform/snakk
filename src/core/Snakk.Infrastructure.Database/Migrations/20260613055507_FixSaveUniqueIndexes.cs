using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixSaveUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Save_UserId_DiscussionId_PostId",
                table: "Save");

            // Remove duplicate rows created by the broken composite index (PostgreSQL NULL != NULL meant
            // multiple saves for the same (UserId, DiscussionId) or (UserId, PostId) could exist).
            // Keep the earliest row (lowest Id) per group.
            migrationBuilder.Sql(@"
DELETE FROM ""Save""
WHERE ""DiscussionId"" IS NOT NULL
  AND ""Id"" NOT IN (
    SELECT MIN(""Id"")
    FROM ""Save""
    WHERE ""DiscussionId"" IS NOT NULL
    GROUP BY ""UserId"", ""DiscussionId""
  );

DELETE FROM ""Save""
WHERE ""PostId"" IS NOT NULL
  AND ""Id"" NOT IN (
    SELECT MIN(""Id"")
    FROM ""Save""
    WHERE ""PostId"" IS NOT NULL
    GROUP BY ""UserId"", ""PostId""
  );
");

            migrationBuilder.CreateIndex(
                name: "IX_Save_UserId_DiscussionId",
                table: "Save",
                columns: new[] { "UserId", "DiscussionId" },
                unique: true,
                filter: "\"DiscussionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Save_UserId_PostId",
                table: "Save",
                columns: new[] { "UserId", "PostId" },
                unique: true,
                filter: "\"PostId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Save_UserId_DiscussionId",
                table: "Save");

            migrationBuilder.DropIndex(
                name: "IX_Save_UserId_PostId",
                table: "Save");

            migrationBuilder.CreateIndex(
                name: "IX_Save_UserId_DiscussionId_PostId",
                table: "Save",
                columns: new[] { "UserId", "DiscussionId", "PostId" },
                unique: true,
                filter: "\"DiscussionId\" IS NOT NULL OR \"PostId\" IS NOT NULL");
        }
    }
}
