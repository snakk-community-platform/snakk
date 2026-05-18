using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAuthVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AuthVersion",
                table: "User",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "AuthVersionUpdatedAt",
                table: "User",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthVersion",
                table: "User");

            migrationBuilder.DropColumn(
                name: "AuthVersionUpdatedAt",
                table: "User");
        }
    }
}
