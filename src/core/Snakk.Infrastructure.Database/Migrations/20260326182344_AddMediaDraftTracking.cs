using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaDraftTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcement");

            migrationBuilder.AddColumn<DateTime>(
                name: "DraftExpiresAt",
                table: "Media",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "Media",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Media",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Banner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    RenderedContent = table.Column<string>(type: "text", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    ScopeEntityId = table.Column<string>(type: "text", nullable: false),
                    VisibleFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VisibleUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDismissible = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banner", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Banner_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Media_IsDraft_DraftExpiresAt",
                table: "Media",
                columns: new[] { "IsDraft", "DraftExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Banner_CreatedByUserId",
                table: "Banner",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Banner_PublicId",
                table: "Banner",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Banner_ScopeId_ScopeEntityId",
                table: "Banner",
                columns: new[] { "ScopeId", "ScopeEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Banner");

            migrationBuilder.DropIndex(
                name: "IX_Media_IsDraft_DraftExpiresAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "DraftExpiresAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Media");

            migrationBuilder.CreateTable(
                name: "Announcement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDismissible = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    RenderedContent = table.Column<string>(type: "text", nullable: false),
                    ScopeEntityId = table.Column<string>(type: "text", nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    VisibleFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VisibleUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcement_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_CreatedByUserId",
                table: "Announcement",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_PublicId",
                table: "Announcement",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_ScopeId_ScopeEntityId",
                table: "Announcement",
                columns: new[] { "ScopeId", "ScopeEntityId" });
        }
    }
}
