using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EngagementScore",
                table: "Discussion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                @"UPDATE ""Discussion"" SET ""EngagementScore"" = ""PostCount"" + ""ReactionCount""");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_EngagementScore_Id_NotDeleted",
                table: "Discussion",
                columns: new[] { "EngagementScore", "Id" },
                descending: new[] { true, true },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discussion_EngagementScore_Id_NotDeleted",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "EngagementScore",
                table: "Discussion");
        }
    }
}
