using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizePostCreatorPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserPublicId",
                table: "Post",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Post" p
                SET "CreatedByUserPublicId" = u."PublicId"
                FROM "User" u
                WHERE p."CreatedByUserId" = u."Id" AND NOT u."IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserPublicId",
                table: "Post");
        }
    }
}
