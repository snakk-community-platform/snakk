using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixDmMessageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DmMessage_ConversationId_CreatedAt_Id",
                table: "DmMessage");

            migrationBuilder.CreateIndex(
                name: "IX_DmMessage_ConversationId_CreatedAt_Desc_Id_Desc",
                table: "DmMessage",
                columns: new[] { "ConversationId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DmMessage_ConversationId_CreatedAt_Desc_Id_Desc",
                table: "DmMessage");

            migrationBuilder.CreateIndex(
                name: "IX_DmMessage_ConversationId_CreatedAt_Id",
                table: "DmMessage",
                columns: new[] { "ConversationId", "CreatedAt", "Id" });
        }
    }
}
