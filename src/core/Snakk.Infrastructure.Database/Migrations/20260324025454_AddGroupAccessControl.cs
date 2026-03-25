using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "Space",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "Hub",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "Community",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EntityGroupAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    CanRead = table.Column<bool>(type: "boolean", nullable: false),
                    CanWrite = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityGroupAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityGroupAccess_CommunityGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CommunityGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntityGroupAccess_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntityGroupAccess_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntityGroupAccess_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntityGroupAccess_CommunityId",
                table: "EntityGroupAccess",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityGroupAccess_GroupId_CommunityId",
                table: "EntityGroupAccess",
                columns: new[] { "GroupId", "CommunityId" },
                unique: true,
                filter: "\"CommunityId\" IS NOT NULL AND \"HubId\" IS NULL AND \"SpaceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EntityGroupAccess_GroupId_HubId",
                table: "EntityGroupAccess",
                columns: new[] { "GroupId", "HubId" },
                unique: true,
                filter: "\"HubId\" IS NOT NULL AND \"SpaceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EntityGroupAccess_GroupId_SpaceId",
                table: "EntityGroupAccess",
                columns: new[] { "GroupId", "SpaceId" },
                unique: true,
                filter: "\"SpaceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EntityGroupAccess_HubId",
                table: "EntityGroupAccess",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityGroupAccess_SpaceId",
                table: "EntityGroupAccess",
                column: "SpaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityGroupAccess");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "Community");
        }
    }
}
