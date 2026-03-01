using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDenormalizedUserAndDiscussionCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscussionCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FollowerCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReplyCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnreadNotificationCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FollowerCount",
                table: "Discussion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing data — split into separate statements to avoid timeout
            migrationBuilder.Sql("""
                UPDATE "User" SET "DiscussionCount" = (
                    SELECT COUNT(*) FROM "Discussion" WHERE "CreatedByUserId" = "User"."Id" AND "IsDeleted" = false
                )
                """);

            migrationBuilder.Sql("""
                UPDATE "User" SET "ReplyCount" = (
                    SELECT COUNT(*) FROM "Post" WHERE "CreatedByUserId" = "User"."Id" AND "IsFirstPost" = false AND "IsDeleted" = false
                )
                """);

            migrationBuilder.Sql("""
                UPDATE "User" SET "FollowerCount" = (
                    SELECT COUNT(*) FROM "Follow" WHERE "FollowedUserId" = "User"."Id" AND "TargetTypeId" = 3
                )
                """);

            migrationBuilder.Sql("""
                UPDATE "User" SET "UnreadNotificationCount" = (
                    SELECT COUNT(*) FROM "Notification" WHERE "RecipientUserId" = "User"."Id" AND "IsRead" = false
                )
                """);

            migrationBuilder.Sql("""
                UPDATE "Discussion" SET "FollowerCount" = (
                    SELECT COUNT(*) FROM "Follow" WHERE "DiscussionId" = "Discussion"."Id" AND "TargetTypeId" = 1
                )
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscussionCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "FollowerCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ReplyCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "UnreadNotificationCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "FollowerCount",
                table: "Discussion");
        }
    }
}
