using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarRevisionToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRole_User_UserDatabaseEntityId",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_UserRole_UserDatabaseEntityId",
                table: "UserRole");

            migrationBuilder.DropColumn(
                name: "UserDatabaseEntityId",
                table: "UserRole");

            migrationBuilder.AddColumn<int>(
                name: "AvatarRevision",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvatarRevision",
                table: "Space",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvatarRevision",
                table: "Hub",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvatarRevision",
                table: "Community",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarRevision",
                table: "User");

            migrationBuilder.DropColumn(
                name: "AvatarRevision",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "AvatarRevision",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "AvatarRevision",
                table: "Community");

            migrationBuilder.AddColumn<int>(
                name: "UserDatabaseEntityId",
                table: "UserRole",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserDatabaseEntityId",
                table: "UserRole",
                column: "UserDatabaseEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRole_User_UserDatabaseEntityId",
                table: "UserRole",
                column: "UserDatabaseEntityId",
                principalTable: "User",
                principalColumn: "Id");
        }
    }
}
