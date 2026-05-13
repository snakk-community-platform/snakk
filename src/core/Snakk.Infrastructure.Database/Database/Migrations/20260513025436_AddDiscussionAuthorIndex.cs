using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionAuthorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Discussion_CreatedByUserPublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "CreatedByUserPublicId", "LastActivityAt", "Id" },
                descending: new[] { false, true, true },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discussion_CreatedByUserPublicId_LastActivityAt_Id_NotDeleted",
                table: "Discussion");
        }
    }
}
