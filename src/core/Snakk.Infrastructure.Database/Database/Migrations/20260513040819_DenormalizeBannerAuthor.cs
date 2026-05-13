using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeBannerAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserPublicId",
                table: "Banner",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Banner" b
                SET "CreatedByUserPublicId" = u."PublicId"
                FROM "User" u
                WHERE b."CreatedByUserId" = u."Id"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserPublicId",
                table: "Banner");
        }
    }
}
