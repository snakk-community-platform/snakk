using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkPreviewImageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageBlurDataUri",
                table: "DiscussionLink",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalImagePath",
                table: "DiscussionLink",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OEmbedHtml",
                table: "DiscussionLink",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageBlurDataUri",
                table: "DiscussionLink");

            migrationBuilder.DropColumn(
                name: "LocalImagePath",
                table: "DiscussionLink");

            migrationBuilder.DropColumn(
                name: "OEmbedHtml",
                table: "DiscussionLink");
        }
    }
}
