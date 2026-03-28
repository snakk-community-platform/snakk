using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailEncryptionAndDeviceManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Email",
                table: "User");

            migrationBuilder.AddColumn<string>(
                name: "EmailHash",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_EmailHash",
                table: "User",
                column: "EmailHash",
                unique: true,
                filter: "\"EmailHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_EmailHash",
                table: "User");

            migrationBuilder.DropColumn(
                name: "EmailHash",
                table: "User");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);
        }
    }
}
