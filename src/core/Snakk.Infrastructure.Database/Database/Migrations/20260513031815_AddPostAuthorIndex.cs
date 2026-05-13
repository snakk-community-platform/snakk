using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPostAuthorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatedByUserPublicId_CreatedAt_NotDeleted",
                table: "Post",
                columns: new[] { "CreatedByUserPublicId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Post_CreatedByUserPublicId_CreatedAt_NotDeleted",
                table: "Post");
        }
    }
}
