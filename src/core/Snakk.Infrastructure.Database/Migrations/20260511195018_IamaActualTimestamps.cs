using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class IamaActualTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualEndedAtUtc",
                table: "DiscussionTypeIama",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualStartedAtUtc",
                table: "DiscussionTypeIama",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualEndedAtUtc",
                table: "DiscussionTypeIama");

            migrationBuilder.DropColumn(
                name: "ActualStartedAtUtc",
                table: "DiscussionTypeIama");
        }
    }
}
