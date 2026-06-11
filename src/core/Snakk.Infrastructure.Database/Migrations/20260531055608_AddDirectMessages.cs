using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DmConversation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    InitiatorUserId = table.Column<int>(type: "integer", nullable: false),
                    InitiatorUserPublicId = table.Column<string>(type: "text", nullable: true),
                    RecipientUserId = table.Column<int>(type: "integer", nullable: false),
                    RecipientUserPublicId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageExcerpt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmConversation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DmConversation_User_InitiatorUserId",
                        column: x => x.InitiatorUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DmConversation_User_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DmMessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    ConversationPublicId = table.Column<string>(type: "text", nullable: true),
                    SenderUserId = table.Column<int>(type: "integer", nullable: false),
                    SenderUserPublicId = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    RenderedContent = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DmMessage_DmConversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DmConversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DmMessage_User_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DmReadState",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmReadState", x => new { x.UserId, x.ConversationId });
                    table.ForeignKey(
                        name: "FK_DmReadState_DmConversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DmConversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DmReadState_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DmConversation_InitiatorUserId_LastMessageAt_Desc",
                table: "DmConversation",
                columns: new[] { "InitiatorUserId", "LastMessageAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DmConversation_InitiatorUserId_RecipientUserId",
                table: "DmConversation",
                columns: new[] { "InitiatorUserId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DmConversation_PublicId",
                table: "DmConversation",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DmConversation_RecipientUserId_LastMessageAt_Desc",
                table: "DmConversation",
                columns: new[] { "RecipientUserId", "LastMessageAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DmMessage_ConversationId_CreatedAt_Id",
                table: "DmMessage",
                columns: new[] { "ConversationId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DmMessage_PublicId",
                table: "DmMessage",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DmMessage_SenderUserId",
                table: "DmMessage",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DmReadState_ConversationId",
                table: "DmReadState",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DmMessage");

            migrationBuilder.DropTable(
                name: "DmReadState");

            migrationBuilder.DropTable(
                name: "DmConversation");
        }
    }
}
