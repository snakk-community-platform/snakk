using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHubAndCommunityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasRules",
                table: "Space",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ParentCommunityHasRules",
                table: "Space",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ParentHubHasRules",
                table: "Space",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RulesRevision",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasRules",
                table: "Hub",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ParentCommunityHasRules",
                table: "Hub",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RulesRevision",
                table: "Hub",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasRules",
                table: "Community",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RulesRevision",
                table: "Community",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommunityId = table.Column<int>(type: "integer", nullable: false)
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
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRule_CommunityId",
                table: "CommunityRule",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_HubRule_HubId",
                table: "HubRule",
                column: "HubId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityRule");

            migrationBuilder.DropTable(
                name: "HubRule");

            migrationBuilder.DropColumn(
                name: "HasRules",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "ParentCommunityHasRules",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "ParentHubHasRules",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "RulesRevision",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "HasRules",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "ParentCommunityHasRules",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "RulesRevision",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "HasRules",
                table: "Community");

            migrationBuilder.DropColumn(
                name: "RulesRevision",
                table: "Community");
        }
    }
}
