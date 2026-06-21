using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionAuthorSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorSlug",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPostAuthorSlug",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE ""Discussion"" d
SET ""AuthorSlug"" = u.""Slug""
FROM ""User"" u
WHERE d.""CreatedByUserId"" = u.""Id""
  AND u.""Slug"" IS NOT NULL;
");

            migrationBuilder.Sql(@"
UPDATE ""Discussion"" d
SET ""LastPostAuthorSlug"" = u.""Slug""
FROM ""User"" u
WHERE d.""LastPostAuthorPublicId"" = u.""PublicId""
  AND u.""Slug"" IS NOT NULL
  AND d.""LastPostAuthorPublicId"" IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorSlug",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "LastPostAuthorSlug",
                table: "Discussion");
        }
    }
}
