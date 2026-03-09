using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Discussion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DiscussionLink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Domain = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionLink_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmbedUrl = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionMedia_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionPoll",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    AllowMultipleChoices = table.Column<bool>(type: "boolean", nullable: false),
                    VotesVisible = table.Column<bool>(type: "boolean", nullable: false),
                    ClosesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionPoll", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionPoll_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpaceAllowedDiscussionType",
                columns: table => new
                {
                    SpaceId = table.Column<int>(type: "integer", nullable: false),
                    DiscussionType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpaceAllowedDiscussionType", x => new { x.SpaceId, x.DiscussionType });
                    table.ForeignKey(
                        name: "FK_SpaceAllowedDiscussionType_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollOption",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PollId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollOption_DiscussionPoll_PollId",
                        column: x => x.PollId,
                        principalTable: "DiscussionPoll",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollVote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OptionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollVote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollVote_PollOption_OptionId",
                        column: x => x.OptionId,
                        principalTable: "PollOption",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollVote_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            // Seed all discussion types for existing spaces
            migrationBuilder.Sql("""
                INSERT INTO "SpaceAllowedDiscussionType" ("SpaceId", "DiscussionType")
                SELECT s."Id", t."Type"
                FROM "Space" s
                CROSS JOIN (VALUES (0), (1), (2), (3), (4), (5)) AS t("Type")
                WHERE s."IsDeleted" = false
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_SpaceId_Type",
                table: "Discussion",
                columns: new[] { "SpaceId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionLink_DiscussionId",
                table: "DiscussionLink",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionMedia_DiscussionId",
                table: "DiscussionMedia",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPoll_DiscussionId",
                table: "DiscussionPoll",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollOption_PollId_DisplayOrder",
                table: "PollOption",
                columns: new[] { "PollId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PollVote_OptionId_UserId",
                table: "PollVote",
                columns: new[] { "OptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollVote_UserId",
                table: "PollVote",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscussionLink");

            migrationBuilder.DropTable(
                name: "DiscussionMedia");

            migrationBuilder.DropTable(
                name: "PollVote");

            migrationBuilder.DropTable(
                name: "SpaceAllowedDiscussionType");

            migrationBuilder.DropTable(
                name: "PollOption");

            migrationBuilder.DropTable(
                name: "DiscussionPoll");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_SpaceId_Type",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Discussion");
        }
    }
}
