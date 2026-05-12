using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeIamaCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BestQuestionCount",
                table: "DiscussionTypeIama",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OfficialAnswerCount",
                table: "DiscussionTypeIama",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "DiscussionTypeIama" i
                SET "OfficialAnswerCount" = (
                        SELECT count(*) FROM "DiscussionTypeIamaOfficialAnswer" WHERE "IamaId" = i."Id"
                    ),
                    "BestQuestionCount" = (
                        SELECT count(*) FROM "DiscussionTypeIamaBestQuestion" WHERE "IamaId" = i."Id"
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestQuestionCount",
                table: "DiscussionTypeIama");

            migrationBuilder.DropColumn(
                name: "OfficialAnswerCount",
                table: "DiscussionTypeIama");
        }
    }
}
