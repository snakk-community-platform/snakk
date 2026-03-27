using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMediaLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Media_IsDraft_DraftExpiresAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "DraftExpiresAt",
                table: "Media");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Media",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReadyForDeletion",
                table: "Media",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Media_IsDeleted",
                table: "Media",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Media_IsReadyForDeletion",
                table: "Media",
                column: "IsReadyForDeletion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Media_IsDeleted",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_Media_IsReadyForDeletion",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "IsReadyForDeletion",
                table: "Media");

            migrationBuilder.AddColumn<DateTime>(
                name: "DraftExpiresAt",
                table: "Media",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_IsDraft_DraftExpiresAt",
                table: "Media",
                columns: new[] { "IsDraft", "DraftExpiresAt" });
        }
    }
}
