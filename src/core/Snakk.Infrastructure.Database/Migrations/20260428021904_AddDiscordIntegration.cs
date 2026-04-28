using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordAvatarHash",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordLinkToken",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscordLinkTokenExpiry",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUserId",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUsername",
                table: "User",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordChannelName",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordInviteUrl",
                table: "Space",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordWebhookUrl",
                table: "Space",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordAvatarHash",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DiscordLinkToken",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DiscordLinkTokenExpiry",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DiscordUserId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DiscordUsername",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DiscordChannelName",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "DiscordInviteUrl",
                table: "Space");

            migrationBuilder.DropColumn(
                name: "DiscordWebhookUrl",
                table: "Space");
        }
    }
}
