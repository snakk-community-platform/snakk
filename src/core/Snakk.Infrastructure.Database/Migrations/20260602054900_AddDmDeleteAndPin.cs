using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDmDeleteAndPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenderedContent",
                table: "DmMessage");

            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromRecipient",
                table: "DmMessage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromSender",
                table: "DmMessage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromInitiator",
                table: "DmConversation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenFromRecipient",
                table: "DmConversation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InitiatorPinned",
                table: "DmConversation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecipientPinned",
                table: "DmConversation",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenFromRecipient",
                table: "DmMessage");

            migrationBuilder.DropColumn(
                name: "HiddenFromSender",
                table: "DmMessage");

            migrationBuilder.DropColumn(
                name: "HiddenFromInitiator",
                table: "DmConversation");

            migrationBuilder.DropColumn(
                name: "HiddenFromRecipient",
                table: "DmConversation");

            migrationBuilder.DropColumn(
                name: "InitiatorPinned",
                table: "DmConversation");

            migrationBuilder.DropColumn(
                name: "RecipientPinned",
                table: "DmConversation");

            migrationBuilder.AddColumn<string>(
                name: "RenderedContent",
                table: "DmMessage",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
