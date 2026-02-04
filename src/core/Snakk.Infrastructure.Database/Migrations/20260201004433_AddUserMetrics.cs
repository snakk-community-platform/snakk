using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMetric",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MetricType = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMetric", x => new { x.UserId, x.MetricType, x.Scope, x.ScopeId });
                    table.ForeignKey(
                        name: "FK_UserMetric_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMetric_LastUpdated",
                table: "UserMetric",
                column: "LastUpdated");

            migrationBuilder.CreateIndex(
                name: "IX_UserMetric_UserId_Scope_ScopeId",
                table: "UserMetric",
                columns: new[] { "UserId", "Scope", "ScopeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMetric");
        }
    }
}
