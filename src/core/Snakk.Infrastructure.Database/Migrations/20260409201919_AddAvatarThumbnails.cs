using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarThumbnailFileName",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarThumbnailFileName",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityLanguageCode",
                table: "Space",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HubLanguageCode",
                table: "Space",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "Space",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarThumbnailFileName",
                table: "Hub",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityLanguageCode",
                table: "Hub",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "Hub",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarThumbnailFileName",
                table: "Community",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "Community",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarThumbnailFileName",
                table: "User");

            migrationBuilder.DropColumn(
                name: "AvatarThumbnailFileName",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "CommunityLanguageCode",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "HubLanguageCode",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "AvatarThumbnailFileName",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "CommunityLanguageCode",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "Hub");

            migrationBuilder.DropColumn(
                name: "AvatarThumbnailFileName",
                table: "Community");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "Community");
        }
    }
}
