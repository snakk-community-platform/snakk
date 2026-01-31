using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class EnumToIntegerMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // STEP 1: Create lookup tables
            migrationBuilder.CreateTable(
                name: "CommunityVisibilityLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityVisibilityLookup", x => x.Id);
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
                name: "UserRoleLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleTypeLookup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleTypeLookup", x => x.Id);
                });

            // STEP 2: Seed lookup tables with enum values
            migrationBuilder.InsertData(
                table: "CommunityVisibilityLookup",
                columns: new[] { "Id", "Name", "Description" },
                values: new object[,]
                {
                    { 1, "PublicListed", "Visible in platform directory" },
                    { 2, "PublicUnlisted", "Accessible but not listed" }
                });

            migrationBuilder.InsertData(
                table: "FollowTargetTypeLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Discussion" },
                    { 2, "Space" },
                    { 3, "User" }
                });

            migrationBuilder.InsertData(
                table: "FollowLevelLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "DiscussionsOnly" },
                    { 2, "DiscussionsAndPosts" }
                });

            migrationBuilder.InsertData(
                table: "ModerationActionLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "DeletePost" },
                    { 2, "DeleteDiscussion" },
                    { 3, "BanUser" },
                    { 4, "UnbanUser" },
                    { 5, "AssignRole" },
                    { 6, "RevokeRole" },
                    { 7, "ResolveReport" },
                    { 8, "DismissReport" },
                    { 9, "EditPost" },
                    { 10, "LockDiscussion" }
                });

            migrationBuilder.InsertData(
                table: "NotificationTypeLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Mention" },
                    { 2, "Reply" },
                    { 3, "NewPostInFollowedDiscussion" },
                    { 4, "NewDiscussionInFollowedSpace" }
                });

            migrationBuilder.InsertData(
                table: "ReactionTypeLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "ThumbsUp" },
                    { 2, "Heart" },
                    { 3, "Eyes" }
                });

            migrationBuilder.InsertData(
                table: "ReportStatusLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Resolved" },
                    { 3, "Dismissed" }
                });

            migrationBuilder.InsertData(
                table: "BanTypeLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "WriteOnly" },
                    { 2, "ReadWrite" }
                });

            migrationBuilder.InsertData(
                table: "UserRoleLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Admin" },
                    { 2, "Mod" }
                });

            migrationBuilder.InsertData(
                table: "UserRoleTypeLookup",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "GlobalAdmin" },
                    { 2, "CommunityAdmin" },
                    { 3, "CommunityMod" },
                    { 4, "HubMod" },
                    { 5, "SpaceMod" }
                });

            // STEP 3: Add new integer columns (nullable temporarily for data migration)
            migrationBuilder.AddColumn<int>(
                name: "VisibilityId",
                table: "Community",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetTypeId",
                table: "Follow",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LevelId",
                table: "Follow",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActionId",
                table: "ModerationLog",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                table: "Notification",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                table: "Reaction",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "Report",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BanTypeId",
                table: "UserBan",
                type: "integer",
                nullable: true);

            // UserDatabaseEntity.RoleId is already nullable, so keep it that way
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "User",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "UserRole",
                type: "integer",
                nullable: true);

            // STEP 4: Populate integer columns by mapping from string values
            migrationBuilder.Sql(@"
                UPDATE ""Community""
                SET ""VisibilityId"" = CASE ""Visibility""
                    WHEN 'PublicListed' THEN 1
                    WHEN 'PublicUnlisted' THEN 2
                END");

            migrationBuilder.Sql(@"
                UPDATE ""Follow""
                SET ""TargetTypeId"" = CASE ""TargetType""
                    WHEN 'Discussion' THEN 1
                    WHEN 'Space' THEN 2
                    WHEN 'User' THEN 3
                END");

            migrationBuilder.Sql(@"
                UPDATE ""Follow""
                SET ""LevelId"" = CASE ""Level""
                    WHEN 'DiscussionsOnly' THEN 1
                    WHEN 'DiscussionsAndPosts' THEN 2
                END");

            migrationBuilder.Sql(@"
                UPDATE ""ModerationLog""
                SET ""ActionId"" = CASE ""Action""
                    WHEN 'DeletePost' THEN 1
                    WHEN 'DeleteDiscussion' THEN 2
                    WHEN 'BanUser' THEN 3
                    WHEN 'UnbanUser' THEN 4
                    WHEN 'AssignRole' THEN 5
                    WHEN 'RevokeRole' THEN 6
                    WHEN 'ResolveReport' THEN 7
                    WHEN 'DismissReport' THEN 8
                    WHEN 'EditPost' THEN 9
                    WHEN 'LockDiscussion' THEN 10
                END");

            migrationBuilder.Sql(@"
                UPDATE ""Notification""
                SET ""TypeId"" = CASE ""Type""
                    WHEN 'Mention' THEN 1
                    WHEN 'Reply' THEN 2
                    WHEN 'NewPostInFollowedDiscussion' THEN 3
                    WHEN 'NewDiscussionInFollowedSpace' THEN 4
                END");

            migrationBuilder.Sql(@"
                UPDATE ""Reaction""
                SET ""TypeId"" = CASE ""Type""
                    WHEN 'ThumbsUp' THEN 1
                    WHEN 'Heart' THEN 2
                    WHEN 'Eyes' THEN 3
                END");

            migrationBuilder.Sql(@"
                UPDATE ""Report""
                SET ""StatusId"" = CASE ""Status""
                    WHEN 'Pending' THEN 1
                    WHEN 'Resolved' THEN 2
                    WHEN 'Dismissed' THEN 3
                END");

            migrationBuilder.Sql(@"
                UPDATE ""UserBan""
                SET ""BanTypeId"" = CASE ""BanType""
                    WHEN 'WriteOnly' THEN 1
                    WHEN 'ReadWrite' THEN 2
                END");

            migrationBuilder.Sql(@"
                UPDATE ""User""
                SET ""RoleId"" = CASE ""Role""
                    WHEN 'Admin' THEN 1
                    WHEN 'Mod' THEN 2
                END
                WHERE ""Role"" IS NOT NULL");

            migrationBuilder.Sql(@"
                UPDATE ""UserRole""
                SET ""RoleId"" = CASE ""Role""
                    WHEN 'GlobalAdmin' THEN 1
                    WHEN 'CommunityAdmin' THEN 2
                    WHEN 'CommunityMod' THEN 3
                    WHEN 'HubMod' THEN 4
                    WHEN 'SpaceMod' THEN 5
                END");

            // STEP 5: Make integer columns non-nullable (except UserDatabaseEntity.RoleId which stays nullable)
            migrationBuilder.AlterColumn<int>(
                name: "VisibilityId",
                table: "Community",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TargetTypeId",
                table: "Follow",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LevelId",
                table: "Follow",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ActionId",
                table: "ModerationLog",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TypeId",
                table: "Notification",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TypeId",
                table: "Reaction",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "StatusId",
                table: "Report",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BanTypeId",
                table: "UserBan",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "UserRole",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // STEP 6: Drop old indexes that reference string columns
            migrationBuilder.DropIndex(
                name: "IX_UserRole_CommunityId_Role_RevokedAt",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_UserRole_HubId_Role_RevokedAt",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_UserRole_SpaceId_Role_RevokedAt",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_UserRole_UserId_RevokedAt",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_Report_Status_CommunityId_CreatedAt",
                table: "Report");

            migrationBuilder.DropIndex(
                name: "IX_Report_Status_HubId_CreatedAt",
                table: "Report");

            migrationBuilder.DropIndex(
                name: "IX_Report_Status_SpaceId_CreatedAt",
                table: "Report");

            migrationBuilder.DropIndex(
                name: "IX_Reaction_PostId_UserId_Type",
                table: "Reaction");

            migrationBuilder.DropIndex(
                name: "IX_Follow_UserId_TargetType_DiscussionId_SpaceId_FollowedUserId",
                table: "Follow");

            // STEP 7: Drop old string columns
            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Community");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Follow");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "ModerationLog");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Reaction");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "BanType",
                table: "UserBan");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "UserRole");

            // STEP 8: Create new indexes on integer columns
            migrationBuilder.CreateIndex(
                name: "IX_Community_VisibilityId",
                table: "Community",
                column: "VisibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_TargetTypeId",
                table: "Follow",
                column: "TargetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_LevelId",
                table: "Follow",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_UserId_TargetTypeId_DiscussionId_SpaceId_FollowedUse~",
                table: "Follow",
                columns: new[] { "UserId", "TargetTypeId", "DiscussionId", "SpaceId", "FollowedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_ActionId",
                table: "ModerationLog",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TypeId",
                table: "Notification",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_TypeId",
                table: "Reaction",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_PostId_UserId_TypeId",
                table: "Reaction",
                columns: new[] { "PostId", "UserId", "TypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status_CommunityId_CreatedAt",
                table: "Report",
                columns: new[] { "StatusId", "CommunityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status_HubId_CreatedAt",
                table: "Report",
                columns: new[] { "StatusId", "HubId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status_SpaceId_CreatedAt",
                table: "Report",
                columns: new[] { "StatusId", "SpaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_BanTypeId",
                table: "UserBan",
                column: "BanTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_User_RoleId",
                table: "User",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserId_RoleId_RevokedAt",
                table: "UserRole",
                columns: new[] { "UserId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_CommunityId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "CommunityId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_HubId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "HubId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_SpaceId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "SpaceId", "RoleId", "RevokedAt" });

            // STEP 9: Add foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_Community_CommunityVisibilityLookup_VisibilityId",
                table: "Community",
                column: "VisibilityId",
                principalTable: "CommunityVisibilityLookup",
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
                name: "FK_Follow_FollowLevelLookup_LevelId",
                table: "Follow",
                column: "LevelId",
                principalTable: "FollowLevelLookup",
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

            migrationBuilder.AddForeignKey(
                name: "FK_User_UserRoleLookup_RoleId",
                table: "User",
                column: "RoleId",
                principalTable: "UserRoleLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRole_UserRoleTypeLookup_RoleId",
                table: "UserRole",
                column: "RoleId",
                principalTable: "UserRoleTypeLookup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys
            migrationBuilder.DropForeignKey(name: "FK_Community_CommunityVisibilityLookup_VisibilityId", table: "Community");
            migrationBuilder.DropForeignKey(name: "FK_Follow_FollowTargetTypeLookup_TargetTypeId", table: "Follow");
            migrationBuilder.DropForeignKey(name: "FK_Follow_FollowLevelLookup_LevelId", table: "Follow");
            migrationBuilder.DropForeignKey(name: "FK_ModerationLog_ModerationActionLookup_ActionId", table: "ModerationLog");
            migrationBuilder.DropForeignKey(name: "FK_Notification_NotificationTypeLookup_TypeId", table: "Notification");
            migrationBuilder.DropForeignKey(name: "FK_Reaction_ReactionTypeLookup_TypeId", table: "Reaction");
            migrationBuilder.DropForeignKey(name: "FK_Report_ReportStatusLookup_StatusId", table: "Report");
            migrationBuilder.DropForeignKey(name: "FK_UserBan_BanTypeLookup_BanTypeId", table: "UserBan");
            migrationBuilder.DropForeignKey(name: "FK_User_UserRoleLookup_RoleId", table: "User");
            migrationBuilder.DropForeignKey(name: "FK_UserRole_UserRoleTypeLookup_RoleId", table: "UserRole");

            // Drop lookup tables
            migrationBuilder.DropTable(name: "CommunityVisibilityLookup");
            migrationBuilder.DropTable(name: "FollowTargetTypeLookup");
            migrationBuilder.DropTable(name: "FollowLevelLookup");
            migrationBuilder.DropTable(name: "ModerationActionLookup");
            migrationBuilder.DropTable(name: "NotificationTypeLookup");
            migrationBuilder.DropTable(name: "ReactionTypeLookup");
            migrationBuilder.DropTable(name: "ReportStatusLookup");
            migrationBuilder.DropTable(name: "BanTypeLookup");
            migrationBuilder.DropTable(name: "UserRoleLookup");
            migrationBuilder.DropTable(name: "UserRoleTypeLookup");

            // Drop indexes
            migrationBuilder.DropIndex(name: "IX_UserRole_CommunityId_Role_RevokedAt", table: "UserRole");
            migrationBuilder.DropIndex(name: "IX_UserRole_HubId_Role_RevokedAt", table: "UserRole");
            migrationBuilder.DropIndex(name: "IX_UserRole_RoleId", table: "UserRole");
            migrationBuilder.DropIndex(name: "IX_UserRole_SpaceId_Role_RevokedAt", table: "UserRole");
            migrationBuilder.DropIndex(name: "IX_UserRole_UserId_RoleId_RevokedAt", table: "UserRole");
            migrationBuilder.DropIndex(name: "IX_UserBan_BanTypeId", table: "UserBan");
            migrationBuilder.DropIndex(name: "IX_User_RoleId", table: "User");
            migrationBuilder.DropIndex(name: "IX_Report_Status_CommunityId_CreatedAt", table: "Report");
            migrationBuilder.DropIndex(name: "IX_Report_Status_HubId_CreatedAt", table: "Report");
            migrationBuilder.DropIndex(name: "IX_Report_Status_SpaceId_CreatedAt", table: "Report");
            migrationBuilder.DropIndex(name: "IX_Reaction_PostId_UserId_TypeId", table: "Reaction");
            migrationBuilder.DropIndex(name: "IX_Reaction_TypeId", table: "Reaction");
            migrationBuilder.DropIndex(name: "IX_Notification_TypeId", table: "Notification");
            migrationBuilder.DropIndex(name: "IX_ModerationLog_ActionId", table: "ModerationLog");
            migrationBuilder.DropIndex(name: "IX_Follow_LevelId", table: "Follow");
            migrationBuilder.DropIndex(name: "IX_Follow_TargetTypeId", table: "Follow");
            migrationBuilder.DropIndex(name: "IX_Follow_UserId_TargetTypeId_DiscussionId_SpaceId_FollowedUse~", table: "Follow");
            migrationBuilder.DropIndex(name: "IX_Community_VisibilityId", table: "Community");

            // Drop integer columns
            migrationBuilder.DropColumn(name: "RoleId", table: "UserRole");
            migrationBuilder.DropColumn(name: "BanTypeId", table: "UserBan");
            migrationBuilder.DropColumn(name: "RoleId", table: "User");
            migrationBuilder.DropColumn(name: "StatusId", table: "Report");
            migrationBuilder.DropColumn(name: "TypeId", table: "Reaction");
            migrationBuilder.DropColumn(name: "TypeId", table: "Notification");
            migrationBuilder.DropColumn(name: "ActionId", table: "ModerationLog");
            migrationBuilder.DropColumn(name: "LevelId", table: "Follow");
            migrationBuilder.DropColumn(name: "TargetTypeId", table: "Follow");
            migrationBuilder.DropColumn(name: "VisibilityId", table: "Community");

            // Add string columns back
            migrationBuilder.AddColumn<string>(name: "Role", table: "UserRole", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "BanType", table: "UserBan", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Role", table: "User", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Status", table: "Report", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Type", table: "Reaction", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Type", table: "Notification", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Action", table: "ModerationLog", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Level", table: "Follow", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "TargetType", table: "Follow", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Visibility", table: "Community", type: "text", nullable: false, defaultValue: "");

            // Create old indexes
            migrationBuilder.CreateIndex(name: "IX_UserRole_CommunityId_Role_RevokedAt", table: "UserRole", columns: new[] { "CommunityId", "Role", "RevokedAt" });
            migrationBuilder.CreateIndex(name: "IX_UserRole_HubId_Role_RevokedAt", table: "UserRole", columns: new[] { "HubId", "Role", "RevokedAt" });
            migrationBuilder.CreateIndex(name: "IX_UserRole_SpaceId_Role_RevokedAt", table: "UserRole", columns: new[] { "SpaceId", "Role", "RevokedAt" });
            migrationBuilder.CreateIndex(name: "IX_UserRole_UserId_RevokedAt", table: "UserRole", columns: new[] { "UserId", "RevokedAt" });
            migrationBuilder.CreateIndex(name: "IX_Report_Status_CommunityId_CreatedAt", table: "Report", columns: new[] { "Status", "CommunityId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_Report_Status_HubId_CreatedAt", table: "Report", columns: new[] { "Status", "HubId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_Report_Status_SpaceId_CreatedAt", table: "Report", columns: new[] { "Status", "SpaceId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_Reaction_PostId_UserId_Type", table: "Reaction", columns: new[] { "PostId", "UserId", "Type" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_Follow_UserId_TargetType_DiscussionId_SpaceId_FollowedUserId", table: "Follow", columns: new[] { "UserId", "TargetType", "DiscussionId", "SpaceId", "FollowedUserId" }, unique: true);
        }
    }
}
