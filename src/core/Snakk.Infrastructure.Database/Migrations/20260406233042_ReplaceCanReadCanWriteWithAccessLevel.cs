using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCanReadCanWriteWithAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new column
            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "GroupAccess",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 2. Migrate existing data: CanWrite=true → 3 (Write), CanRead=true → 1 (Read), else 0 (None)
            migrationBuilder.Sql("""
                UPDATE "GroupAccess" SET "AccessLevel" = CASE
                    WHEN "CanWrite" = true THEN 3
                    WHEN "CanRead" = true THEN 1
                    ELSE 0
                END
                """);

            // 3. Drop old columns
            migrationBuilder.DropColumn(
                name: "CanRead",
                table: "GroupAccess");

            migrationBuilder.DropColumn(
                name: "CanWrite",
                table: "GroupAccess");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "GroupAccess");

            migrationBuilder.AddColumn<bool>(
                name: "CanRead",
                table: "GroupAccess",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanWrite",
                table: "GroupAccess",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
