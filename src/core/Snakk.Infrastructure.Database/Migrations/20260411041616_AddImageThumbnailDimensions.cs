using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddImageThumbnailDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediumThumbnailHeight",
                table: "Image",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediumThumbnailWidth",
                table: "Image",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThumbnailHeight",
                table: "Image",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThumbnailWidth",
                table: "Image",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediumThumbnailHeight",
                table: "Image");

            migrationBuilder.DropColumn(
                name: "MediumThumbnailWidth",
                table: "Image");

            migrationBuilder.DropColumn(
                name: "ThumbnailHeight",
                table: "Image");

            migrationBuilder.DropColumn(
                name: "ThumbnailWidth",
                table: "Image");
        }
    }
}
