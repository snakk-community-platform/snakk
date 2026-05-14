using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class MakeImageSha256HashUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Image_Sha256Hash",
                table: "Image");

            migrationBuilder.CreateIndex(
                name: "IX_Image_Sha256Hash",
                table: "Image",
                column: "Sha256Hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Image_Sha256Hash",
                table: "Image");

            migrationBuilder.CreateIndex(
                name: "IX_Image_Sha256Hash",
                table: "Image",
                column: "Sha256Hash");
        }
    }
}
