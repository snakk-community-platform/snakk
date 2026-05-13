using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRestrictedPartialIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Space_IsRestricted_True",
                table: "Space",
                column: "Id",
                filter: "\"IsRestricted\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Hub_IsRestricted_True",
                table: "Hub",
                column: "Id",
                filter: "\"IsRestricted\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Community_IsRestricted_True",
                table: "Community",
                column: "Id",
                filter: "\"IsRestricted\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Space_IsRestricted_True",
                table: "Space");

            migrationBuilder.DropIndex(
                name: "IX_Hub_IsRestricted_True",
                table: "Hub");

            migrationBuilder.DropIndex(
                name: "IX_Community_IsRestricted_True",
                table: "Community");
        }
    }
}
