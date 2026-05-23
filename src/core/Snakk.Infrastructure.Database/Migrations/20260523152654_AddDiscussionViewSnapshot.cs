using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionViewSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscussionViewSnapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Hour = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiscussionPublicId = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionViewSnapshot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionViewSnapshot_DiscussionPublicId_Hour",
                table: "DiscussionViewSnapshot",
                columns: new[] { "DiscussionPublicId", "Hour" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionViewSnapshot_Hour_DiscussionPublicId_CountryCode",
                table: "DiscussionViewSnapshot",
                columns: new[] { "Hour", "DiscussionPublicId", "CountryCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscussionViewSnapshot");
        }
    }
}
