using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeDiscussionLastPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastPostAuthorAvatarFileName",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPostAuthorAvatarThumbnailFileName",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPostAuthorDisplayName",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPostAuthorPublicId",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPostPlainTextExcerpt",
                table: "Discussion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Backfill: populate from the most recent non-first post per discussion
            migrationBuilder.Sql("""
                UPDATE "Discussion" d
                SET "LastPostAuthorPublicId"               = sub."PublicId",
                    "LastPostAuthorDisplayName"            = sub."DisplayName",
                    "LastPostAuthorAvatarFileName"         = sub."AvatarFileName",
                    "LastPostAuthorAvatarThumbnailFileName" = sub."AvatarThumbnailFileName",
                    "LastPostPlainTextExcerpt"             = sub."PlainTextExcerpt"
                FROM (
                    SELECT DISTINCT ON (p."DiscussionId")
                           p."DiscussionId",
                           u."PublicId",
                           u."DisplayName",
                           u."AvatarFileName",
                           u."AvatarThumbnailFileName",
                           p."PlainTextExcerpt"
                    FROM   "Post"   p
                    JOIN   "User"   u ON u."Id" = p."CreatedByUserId" AND NOT u."IsDeleted"
                    WHERE  NOT p."IsDeleted" AND NOT p."IsFirstPost"
                    ORDER  BY p."DiscussionId", p."CreatedAt" DESC
                ) sub
                WHERE d."Id" = sub."DiscussionId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPostAuthorAvatarFileName",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "LastPostAuthorAvatarThumbnailFileName",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "LastPostAuthorDisplayName",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "LastPostAuthorPublicId",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "LastPostPlainTextExcerpt",
                table: "Discussion");
        }
    }
}
