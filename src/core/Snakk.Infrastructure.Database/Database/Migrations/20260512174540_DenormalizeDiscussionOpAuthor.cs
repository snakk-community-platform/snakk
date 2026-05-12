using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeDiscussionOpAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorAvatarFileName",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorAvatarThumbnailFileName",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorDisplayName",
                table: "Discussion",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorAvatarFileName",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "AuthorAvatarThumbnailFileName",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "AuthorDisplayName",
                table: "Discussion");
        }
    }
}
