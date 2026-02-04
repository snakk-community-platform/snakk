using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AchievementCategoryLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementCategoryLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AchievementRequirementTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementRequirementTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Achievement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: true),
                    TierLevel = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    IsSecret = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequirementConfig = table.Column<string>(type: "jsonb", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    RequirementTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Achievement_AchievementCategoryLookup_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AchievementCategoryLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Achievement_AchievementRequirementTypeLookup_RequirementTyp~",
                        column: x => x.RequirementTypeId,
                        principalTable: "AchievementRequirementTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDisplayed = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AchievementId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievement_Achievement_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "Achievement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievement_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievementProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurrentValue = table.Column<int>(type: "integer", nullable: false),
                    TargetValue = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProgressData = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AchievementId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievementProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievementProgress_Achievement_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "Achievement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserAchievementProgress_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_CategoryId",
                table: "Achievement",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_IsActive_DisplayOrder",
                table: "Achievement",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_PublicId",
                table: "Achievement",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_RequirementTypeId",
                table: "Achievement",
                column: "RequirementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_Slug",
                table: "Achievement",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_AchievementId",
                table: "UserAchievement",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_PublicId",
                table: "UserAchievement",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_UserId_AchievementId",
                table: "UserAchievement",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_UserId_EarnedAt_Desc",
                table: "UserAchievement",
                columns: new[] { "UserId", "EarnedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_UserId_IsDisplayed_DisplayOrder",
                table: "UserAchievement",
                columns: new[] { "UserId", "IsDisplayed", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievementProgress_AchievementId",
                table: "UserAchievementProgress",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievementProgress_UserId_AchievementId",
                table: "UserAchievementProgress",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            // Seed AchievementCategoryLookup
            migrationBuilder.InsertData(
                table: "AchievementCategoryLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Engagement" },
                    { 2, "Social" },
                    { 3, "Content" },
                    { 4, "Milestones" },
                    { 5, "Moderation" },
                    { 6, "Special" }
                });

            // Seed AchievementRequirementTypeLookup
            migrationBuilder.InsertData(
                table: "AchievementRequirementTypeLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Count" },
                    { 2, "Streak" },
                    { 3, "Milestone" },
                    { 4, "TimeBased" }
                });

            // Seed initial achievements
            migrationBuilder.InsertData(
                table: "Achievement",
                columns: new[] { "Id", "PublicId", "Slug", "Name", "Description", "IconUrl", "CategoryId", "TierLevel", "Points", "IsSecret", "IsActive", "RequirementTypeId", "RequirementConfig", "DisplayOrder", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "01JKM7X0000000000000000001", "first-steps", "First Steps", "Create your first post", null, 1, 1, 10, false, true, 1, "{\"target\": 1, \"eventType\": \"POST_CREATE\"}", 10, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 2, "01JKM7X0000000000000000002", "conversation-starter", "Conversation Starter", "Create your first discussion", null, 1, 1, 15, false, true, 1, "{\"target\": 1, \"eventType\": \"DISCUSSION_CREATE\"}", 20, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 3, "01JKM7X0000000000000000003", "century-club", "Century Club", "Create 100 posts", null, 1, 3, 100, false, true, 1, "{\"target\": 100, \"eventType\": \"POST_CREATE\"}", 30, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 4, "01JKM7X0000000000000000004", "daily-contributor", "Daily Contributor", "Post at least once every day for 7 days", null, 1, 2, 50, false, true, 2, "{\"target\": 7, \"eventType\": \"DAILY_POST\"}", 40, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 5, "01JKM7X0000000000000000005", "popular", "Popular", "Receive 100 reactions across all your posts", null, 2, 3, 75, false, true, 1, "{\"target\": 100, \"eventType\": \"REACTION_RECEIVED\"}", 50, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 6, "01JKM7X0000000000000000006", "reactor", "Reactor", "Give your first reaction to a post", null, 2, 1, 5, false, true, 1, "{\"target\": 1, \"eventType\": \"REACTION_GIVEN\"}", 60, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 7, "01JKM7X0000000000000000007", "newbie", "Newbie", "Welcome to the community!", null, 4, 1, 5, false, true, 3, "{\"eventType\": \"ACCOUNT_CREATED\"}", 70, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null },
                    { 8, "01JKM7X0000000000000000008", "one-month-club", "One Month Club", "Member for 30 days", null, 4, 2, 25, false, true, 4, "{\"target\": 30, \"eventType\": \"ACCOUNT_AGE_DAYS\"}", 80, new DateTime(2026, 1, 31, 17, 58, 10, 0, DateTimeKind.Utc), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievement");

            migrationBuilder.DropTable(
                name: "UserAchievementProgress");

            migrationBuilder.DropTable(
                name: "Achievement");

            migrationBuilder.DropTable(
                name: "AchievementCategoryLookup");

            migrationBuilder.DropTable(
                name: "AchievementRequirementTypeLookup");
        }
    }
}
