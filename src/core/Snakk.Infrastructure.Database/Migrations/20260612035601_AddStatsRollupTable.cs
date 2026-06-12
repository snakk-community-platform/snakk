using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStatsRollupTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatsRollup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatsRollup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatsRollup_StatKind_Rank",
                table: "StatsRollup",
                columns: new[] { "StatKind", "Rank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatsRollup");
        }
    }
}
