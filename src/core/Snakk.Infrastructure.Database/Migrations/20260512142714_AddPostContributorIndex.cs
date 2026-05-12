using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPostContributorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatedAt_CreatedByUserId_NotDeleted",
                table: "Post",
                columns: new[] { "CreatedAt", "CreatedByUserId" },
                descending: new[] { true, false },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Post_CreatedAt_CreatedByUserId_NotDeleted",
                table: "Post");
        }
    }
}
