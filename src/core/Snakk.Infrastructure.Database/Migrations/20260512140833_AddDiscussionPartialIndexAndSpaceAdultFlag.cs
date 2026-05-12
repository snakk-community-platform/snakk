using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionPartialIndexAndSpaceAdultFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discussion_LastActivityAt_Id_Desc",
                table: "Discussion");

            migrationBuilder.AddColumn<bool>(
                name: "CommunityHideAdultDiscussionsFromLists",
                table: "Space",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill from parent community
            migrationBuilder.Sql("""
                UPDATE "Space" sp
                SET "CommunityHideAdultDiscussionsFromLists" = c."HideAdultDiscussionsFromLists"
                FROM "Hub" h
                JOIN "Community" c ON c."Id" = h."CommunityId" AND NOT c."IsDeleted"
                WHERE sp."HubId" = h."Id" AND NOT sp."IsDeleted";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "LastActivityAt", "Id" },
                descending: new[] { true, true },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discussion_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "CommunityHideAdultDiscussionsFromLists",
                table: "Space");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_LastActivityAt_Id_Desc",
                table: "Discussion",
                columns: new[] { "LastActivityAt", "Id" },
                descending: new bool[0]);
        }
    }
}
