using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Post_PublicId",
                table: "Post",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_SpaceId_CreatedAt_Desc",
                table: "Discussion",
                columns: new[] { "SpaceId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Action_Success_CreatedAt_Desc",
                table: "AuditLog",
                columns: new[] { "Action", "Success", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Category_CreatedAt_Desc",
                table: "AuditLog",
                columns: new[] { "Category", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CreatedAt_Desc_Category_Action",
                table: "AuditLog",
                columns: new[] { "CreatedAt", "Category", "Action" },
                descending: new[] { true, false, false });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_PublicId",
                table: "AuditLog",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_SeverityId_CreatedAt_Desc",
                table: "AuditLog",
                columns: new[] { "SeverityId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Post_PublicId",
                table: "Post");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_SpaceId_CreatedAt_Desc",
                table: "Discussion");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_Action_Success_CreatedAt_Desc",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_Category_CreatedAt_Desc",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_CreatedAt_Desc_Category_Action",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_PublicId",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_SeverityId_CreatedAt_Desc",
                table: "AuditLog");
        }
    }
}
