using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ScopeSlugsToParentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Space_HubId",
                table: "Space");

            migrationBuilder.DropIndex(
                name: "IX_Space_Slug",
                table: "Space");

            migrationBuilder.DropIndex(
                name: "IX_Hub_CommunityId",
                table: "Hub");

            migrationBuilder.DropIndex(
                name: "IX_Hub_Slug",
                table: "Hub");

            migrationBuilder.CreateIndex(
                name: "IX_Space_HubId_Slug",
                table: "Space",
                columns: new[] { "HubId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hub_CommunityId_Slug",
                table: "Hub",
                columns: new[] { "CommunityId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Space_HubId_Slug",
                table: "Space");

            migrationBuilder.DropIndex(
                name: "IX_Hub_CommunityId_Slug",
                table: "Hub");

            migrationBuilder.CreateIndex(
                name: "IX_Space_HubId",
                table: "Space",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Space_Slug",
                table: "Space",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_Hub_CommunityId",
                table: "Hub",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Hub_Slug",
                table: "Hub",
                column: "Slug");
        }
    }
}
