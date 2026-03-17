using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class UnifyRuleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the unified Rule table first
            migrationBuilder.CreateTable(
                name: "Rule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rule_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rule_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rule_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rule_CommunityId",
                table: "Rule",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Rule_HubId",
                table: "Rule",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Rule_SpaceId",
                table: "Rule",
                column: "SpaceId");

            // 2. Migrate existing data from old tables into unified table
            migrationBuilder.Sql("""
                INSERT INTO "Rule" ("Title", "Description", "SortOrder", "CreatedAt", "UpdatedAt", "CommunityId", "HubId", "SpaceId")
                SELECT "Title", "Description", "SortOrder", "CreatedAt", "UpdatedAt", "CommunityId", NULL, NULL
                FROM "CommunityRule";

                INSERT INTO "Rule" ("Title", "Description", "SortOrder", "CreatedAt", "UpdatedAt", "CommunityId", "HubId", "SpaceId")
                SELECT "Title", "Description", "SortOrder", "CreatedAt", "UpdatedAt", NULL, "HubId", NULL
                FROM "HubRule";

                INSERT INTO "Rule" ("Title", "Description", "SortOrder", "CreatedAt", "UpdatedAt", "CommunityId", "HubId", "SpaceId")
                SELECT "Title", "Description", "SortOrder", "CreatedAt", "UpdatedAt", NULL, NULL, "SpaceId"
                FROM "SpaceRule";
                """);

            // 3. Drop old tables after data is migrated
            migrationBuilder.DropTable(
                name: "CommunityRule");

            migrationBuilder.DropTable(
                name: "HubRule");

            migrationBuilder.DropTable(
                name: "SpaceRule");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rule");

            migrationBuilder.CreateTable(
                name: "CommunityRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommunityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityRule_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HubRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HubId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HubRule_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpaceRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpaceId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpaceRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpaceRule_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRule_CommunityId",
                table: "CommunityRule",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_HubRule_HubId",
                table: "HubRule",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceRule_SpaceId",
                table: "SpaceRule",
                column: "SpaceId");
        }
    }
}
