using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Database.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizePublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AchievementPublicId",
                table: "UserAchievementProgress",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserPublicId",
                table: "UserAchievementProgress",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AchievementPublicId",
                table: "UserAchievement",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserPublicId",
                table: "UserAchievement",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostPublicId",
                table: "Reaction",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserPublicId",
                table: "Reaction",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommunityId",
                table: "Post",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CommunityPublicId",
                table: "Post",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscussionPublicId",
                table: "Post",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HubId",
                table: "Post",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HubPublicId",
                table: "Post",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpaceId",
                table: "Post",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SpacePublicId",
                table: "Post",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentionedUserPublicId",
                table: "Mention",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostPublicId",
                table: "Mention",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityPublicId",
                table: "Hub",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscussionPublicId",
                table: "Follow",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowedUserPublicId",
                table: "Follow",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpacePublicId",
                table: "Follow",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserPublicId",
                table: "Follow",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommunityId",
                table: "Discussion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CommunityPublicId",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserPublicId",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HubId",
                table: "Discussion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HubPublicId",
                table: "Discussion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpacePublicId",
                table: "Discussion",
                type: "text",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AchievementPublicId",
                table: "UserAchievementProgress");

            migrationBuilder.DropColumn(
                name: "UserPublicId",
                table: "UserAchievementProgress");

            migrationBuilder.DropColumn(
                name: "AchievementPublicId",
                table: "UserAchievement");

            migrationBuilder.DropColumn(
                name: "UserPublicId",
                table: "UserAchievement");

            migrationBuilder.DropColumn(
                name: "PostPublicId",
                table: "Reaction");

            migrationBuilder.DropColumn(
                name: "UserPublicId",
                table: "Reaction");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "CommunityPublicId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "DiscussionPublicId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "HubId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "HubPublicId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "SpacePublicId",
                table: "Post");

            migrationBuilder.DropColumn(
                name: "MentionedUserPublicId",
                table: "Mention");

            migrationBuilder.DropColumn(
                name: "PostPublicId",
                table: "Mention");

            migrationBuilder.DropColumn(
                name: "CommunityPublicId",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "DiscussionPublicId",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "FollowedUserPublicId",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "SpacePublicId",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "UserPublicId",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "CommunityPublicId",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "CreatedByUserPublicId",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "HubId",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "HubPublicId",
                table: "Discussion");

            migrationBuilder.DropColumn(
                name: "SpacePublicId",
                table: "Discussion");
        }
    }
}
