using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarMicro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarMicroFileName",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarMicroFileName",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarMicroFileName",
                table: "Hub",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarMicroFileName",
                table: "Community",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarMicroFileName",
                table: "User");

            migrationBuilder.DropColumn(
                name: "AvatarMicroFileName",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "AvatarMicroFileName",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "AvatarMicroFileName",
                table: "Community");
        }
    }
}
