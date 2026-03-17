using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamRevision",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamRevision",
                table: "Hub",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamRevision",
                table: "Community",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamRevision",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "TeamRevision",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "TeamRevision",
                table: "Community");
        }
    }
}
