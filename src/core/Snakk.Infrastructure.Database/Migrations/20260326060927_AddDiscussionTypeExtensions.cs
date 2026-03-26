using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionTypeExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowChangeVote",
                table: "DiscussionPoll",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CommunityAllowedDiscussionType",
                columns: table => new
                {
                    CommunityId = table.Column<int>(type: "integer", nullable: false),
                    DiscussionType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityAllowedDiscussionType", x => new { x.CommunityId, x.DiscussionType });
                    table.ForeignKey(
                        name: "FK_CommunityAllowedDiscussionType_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionDebate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    AllowNeutral = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionDebate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionDebate_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionGuide",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionGuide", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionGuide_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionQuestion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    AcceptedPostId = table.Column<int>(type: "integer", nullable: true),
                    SolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionQuestion_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscussionQuestion_Post_AcceptedPostId",
                        column: x => x.AcceptedPostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HubAllowedDiscussionType",
                columns: table => new
                {
                    HubId = table.Column<int>(type: "integer", nullable: false),
                    DiscussionType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubAllowedDiscussionType", x => new { x.HubId, x.DiscussionType });
                    table.ForeignKey(
                        name: "FK_HubAllowedDiscussionType_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionDebatePosition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DebateId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionDebatePosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionDebatePosition_DiscussionDebate_DebateId",
                        column: x => x.DebateId,
                        principalTable: "DiscussionDebate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostDebatePosition",
                columns: table => new
                {
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    PositionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostDebatePosition", x => x.PostId);
                    table.ForeignKey(
                        name: "FK_PostDebatePosition_DiscussionDebatePosition_PositionId",
                        column: x => x.PositionId,
                        principalTable: "DiscussionDebatePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostDebatePosition_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionDebate_DiscussionId",
                table: "DiscussionDebate",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebatePosition_DebateId_Index",
                table: "DiscussionDebatePosition",
                columns: new[] { "DebateId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionGuide_DiscussionId",
                table: "DiscussionGuide",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionQuestion_AcceptedPostId",
                table: "DiscussionQuestion",
                column: "AcceptedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionQuestion_DiscussionId",
                table: "DiscussionQuestion",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostDebatePosition_PositionId",
                table: "PostDebatePosition",
                column: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityAllowedDiscussionType");

            migrationBuilder.DropTable(
                name: "DiscussionGuide");

            migrationBuilder.DropTable(
                name: "DiscussionQuestion");

            migrationBuilder.DropTable(
                name: "HubAllowedDiscussionType");

            migrationBuilder.DropTable(
                name: "PostDebatePosition");

            migrationBuilder.DropTable(
                name: "DiscussionDebatePosition");

            migrationBuilder.DropTable(
                name: "DiscussionDebate");

            migrationBuilder.DropColumn(
                name: "AllowChangeVote",
                table: "DiscussionPoll");
        }
    }
}
