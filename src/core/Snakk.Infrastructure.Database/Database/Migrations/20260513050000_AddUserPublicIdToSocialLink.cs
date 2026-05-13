using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPublicIdToSocialLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserPublicId",
                table: "UserSocialLink",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "UserSocialLink" sl
                SET "UserPublicId" = u."PublicId"
                FROM "User" u
                WHERE sl."UserId" = u."Id"
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserSocialLink_UserPublicId",
                table: "UserSocialLink",
                column: "UserPublicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSocialLink_UserPublicId",
                table: "UserSocialLink");

            migrationBuilder.DropColumn(
                name: "UserPublicId",
                table: "UserSocialLink");
        }
    }
}
