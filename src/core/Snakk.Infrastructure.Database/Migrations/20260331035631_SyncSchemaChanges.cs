using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class SyncSchemaChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscussionIama",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    IsScheduled = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionIama", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionIama_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IamaBestQuestion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IamaId = table.Column<int>(type: "integer", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IamaBestQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IamaBestQuestion_DiscussionIama_IamaId",
                        column: x => x.IamaId,
                        principalTable: "DiscussionIama",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IamaBestQuestion_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IamaOfficialAnswer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IamaId = table.Column<int>(type: "integer", nullable: false),
                    QuestionPostId = table.Column<int>(type: "integer", nullable: false),
                    AnswerPostId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IamaOfficialAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IamaOfficialAnswer_DiscussionIama_IamaId",
                        column: x => x.IamaId,
                        principalTable: "DiscussionIama",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IamaOfficialAnswer_Post_AnswerPostId",
                        column: x => x.AnswerPostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IamaOfficialAnswer_Post_QuestionPostId",
                        column: x => x.QuestionPostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionIama_DiscussionId",
                table: "DiscussionIama",
                column: "DiscussionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IamaBestQuestion_IamaId_DisplayOrder",
                table: "IamaBestQuestion",
                columns: new[] { "IamaId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IamaBestQuestion_PostId",
                table: "IamaBestQuestion",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_IamaOfficialAnswer_AnswerPostId",
                table: "IamaOfficialAnswer",
                column: "AnswerPostId");

            migrationBuilder.CreateIndex(
                name: "IX_IamaOfficialAnswer_IamaId_QuestionPostId",
                table: "IamaOfficialAnswer",
                columns: new[] { "IamaId", "QuestionPostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IamaOfficialAnswer_QuestionPostId",
                table: "IamaOfficialAnswer",
                column: "QuestionPostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IamaBestQuestion");

            migrationBuilder.DropTable(
                name: "IamaOfficialAnswer");

            migrationBuilder.DropTable(
                name: "DiscussionIama");
        }
    }
}
