using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPostFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMilestone",
                table: "Post",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNecro",
                table: "Post",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOp",
                table: "Post",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUsersFirstPostInDiscussion",
                table: "Post",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUsersFirstPostInSpace",
                table: "Post",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMilestone",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "IsNecro",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "IsOp",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "IsUsersFirstPostInDiscussion",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "IsUsersFirstPostInSpace",
                table: "Post");
        }
    }
}
