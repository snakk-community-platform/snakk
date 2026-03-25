using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenameGroupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename tables
            migrationBuilder.RenameTable(
                name: "CommunityGroup",
                newName: "Groups");

            migrationBuilder.RenameTable(
                name: "CommunityGroupMember",
                newName: "GroupMembers");

            migrationBuilder.RenameTable(
                name: "EntityGroupAccess",
                newName: "GroupAccess");

            // Rename primary key constraints
            migrationBuilder.Sql("ALTER TABLE \"Groups\" RENAME CONSTRAINT \"PK_CommunityGroup\" TO \"PK_Groups\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupMembers\" RENAME CONSTRAINT \"PK_CommunityGroupMember\" TO \"PK_GroupMembers\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupAccess\" RENAME CONSTRAINT \"PK_EntityGroupAccess\" TO \"PK_GroupAccess\";");

            // Rename foreign key constraints on Groups
            migrationBuilder.Sql("ALTER TABLE \"Groups\" RENAME CONSTRAINT \"FK_CommunityGroup_Community_CommunityId\" TO \"FK_Groups_Community_CommunityId\";");

            // Rename foreign key constraints on GroupMembers
            migrationBuilder.Sql("ALTER TABLE \"GroupMembers\" RENAME CONSTRAINT \"FK_CommunityGroupMember_CommunityGroup_GroupId\" TO \"FK_GroupMembers_Groups_GroupId\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupMembers\" RENAME CONSTRAINT \"FK_CommunityGroupMember_User_AddedByUserId\" TO \"FK_GroupMembers_User_AddedByUserId\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupMembers\" RENAME CONSTRAINT \"FK_CommunityGroupMember_User_UserId\" TO \"FK_GroupMembers_User_UserId\";");

            // Rename foreign key constraints on GroupAccess
            migrationBuilder.Sql("ALTER TABLE \"GroupAccess\" RENAME CONSTRAINT \"FK_EntityGroupAccess_CommunityGroup_GroupId\" TO \"FK_GroupAccess_Groups_GroupId\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupAccess\" RENAME CONSTRAINT \"FK_EntityGroupAccess_Community_CommunityId\" TO \"FK_GroupAccess_Community_CommunityId\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupAccess\" RENAME CONSTRAINT \"FK_EntityGroupAccess_Hub_HubId\" TO \"FK_GroupAccess_Hub_HubId\";");
            migrationBuilder.Sql("ALTER TABLE \"GroupAccess\" RENAME CONSTRAINT \"FK_EntityGroupAccess_Space_SpaceId\" TO \"FK_GroupAccess_Space_SpaceId\";");

            // Rename indexes on Groups
            migrationBuilder.RenameIndex(name: "IX_CommunityGroup_PublicId", table: "Groups", newName: "IX_Groups_PublicId");
            migrationBuilder.RenameIndex(name: "IX_CommunityGroup_CommunityId_Slug", table: "Groups", newName: "IX_Groups_CommunityId_Slug");
            migrationBuilder.RenameIndex(name: "IX_CommunityGroup_CommunityId_IsPublic", table: "Groups", newName: "IX_Groups_CommunityId_IsPublic");

            // Rename indexes on GroupMembers
            migrationBuilder.RenameIndex(name: "IX_CommunityGroupMember_GroupId_UserId", table: "GroupMembers", newName: "IX_GroupMembers_GroupId_UserId");
            migrationBuilder.RenameIndex(name: "IX_CommunityGroupMember_UserId_GroupId", table: "GroupMembers", newName: "IX_GroupMembers_UserId_GroupId");
            migrationBuilder.RenameIndex(name: "IX_CommunityGroupMember_AddedByUserId", table: "GroupMembers", newName: "IX_GroupMembers_AddedByUserId");

            // Rename indexes on GroupAccess
            migrationBuilder.RenameIndex(name: "IX_EntityGroupAccess_GroupId_CommunityId", table: "GroupAccess", newName: "IX_GroupAccess_GroupId_CommunityId");
            migrationBuilder.RenameIndex(name: "IX_EntityGroupAccess_GroupId_HubId", table: "GroupAccess", newName: "IX_GroupAccess_GroupId_HubId");
            migrationBuilder.RenameIndex(name: "IX_EntityGroupAccess_GroupId_SpaceId", table: "GroupAccess", newName: "IX_GroupAccess_GroupId_SpaceId");
            migrationBuilder.RenameIndex(name: "IX_EntityGroupAccess_CommunityId", table: "GroupAccess", newName: "IX_GroupAccess_CommunityId");
            migrationBuilder.RenameIndex(name: "IX_EntityGroupAccess_HubId", table: "GroupAccess", newName: "IX_GroupAccess_HubId");
            migrationBuilder.RenameIndex(name: "IX_EntityGroupAccess_SpaceId", table: "GroupAccess", newName: "IX_GroupAccess_SpaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Groups", newName: "CommunityGroup");
            migrationBuilder.RenameTable(name: "GroupMembers", newName: "CommunityGroupMember");
            migrationBuilder.RenameTable(name: "GroupAccess", newName: "EntityGroupAccess");

            migrationBuilder.Sql("ALTER TABLE \"CommunityGroup\" RENAME CONSTRAINT \"PK_Groups\" TO \"PK_CommunityGroup\";");
            migrationBuilder.Sql("ALTER TABLE \"CommunityGroupMember\" RENAME CONSTRAINT \"PK_GroupMembers\" TO \"PK_CommunityGroupMember\";");
            migrationBuilder.Sql("ALTER TABLE \"EntityGroupAccess\" RENAME CONSTRAINT \"PK_GroupAccess\" TO \"PK_EntityGroupAccess\";");

            migrationBuilder.Sql("ALTER TABLE \"CommunityGroup\" RENAME CONSTRAINT \"FK_Groups_Community_CommunityId\" TO \"FK_CommunityGroup_Community_CommunityId\";");

            migrationBuilder.Sql("ALTER TABLE \"CommunityGroupMember\" RENAME CONSTRAINT \"FK_GroupMembers_Groups_GroupId\" TO \"FK_CommunityGroupMember_CommunityGroup_GroupId\";");
            migrationBuilder.Sql("ALTER TABLE \"CommunityGroupMember\" RENAME CONSTRAINT \"FK_GroupMembers_User_AddedByUserId\" TO \"FK_CommunityGroupMember_User_AddedByUserId\";");
            migrationBuilder.Sql("ALTER TABLE \"CommunityGroupMember\" RENAME CONSTRAINT \"FK_GroupMembers_User_UserId\" TO \"FK_CommunityGroupMember_User_UserId\";");

            migrationBuilder.Sql("ALTER TABLE \"EntityGroupAccess\" RENAME CONSTRAINT \"FK_GroupAccess_Groups_GroupId\" TO \"FK_EntityGroupAccess_CommunityGroup_GroupId\";");
            migrationBuilder.Sql("ALTER TABLE \"EntityGroupAccess\" RENAME CONSTRAINT \"FK_GroupAccess_Community_CommunityId\" TO \"FK_EntityGroupAccess_Community_CommunityId\";");
            migrationBuilder.Sql("ALTER TABLE \"EntityGroupAccess\" RENAME CONSTRAINT \"FK_GroupAccess_Hub_HubId\" TO \"FK_EntityGroupAccess_Hub_HubId\";");
            migrationBuilder.Sql("ALTER TABLE \"EntityGroupAccess\" RENAME CONSTRAINT \"FK_GroupAccess_Space_SpaceId\" TO \"FK_EntityGroupAccess_Space_SpaceId\";");

            migrationBuilder.RenameIndex(name: "IX_Groups_PublicId", table: "CommunityGroup", newName: "IX_CommunityGroup_PublicId");
            migrationBuilder.RenameIndex(name: "IX_Groups_CommunityId_Slug", table: "CommunityGroup", newName: "IX_CommunityGroup_CommunityId_Slug");
            migrationBuilder.RenameIndex(name: "IX_Groups_CommunityId_IsPublic", table: "CommunityGroup", newName: "IX_CommunityGroup_CommunityId_IsPublic");

            migrationBuilder.RenameIndex(name: "IX_GroupMembers_GroupId_UserId", table: "CommunityGroupMember", newName: "IX_CommunityGroupMember_GroupId_UserId");
            migrationBuilder.RenameIndex(name: "IX_GroupMembers_UserId_GroupId", table: "CommunityGroupMember", newName: "IX_CommunityGroupMember_UserId_GroupId");
            migrationBuilder.RenameIndex(name: "IX_GroupMembers_AddedByUserId", table: "CommunityGroupMember", newName: "IX_CommunityGroupMember_AddedByUserId");

            migrationBuilder.RenameIndex(name: "IX_GroupAccess_GroupId_CommunityId", table: "EntityGroupAccess", newName: "IX_EntityGroupAccess_GroupId_CommunityId");
            migrationBuilder.RenameIndex(name: "IX_GroupAccess_GroupId_HubId", table: "EntityGroupAccess", newName: "IX_EntityGroupAccess_GroupId_HubId");
            migrationBuilder.RenameIndex(name: "IX_GroupAccess_GroupId_SpaceId", table: "EntityGroupAccess", newName: "IX_EntityGroupAccess_GroupId_SpaceId");
            migrationBuilder.RenameIndex(name: "IX_GroupAccess_CommunityId", table: "EntityGroupAccess", newName: "IX_EntityGroupAccess_CommunityId");
            migrationBuilder.RenameIndex(name: "IX_GroupAccess_HubId", table: "EntityGroupAccess", newName: "IX_EntityGroupAccess_HubId");
            migrationBuilder.RenameIndex(name: "IX_GroupAccess_SpaceId", table: "EntityGroupAccess", newName: "IX_EntityGroupAccess_SpaceId");
        }
    }
}
