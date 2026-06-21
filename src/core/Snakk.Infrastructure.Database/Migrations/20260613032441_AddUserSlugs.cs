using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayNameChangeCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisplayNameLastChangedAt",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSlugCustomized",
                table: "User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlugChangeCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlugLastChangedAt",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserSlugHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    OldSlug = table.Column<string>(type: "text", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSlugHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSlugHistory_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill slugs from DisplayName (with collision resolution) before adding unique index
            migrationBuilder.Sql(@"
DO $$
DECLARE
  r record;
  base_slug text;
  candidate_slug text;
  counter int;
BEGIN
  FOR r IN SELECT ""Id"", ""DisplayName"", ""PublicId"" FROM ""User"" ORDER BY ""Id""
  LOOP
    IF r.""DisplayName"" IS NOT NULL AND length(trim(r.""DisplayName"")) >= 3 THEN
      base_slug := lower(trim(r.""DisplayName""));
      base_slug := regexp_replace(base_slug, '[^a-z0-9]+', '-', 'g');
      base_slug := regexp_replace(base_slug, '(^-+|-+$)', '', 'g');
      base_slug := left(base_slug, 30);
      base_slug := regexp_replace(base_slug, '-+$', '');
    ELSE
      base_slug := lower(left(r.""PublicId"", 30));
      base_slug := regexp_replace(base_slug, '[^a-z0-9]+', '-', 'g');
      base_slug := regexp_replace(base_slug, '(^-+|-+$)', '', 'g');
    END IF;

    IF length(base_slug) < 3 THEN
      base_slug := 'user' || lower(substring(r.""PublicId"", 1, 26));
    END IF;

    candidate_slug := base_slug;
    counter := 0;
    WHILE EXISTS (SELECT 1 FROM ""User"" WHERE ""Slug"" = candidate_slug) LOOP
      counter := counter + 1;
      IF length(base_slug) + length(counter::text) > 30 THEN
        candidate_slug := left(base_slug, 30 - length(counter::text)) || counter::text;
      ELSE
        candidate_slug := base_slug || counter::text;
      END IF;
    END LOOP;

    UPDATE ""User"" SET ""Slug"" = candidate_slug WHERE ""Id"" = r.""Id"";
  END LOOP;
END $$;
");

            migrationBuilder.CreateIndex(
                name: "IX_User_Slug",
                table: "User",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSlugHistory_OldSlug",
                table: "UserSlugHistory",
                column: "OldSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSlugHistory_UserId",
                table: "UserSlugHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSlugHistory");

            migrationBuilder.DropIndex(
                name: "IX_User_Slug",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DisplayNameChangeCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DisplayNameLastChangedAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "IsSlugCustomized",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "User");

            migrationBuilder.DropColumn(
                name: "SlugChangeCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "SlugLastChangedAt",
                table: "User");
        }
    }
}
