using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class NarrowReactionUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reaction_PostId_UserId_TypeId",
                table: "Reaction");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_PostId_UserId",
                table: "Reaction",
                columns: new[] { "PostId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reaction_PostId_UserId",
                table: "Reaction");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_PostId_UserId_TypeId",
                table: "Reaction",
                columns: new[] { "PostId", "UserId", "TypeId" },
                unique: true);
        }
    }
}
