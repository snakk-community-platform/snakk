using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRoleLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_UserRoleLookup_RoleId",
                table: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRole_UserRoleTypeLookup_RoleId",
                table: "UserRole");

            migrationBuilder.DropTable(
                name: "UserRoleLookup");

            migrationBuilder.DropTable(
                name: "UserRoleTypeLookup");

            migrationBuilder.DropIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_User_RoleId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "User",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRoleLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_User_RoleId",
                table: "User",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_User_UserRoleLookup_RoleId",
                table: "User",
                column: "RoleId",
                principalTable: "UserRoleLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRole_UserRoleTypeLookup_RoleId",
                table: "UserRole",
                column: "RoleId",
                principalTable: "UserRoleTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
