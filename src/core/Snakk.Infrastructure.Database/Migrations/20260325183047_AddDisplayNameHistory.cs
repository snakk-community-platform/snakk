using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DisplayNameChangedAt",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisplayNameLocked",
                table: "User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DisplayNameHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PreviousName = table.Column<string>(type: "text", nullable: false),
                    NewName = table.Column<string>(type: "text", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisplayNameHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisplayNameHistory_User_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DisplayNameHistory_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisplayNameHistory_ChangedByUserId",
                table: "DisplayNameHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisplayNameHistory_NewName",
                table: "DisplayNameHistory",
                column: "NewName");

            migrationBuilder.CreateIndex(
                name: "IX_DisplayNameHistory_UserId",
                table: "DisplayNameHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisplayNameHistory");

            migrationBuilder.DropColumn(
                name: "DisplayNameChangedAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "IsDisplayNameLocked",
                table: "User");
        }
    }
}
