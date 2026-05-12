using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceStringIndexesWithIntegerFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Space_HubPublicId_NotDeleted",
                table: "Space");

            migrationBuilder.DropIndex(
                name: "IX_Post_CommunityPublicId_CreatedAt_NotDeleted",
                table: "Post");

            migrationBuilder.DropIndex(
                name: "IX_Post_DiscussionPublicId_CreatedAt_NotDeleted",
                table: "Post");

            migrationBuilder.DropIndex(
                name: "IX_Post_HubPublicId_CreatedAt_NotDeleted",
                table: "Post");

            migrationBuilder.DropIndex(
                name: "IX_Post_SpacePublicId_CreatedAt_NotDeleted",
                table: "Post");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_CommunityPublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_HubPublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_SpacePublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_CommunityId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "CommunityId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_HubId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "HubId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_SpaceId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "SpaceId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discussion_CommunityId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_HubId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_SpaceId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.CreateIndex(
                name: "IX_Space_HubPublicId_NotDeleted",
                table: "Space",
                column: "HubPublicId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Post_CommunityPublicId_CreatedAt_NotDeleted",
                table: "Post",
                columns: new[] { "CommunityPublicId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Post_DiscussionPublicId_CreatedAt_NotDeleted",
                table: "Post",
                columns: new[] { "DiscussionPublicId", "CreatedAt" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Post_HubPublicId_CreatedAt_NotDeleted",
                table: "Post",
                columns: new[] { "HubPublicId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Post_SpacePublicId_CreatedAt_NotDeleted",
                table: "Post",
                columns: new[] { "SpacePublicId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_CommunityPublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "CommunityPublicId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_HubPublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "HubPublicId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_SpacePublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "SpacePublicId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
