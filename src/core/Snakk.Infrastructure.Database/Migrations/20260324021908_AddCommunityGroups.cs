using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityGroup_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityGroupMember",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AddedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityGroupMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityGroupMember_CommunityGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CommunityGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityGroupMember_User_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunityGroupMember_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGroup_CommunityId_IsPublic",
                table: "CommunityGroup",
                columns: new[] { "CommunityId", "IsPublic" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGroup_CommunityId_Slug",
                table: "CommunityGroup",
                columns: new[] { "CommunityId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGroup_PublicId",
                table: "CommunityGroup",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGroupMember_AddedByUserId",
                table: "CommunityGroupMember",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGroupMember_GroupId_UserId",
                table: "CommunityGroupMember",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityGroupMember_UserId_GroupId",
                table: "CommunityGroupMember",
                columns: new[] { "UserId", "GroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityGroupMember");

            migrationBuilder.DropTable(
                name: "CommunityGroup");
        }
    }
}
