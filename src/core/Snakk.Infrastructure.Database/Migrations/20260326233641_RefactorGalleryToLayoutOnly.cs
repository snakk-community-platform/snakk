using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RefactorGalleryToLayoutOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbedUrl",
                table: "DiscussionMedia");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "DiscussionMedia");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "DiscussionMedia");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "DiscussionMedia");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "DiscussionMedia");

            migrationBuilder.AddColumn<string>(
                name: "Layout",
                table: "DiscussionMedia",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Layout",
                table: "DiscussionMedia");

            migrationBuilder.AddColumn<string>(
                name: "EmbedUrl",
                table: "DiscussionMedia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "DiscussionMedia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "DiscussionMedia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "DiscussionMedia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "DiscussionMedia",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");
        }
    }
}
