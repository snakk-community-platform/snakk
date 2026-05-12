using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeNotificationPublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorUserPublicId",
                table: "Notification",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserPublicId",
                table: "Notification",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDiscussionPublicId",
                table: "Notification",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePostPublicId",
                table: "Notification",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSpacePublicId",
                table: "Notification",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Notification" n
                SET "RecipientUserPublicId" = ru."PublicId"
                FROM "User" ru
                WHERE ru."Id" = n."RecipientUserId";

                UPDATE "Notification" n
                SET "SourcePostPublicId" = p."PublicId"
                FROM "Post" p
                WHERE p."Id" = n."SourcePostId";

                UPDATE "Notification" n
                SET "SourceDiscussionPublicId" = d."PublicId"
                FROM "Discussion" d
                WHERE d."Id" = n."SourceDiscussionId";

                UPDATE "Notification" n
                SET "SourceSpacePublicId" = s."PublicId"
                FROM "Space" s
                WHERE s."Id" = n."SourceSpaceId";

                UPDATE "Notification" n
                SET "ActorUserPublicId" = au."PublicId"
                FROM "User" au
                WHERE au."Id" = n."ActorUserId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorUserPublicId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "RecipientUserPublicId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "SourceDiscussionPublicId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "SourcePostPublicId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "SourceSpacePublicId",
                table: "Notification");
        }
    }
}
