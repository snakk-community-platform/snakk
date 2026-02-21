using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Snakk.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityVisibilityLookup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionReadState",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DiscussionId = table.Column<string>(type: "text", nullable: false),
                    LastReadPostId = table.Column<string>(type: "text", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionReadState", x => new { x.UserId, x.DiscussionId });
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
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsSystemPermission = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
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
                    RequirementConfig = table.Column<string>(type: "text", nullable: false),
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
                name: "Community",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VisibilityId = table.Column<int>(type: "integer", nullable: false),
                    ExposeToPlatformFeed = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HubCount = table.Column<int>(type: "integer", nullable: false),
                    SpaceCount = table.Column<int>(type: "integer", nullable: false),
                    DiscussionCount = table.Column<int>(type: "integer", nullable: false),
                    PostCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Community", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Community_CommunityVisibilityLookup_VisibilityId",
                        column: x => x.VisibilityId,
                        principalTable: "CommunityVisibilityLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    EmailVerificationToken = table.Column<string>(type: "text", nullable: true),
                    OAuthProvider = table.Column<string>(type: "text", nullable: true),
                    OAuthProviderId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    AvatarFileName = table.Column<string>(type: "text", nullable: true),
                    PreferEndlessScroll = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "text", nullable: true),
                    TwoFactorEnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.UniqueConstraint("AK_User_PublicId", x => x.PublicId);
                    table.ForeignKey(
                        name: "FK_User_UserRoleLookup_RoleId",
                        column: x => x.RoleId,
                        principalTable: "UserRoleLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityDomain",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDomain", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityDomain_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllowAnonymousReading = table.Column<bool>(type: "boolean", nullable: false),
                    RequireEmailConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SpaceCount = table.Column<int>(type: "integer", nullable: false),
                    DiscussionCount = table.Column<int>(type: "integer", nullable: false),
                    PostCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hub", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hub_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    TargetType = table.Column<string>(type: "text", nullable: true),
                    TargetId = table.Column<string>(type: "text", nullable: true),
                    TargetDisplayName = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeverityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLog_AuditLogSeverityLookup_SeverityId",
                        column: x => x.SeverityId,
                        principalTable: "AuditLogSeverityLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditLog_User_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BackupCode",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedIp = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupCode_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    TokenValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "text", nullable: true),
                    ReplacedByTokenId = table.Column<int>(type: "integer", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: true),
                    DeviceName = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_RefreshToken_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "RefreshToken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshToken_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSettings_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TemporaryRoleElevations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    GrantedById = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedById = table.Column<int>(type: "integer", nullable: true),
                    RevokedReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryRoleElevations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemporaryRoleElevations_User_GrantedById",
                        column: x => x.GrantedById,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemporaryRoleElevations_User_RevokedById",
                        column: x => x.RevokedById,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TemporaryRoleElevations_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrustedDevice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    TrustedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedIp = table.Column<string>(type: "text", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedDevice_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    ProgressData = table.Column<string>(type: "text", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "UserMetric",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MetricType = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMetric", x => new { x.UserId, x.MetricType, x.Scope, x.ScopeId });
                    table.ForeignKey(
                        name: "FK_UserMetric_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EventTypes = table.Column<string>(type: "text", nullable: false),
                    Secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomHeaders = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Webhooks_User_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "User",
                        principalColumn: "PublicId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Space",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllowAnonymousReading = table.Column<bool>(type: "boolean", nullable: false),
                    RequireEmailConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscussionCount = table.Column<int>(type: "integer", nullable: false),
                    PostCount = table.Column<int>(type: "integer", nullable: false),
                    HubId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Space", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Space_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDeliveryLogs_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Discussion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    PostCount = table.Column<int>(type: "integer", nullable: false),
                    ReactionCount = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discussion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discussion_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Discussion_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportReason",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportReason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportReason_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportReason_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportReason_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportReason_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserBan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BanTypeId = table.Column<int>(type: "integer", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    BannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BannedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UnbannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnbannedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBan_BanTypeLookup_BanTypeId",
                        column: x => x.BanTypeId,
                        principalTable: "BanTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBan_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBan_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBan_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBan_User_BannedByUserId",
                        column: x => x.BannedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBan_User_UnbannedByUserId",
                        column: x => x.UnbannedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserBan_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UserDatabaseEntityId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRole_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_UserRoleTypeLookup_RoleId",
                        column: x => x.RoleId,
                        principalTable: "UserRoleTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRole_User_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_User_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserDatabaseEntityId",
                        column: x => x.UserDatabaseEntityId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Follow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TargetTypeId = table.Column<int>(type: "integer", nullable: false),
                    LevelId = table.Column<int>(type: "integer", nullable: false),
                    DiscussionId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    FollowedUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Follow_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Follow_FollowLevelLookup_LevelId",
                        column: x => x.LevelId,
                        principalTable: "FollowLevelLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Follow_FollowTargetTypeLookup_TargetTypeId",
                        column: x => x.TargetTypeId,
                        principalTable: "FollowTargetTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Follow_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Follow_User_FollowedUserId",
                        column: x => x.FollowedUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Follow_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Post",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsFirstPost = table.Column<bool>(type: "boolean", nullable: false),
                    RevisionCount = table.Column<int>(type: "integer", nullable: false),
                    DiscussionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReplyToPostId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Post", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Post_Discussion_DiscussionId",
                        column: x => x.DiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Post_Post_ReplyToPostId",
                        column: x => x.ReplyToPostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Post_User_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_UserRole_RoleId",
                        column: x => x.RoleId,
                        principalTable: "UserRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_User_GrantedById",
                        column: x => x.GrantedById,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Mention",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    MentionedUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mention", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mention_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mention_User_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    RecipientUserId = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    SourcePostId = table.Column<int>(type: "integer", nullable: true),
                    SourceDiscussionId = table.Column<int>(type: "integer", nullable: true),
                    SourceSpaceId = table.Column<int>(type: "integer", nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_Discussion_SourceDiscussionId",
                        column: x => x.SourceDiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_NotificationTypeLookup_TypeId",
                        column: x => x.TypeId,
                        principalTable: "NotificationTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Post_SourcePostId",
                        column: x => x.SourcePostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_Space_SourceSpaceId",
                        column: x => x.SourceSpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_User_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_User_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PostRevision",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    PostPublicId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedByUserId = table.Column<int>(type: "integer", nullable: false),
                    EditedByUserPublicId = table.Column<string>(type: "text", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostRevision_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostRevision_User_EditedByUserId",
                        column: x => x.EditedByUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reaction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reaction_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reaction_ReactionTypeLookup_TypeId",
                        column: x => x.TypeId,
                        principalTable: "ReactionTypeLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reaction_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Report",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    ReporterUserId = table.Column<int>(type: "integer", nullable: false),
                    ReportedPostId = table.Column<int>(type: "integer", nullable: true),
                    ReportedDiscussionId = table.Column<int>(type: "integer", nullable: true),
                    ReportedUserId = table.Column<int>(type: "integer", nullable: true),
                    ReasonId = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolutionNote = table.Column<string>(type: "text", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    CommunityId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Report", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Report_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Discussion_ReportedDiscussionId",
                        column: x => x.ReportedDiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Post_ReportedPostId",
                        column: x => x.ReportedPostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_ReportReason_ReasonId",
                        column: x => x.ReasonId,
                        principalTable: "ReportReason",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_ReportStatusLookup_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ReportStatusLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Report_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_User_ReportedUserId",
                        column: x => x.ReportedUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_User_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_User_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ModerationLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    ActionId = table.Column<int>(type: "integer", nullable: false),
                    TargetPostId = table.Column<int>(type: "integer", nullable: true),
                    TargetDiscussionId = table.Column<int>(type: "integer", nullable: true),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true),
                    TargetReportId = table.Column<int>(type: "integer", nullable: true),
                    TargetUserRoleId = table.Column<int>(type: "integer", nullable: true),
                    TargetUserBanId = table.Column<int>(type: "integer", nullable: true),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationLog_Community_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Community",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_Discussion_TargetDiscussionId",
                        column: x => x.TargetDiscussionId,
                        principalTable: "Discussion",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_Hub_HubId",
                        column: x => x.HubId,
                        principalTable: "Hub",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_ModerationActionLookup_ActionId",
                        column: x => x.ActionId,
                        principalTable: "ModerationActionLookup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationLog_Post_TargetPostId",
                        column: x => x.TargetPostId,
                        principalTable: "Post",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_Report_TargetReportId",
                        column: x => x.TargetReportId,
                        principalTable: "Report",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_Space_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Space",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_UserBan_TargetUserBanId",
                        column: x => x.TargetUserBanId,
                        principalTable: "UserBan",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_UserRole_TargetUserRoleId",
                        column: x => x.TargetUserRoleId,
                        principalTable: "UserRole",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_User_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ModerationLog_User_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReportComment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    ReportId = table.Column<int>(type: "integer", nullable: false),
                    AuthorUserId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportComment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportComment_Report_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Report",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportComment_User_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "User",
                        principalColumn: "Id");
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
                name: "IX_AuditLog_ActorUserId",
                table: "AuditLog",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_SeverityId",
                table: "AuditLog",
                column: "SeverityId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupCode_PublicId",
                table: "BackupCode",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupCode_UserId",
                table: "BackupCode",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Community_PublicId",
                table: "Community",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Community_Slug",
                table: "Community",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Community_VisibilityId",
                table: "Community",
                column: "VisibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDomain_CommunityId",
                table: "CommunityDomain",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDomain_Domain",
                table: "CommunityDomain",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDomain_PublicId",
                table: "CommunityDomain",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_CreatedAt_IsDeleted_Desc",
                table: "Discussion",
                columns: new[] { "CreatedAt", "IsDeleted" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_CreatedByUserId_CreatedAt_Id_Desc",
                table: "Discussion",
                columns: new[] { "CreatedByUserId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_IsDeleted",
                table: "Discussion",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_LastActivityAt_Id_Desc",
                table: "Discussion",
                columns: new[] { "LastActivityAt", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_PublicId",
                table: "Discussion",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_Slug",
                table: "Discussion",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_SpaceId_Pinned_LastActivityAt_Id",
                table: "Discussion",
                columns: new[] { "SpaceId", "IsPinned", "LastActivityAt", "Id" },
                descending: new[] { false, true, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Follow_DiscussionId",
                table: "Follow",
                column: "DiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_FollowedUserId",
                table: "Follow",
                column: "FollowedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_LevelId",
                table: "Follow",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_PublicId",
                table: "Follow",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Follow_SpaceId",
                table: "Follow",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_TargetTypeId",
                table: "Follow",
                column: "TargetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_UserId_TargetTypeId_DiscussionId_SpaceId_FollowedUse~",
                table: "Follow",
                columns: new[] { "UserId", "TargetTypeId", "DiscussionId", "SpaceId", "FollowedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hub_CommunityId",
                table: "Hub",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Hub_PublicId",
                table: "Hub",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hub_Slug",
                table: "Hub",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_Mention_MentionedUserId",
                table: "Mention",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Mention_PostId_MentionedUserId",
                table: "Mention",
                columns: new[] { "PostId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mention_PublicId",
                table: "Mention",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_ActionId",
                table: "ModerationLog",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_ActorUserId_CreatedAt_Desc",
                table: "ModerationLog",
                columns: new[] { "ActorUserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_CommunityId_CreatedAt_Desc",
                table: "ModerationLog",
                columns: new[] { "CommunityId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_HubId",
                table: "ModerationLog",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_PublicId",
                table: "ModerationLog",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_SpaceId",
                table: "ModerationLog",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_TargetDiscussionId",
                table: "ModerationLog",
                column: "TargetDiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_TargetPostId",
                table: "ModerationLog",
                column: "TargetPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_TargetReportId",
                table: "ModerationLog",
                column: "TargetReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_TargetUserBanId",
                table: "ModerationLog",
                column: "TargetUserBanId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_TargetUserId",
                table: "ModerationLog",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationLog_TargetUserRoleId",
                table: "ModerationLog",
                column: "TargetUserRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_ActorUserId",
                table: "Notification",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_PublicId",
                table: "Notification",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientUserId_IsRead_CreatedAt",
                table: "Notification",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_SourceDiscussionId",
                table: "Notification",
                column: "SourceDiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_SourcePostId",
                table: "Notification",
                column: "SourcePostId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_SourceSpaceId",
                table: "Notification",
                column: "SourceSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_TypeId",
                table: "Notification",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Permission_Category",
                table: "Permissions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PublicId",
                table: "Permissions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatedAt_DiscussionId_IsDeleted",
                table: "Post",
                columns: new[] { "CreatedAt", "DiscussionId", "IsDeleted" },
                descending: new[] { true, false, false });

            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatedByUserId_CreatedAt_Id_Desc",
                table: "Post",
                columns: new[] { "CreatedByUserId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Post_DiscussionId_CreatedAt_Id",
                table: "Post",
                columns: new[] { "DiscussionId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Post_IsDeleted",
                table: "Post",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Post_ReplyToPostId",
                table: "Post",
                column: "ReplyToPostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostRevision_EditedByUserId",
                table: "PostRevision",
                column: "EditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostRevision_PostId",
                table: "PostRevision",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_PostId_UserId_TypeId",
                table: "Reaction",
                columns: new[] { "PostId", "UserId", "TypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_PublicId",
                table: "Reaction",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_TypeId",
                table: "Reaction",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_UserId",
                table: "Reaction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_ReplacedByTokenId",
                table: "RefreshToken",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenValue",
                table: "RefreshToken",
                column: "TokenValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_CommunityId",
                table: "Report",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_HubId",
                table: "Report",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_PublicId",
                table: "Report",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Report_ReasonId",
                table: "Report",
                column: "ReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ReportedDiscussionId",
                table: "Report",
                column: "ReportedDiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ReportedPostId",
                table: "Report",
                column: "ReportedPostId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ReportedUserId",
                table: "Report",
                column: "ReportedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ReporterUserId",
                table: "Report",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ResolvedByUserId",
                table: "Report",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_SpaceId",
                table: "Report",
                column: "SpaceId");

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
                name: "IX_ReportComment_AuthorUserId",
                table: "ReportComment",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportComment_PublicId",
                table: "ReportComment",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportComment_ReportId",
                table: "ReportComment",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportReason_CommunityId",
                table: "ReportReason",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportReason_CreatedByUserId",
                table: "ReportReason",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportReason_HubId",
                table: "ReportReason",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportReason_PublicId",
                table: "ReportReason",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportReason_SpaceId",
                table: "ReportReason",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_GrantedById",
                table: "RolePermissions",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Space_HubId",
                table: "Space",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Space_PublicId",
                table: "Space",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Space_Slug",
                table: "Space",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Category",
                table: "SystemSettings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Category_Key",
                table: "SystemSettings",
                columns: new[] { "Category", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_PublicId",
                table: "SystemSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_UpdatedById",
                table: "SystemSettings",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryRoleElevation_ExpiresAt",
                table: "TemporaryRoleElevations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryRoleElevation_UserId",
                table: "TemporaryRoleElevations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryRoleElevation_UserId_ExpiresAt_Active",
                table: "TemporaryRoleElevations",
                columns: new[] { "UserId", "ExpiresAt" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryRoleElevations_GrantedById",
                table: "TemporaryRoleElevations",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryRoleElevations_PublicId",
                table: "TemporaryRoleElevations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryRoleElevations_RevokedById",
                table: "TemporaryRoleElevations",
                column: "RevokedById");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevice_PublicId",
                table: "TrustedDevice",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevice_UserId_DeviceFingerprint",
                table: "TrustedDevice",
                columns: new[] { "UserId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_User_DisplayName",
                table: "User",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_IsDeleted",
                table: "User",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_User_PublicId",
                table: "User",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_RoleId",
                table: "User",
                column: "RoleId");

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

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_BannedByUserId",
                table: "UserBan",
                column: "BannedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_BanTypeId",
                table: "UserBan",
                column: "BanTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_CommunityId",
                table: "UserBan",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_HubId",
                table: "UserBan",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_PublicId",
                table: "UserBan",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_SpaceId",
                table: "UserBan",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_UnbannedByUserId",
                table: "UserBan",
                column: "UnbannedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBan_UserId_UnbannedAt_ExpiresAt",
                table: "UserBan",
                columns: new[] { "UserId", "UnbannedAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMetric_LastUpdated",
                table: "UserMetric",
                column: "LastUpdated");

            migrationBuilder.CreateIndex(
                name: "IX_UserMetric_UserId_Scope_ScopeId",
                table: "UserMetric",
                columns: new[] { "UserId", "Scope", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_AssignedByUserId",
                table: "UserRole",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_CommunityId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "CommunityId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_HubId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "HubId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_PublicId",
                table: "UserRole",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RevokedByUserId",
                table: "UserRole",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_SpaceId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "SpaceId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserDatabaseEntityId",
                table: "UserRole",
                column: "UserDatabaseEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserId_RoleId_RevokedAt",
                table: "UserRole",
                columns: new[] { "UserId", "RoleId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLog_EventType",
                table: "WebhookDeliveryLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLog_IsSuccess_NextRetryAt",
                table: "WebhookDeliveryLogs",
                columns: new[] { "IsSuccess", "NextRetryAt" },
                filter: "\"NextRetryAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLog_WebhookId",
                table: "WebhookDeliveryLogs",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLog_WebhookId_CreatedAt_Desc",
                table: "WebhookDeliveryLogs",
                columns: new[] { "WebhookId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Webhook_CreatedAt_Desc",
                table: "Webhooks",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Webhook_IsActive",
                table: "Webhooks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_CreatedBy",
                table: "Webhooks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_Id",
                table: "Webhooks",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "BackupCode");

            migrationBuilder.DropTable(
                name: "CommunityDomain");

            migrationBuilder.DropTable(
                name: "DiscussionReadState");

            migrationBuilder.DropTable(
                name: "Follow");

            migrationBuilder.DropTable(
                name: "Mention");

            migrationBuilder.DropTable(
                name: "ModerationLog");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "PostRevision");

            migrationBuilder.DropTable(
                name: "Reaction");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropTable(
                name: "ReportComment");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TemporaryRoleElevations");

            migrationBuilder.DropTable(
                name: "TrustedDevice");

            migrationBuilder.DropTable(
                name: "UserAchievement");

            migrationBuilder.DropTable(
                name: "UserAchievementProgress");

            migrationBuilder.DropTable(
                name: "UserMetric");

            migrationBuilder.DropTable(
                name: "WebhookDeliveryLogs");

            migrationBuilder.DropTable(
                name: "AuditLogSeverityLookup");

            migrationBuilder.DropTable(
                name: "FollowLevelLookup");

            migrationBuilder.DropTable(
                name: "FollowTargetTypeLookup");

            migrationBuilder.DropTable(
                name: "ModerationActionLookup");

            migrationBuilder.DropTable(
                name: "UserBan");

            migrationBuilder.DropTable(
                name: "NotificationTypeLookup");

            migrationBuilder.DropTable(
                name: "ReactionTypeLookup");

            migrationBuilder.DropTable(
                name: "Report");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "Achievement");

            migrationBuilder.DropTable(
                name: "Webhooks");

            migrationBuilder.DropTable(
                name: "BanTypeLookup");

            migrationBuilder.DropTable(
                name: "Post");

            migrationBuilder.DropTable(
                name: "ReportReason");

            migrationBuilder.DropTable(
                name: "ReportStatusLookup");

            migrationBuilder.DropTable(
                name: "UserRoleTypeLookup");

            migrationBuilder.DropTable(
                name: "AchievementCategoryLookup");

            migrationBuilder.DropTable(
                name: "AchievementRequirementTypeLookup");

            migrationBuilder.DropTable(
                name: "Discussion");

            migrationBuilder.DropTable(
                name: "Space");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Hub");

            migrationBuilder.DropTable(
                name: "UserRoleLookup");

            migrationBuilder.DropTable(
                name: "Community");

            migrationBuilder.DropTable(
                name: "CommunityVisibilityLookup");
        }
    }
}
