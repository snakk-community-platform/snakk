using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAllLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Achievement_AchievementCategoryLookup_CategoryId",
                table: "Achievement");

            migrationBuilder.DropForeignKey(
                name: "FK_Achievement_AchievementRequirementTypeLookup_RequirementTyp~",
                table: "Achievement");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLog_AuditLogSeverityLookup_SeverityId",
                table: "AuditLog");

            migrationBuilder.DropForeignKey(
                name: "FK_Community_CommunityVisibilityLookup_VisibilityId",
                table: "Community");

            migrationBuilder.DropForeignKey(
                name: "FK_Follow_FollowLevelLookup_LevelId",
                table: "Follow");

            migrationBuilder.DropForeignKey(
                name: "FK_Follow_FollowTargetTypeLookup_TargetTypeId",
                table: "Follow");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationLog_ModerationActionLookup_ActionId",
                table: "ModerationLog");

            migrationBuilder.DropForeignKey(
                name: "FK_Notification_NotificationTypeLookup_TypeId",
                table: "Notification");

            migrationBuilder.DropForeignKey(
                name: "FK_Reaction_ReactionTypeLookup_TypeId",
                table: "Reaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Report_ReportStatusLookup_StatusId",
                table: "Report");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBan_BanTypeLookup_BanTypeId",
                table: "UserBan");

            migrationBuilder.DropTable(
                name: "AchievementCategoryLookup");

            migrationBuilder.DropTable(
                name: "AchievementRequirementTypeLookup");

            migrationBuilder.DropTable(
                name: "AuditLogSeverityLookup");

            migrationBuilder.DropTable(
                name: "BanTypeLookup");

            migrationBuilder.DropTable(
                name: "CommunityVisibilityLookup");

            migrationBuilder.DropTable(
                name: "FollowLevelLookup");

            migrationBuilder.DropTable(
                name: "FollowTargetTypeLookup");

            migrationBuilder.DropTable(
                name: "ModerationActionLookup");

            migrationBuilder.DropTable(
                name: "NotificationTypeLookup");

            migrationBuilder.DropTable(
                name: "ReactionTypeLookup");

            migrationBuilder.DropTable(
                name: "ReportStatusLookup");

            migrationBuilder.DropIndex(
                name: "IX_UserBan_BanTypeId",
                table: "UserBan");

            migrationBuilder.DropIndex(
                name: "IX_Reaction_TypeId",
                table: "Reaction");

            migrationBuilder.DropIndex(
                name: "IX_Notification_TypeId",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_ModerationLog_ActionId",
                table: "ModerationLog");

            migrationBuilder.DropIndex(
                name: "IX_Follow_LevelId",
                table: "Follow");

            migrationBuilder.DropIndex(
                name: "IX_Follow_TargetTypeId",
                table: "Follow");

            migrationBuilder.DropIndex(
                name: "IX_Community_VisibilityId",
                table: "Community");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_SeverityId",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_Achievement_CategoryId",
                table: "Achievement");

            migrationBuilder.DropIndex(
                name: "IX_Achievement_RequirementTypeId",
                table: "Achievement");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "AuditLogSeverityLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogSeverityLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BanTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BanTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityVisibilityLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityVisibilityLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FollowLevelLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowLevelLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FollowTargetTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowTargetTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActionLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActionLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReactionTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionTypeLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportStatusLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportStatusLookup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_BanTypeId",
                table: "UserBan",
                column: "BanTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_TypeId",
                table: "Reaction",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TypeId",
                table: "Notification",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_ActionId",
                table: "ModerationLog",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_LevelId",
                table: "Follow",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_TargetTypeId",
                table: "Follow",
                column: "TargetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Community_VisibilityId",
                table: "Community",
                column: "VisibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_SeverityId",
                table: "AuditLog",
                column: "SeverityId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_CategoryId",
                table: "Achievement",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_RequirementTypeId",
                table: "Achievement",
                column: "RequirementTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Achievement_AchievementCategoryLookup_CategoryId",
                table: "Achievement",
                column: "CategoryId",
                principalTable: "AchievementCategoryLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Achievement_AchievementRequirementTypeLookup_RequirementTyp~",
                table: "Achievement",
                column: "RequirementTypeId",
                principalTable: "AchievementRequirementTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLog_AuditLogSeverityLookup_SeverityId",
                table: "AuditLog",
                column: "SeverityId",
                principalTable: "AuditLogSeverityLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Community_CommunityVisibilityLookup_VisibilityId",
                table: "Community",
                column: "VisibilityId",
                principalTable: "CommunityVisibilityLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Follow_FollowLevelLookup_LevelId",
                table: "Follow",
                column: "LevelId",
                principalTable: "FollowLevelLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Follow_FollowTargetTypeLookup_TargetTypeId",
                table: "Follow",
                column: "TargetTypeId",
                principalTable: "FollowTargetTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationLog_ModerationActionLookup_ActionId",
                table: "ModerationLog",
                column: "ActionId",
                principalTable: "ModerationActionLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_NotificationTypeLookup_TypeId",
                table: "Notification",
                column: "TypeId",
                principalTable: "NotificationTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reaction_ReactionTypeLookup_TypeId",
                table: "Reaction",
                column: "TypeId",
                principalTable: "ReactionTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Report_ReportStatusLookup_StatusId",
                table: "Report",
                column: "StatusId",
                principalTable: "ReportStatusLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserBan_BanTypeLookup_BanTypeId",
                table: "UserBan",
                column: "BanTypeId",
                principalTable: "BanTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
