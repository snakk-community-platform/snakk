using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModerationLog_HubId",
                table: "ModerationLog");

            migrationBuilder.DropIndex(
                name: "IX_ModerationLog_SpaceId",
                table: "ModerationLog");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_User_OAuthProviderId",
                table: "User",
                column: "OAuthProviderId",
                filter: "\"OAuthProviderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientUserId_CreatedAt_Desc",
                table: "Notification",
                columns: new[] { "RecipientUserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_HubId_CreatedAt_Desc",
                table: "ModerationLog",
                columns: new[] { "HubId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_SpaceId_CreatedAt_Desc",
                table: "ModerationLog",
                columns: new[] { "SpaceId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_TrendScore_Desc",
                table: "Discussion",
                column: "TrendScore",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Email",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_OAuthProviderId",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Notification_RecipientUserId_CreatedAt_Desc",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_ModerationLog_HubId_CreatedAt_Desc",
                table: "ModerationLog");

            migrationBuilder.DropIndex(
                name: "IX_ModerationLog_SpaceId_CreatedAt_Desc",
                table: "ModerationLog");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_TrendScore_Desc",
                table: "Discussion");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_HubId",
                table: "ModerationLog",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_SpaceId",
                table: "ModerationLog",
                column: "SpaceId");
        }
    }
}
