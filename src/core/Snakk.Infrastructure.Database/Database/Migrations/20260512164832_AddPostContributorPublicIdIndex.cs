using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPostContributorPublicIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatedAt_CreatedByUserPublicId_NotDeleted",
                table: "Post",
                columns: new[] { "CreatedAt", "CreatedByUserPublicId" },
                descending: new[] { true, false },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Post_CreatedAt_CreatedByUserPublicId_NotDeleted",
                table: "Post");
        }
    }
}
