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
                    Visibility = table.Column<string>(type: "text", nullable: false),
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
                    Role = table.Column<string>(type: "text", nullable: true),
                    AvatarFileName = table.Column<string>(type: "text", nullable: true),
                    PreferEndlessScroll = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
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
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    PostCount = table.Column<int>(type: "integer", nullable: false),
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
                    BanType = table.Column<string>(type: "text", nullable: false),
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
                    Role = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<int>(type: "integer", nullable: true),
                    HubId = table.Column<int>(type: "integer", nullable: true),
                    SpaceId = table.Column<int>(type: "integer", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "integer", nullable: true)
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
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
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
                    Type = table.Column<string>(type: "text", nullable: false),
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
                    Type = table.Column<string>(type: "text", nullable: false),
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
                    Status = table.Column<string>(type: "text", nullable: false),
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
                    Action = table.Column<string>(type: "text", nullable: false),
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
                name: "IX_Follow_PublicId",
                table: "Follow",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Follow_SpaceId",
                table: "Follow",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_UserId_TargetType_DiscussionId_SpaceId_FollowedUserId",
                table: "Follow",
                columns: new[] { "UserId", "TargetType", "DiscussionId", "SpaceId", "FollowedUserId" },
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
                name: "IX_Reaction_PostId_UserId_Type",
                table: "Reaction",
                columns: new[] { "PostId", "UserId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_PublicId",
                table: "Reaction",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reaction_UserId",
                table: "Reaction",
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
                columns: new[] { "Status", "CommunityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status_HubId_CreatedAt",
                table: "Report",
                columns: new[] { "Status", "HubId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status_SpaceId_CreatedAt",
                table: "Report",
                columns: new[] { "Status", "SpaceId", "CreatedAt" });

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
                name: "IX_UserBan_BannedByUserId",
                table: "UserBan",
                column: "BannedByUserId");

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
                name: "IX_UserRole_AssignedByUserId",
                table: "UserRole",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_CommunityId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "CommunityId", "Role", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_HubId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "HubId", "Role", "RevokedAt" });

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
                name: "IX_UserRole_SpaceId_Role_RevokedAt",
                table: "UserRole",
                columns: new[] { "SpaceId", "Role", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_UserId_RevokedAt",
                table: "UserRole",
                columns: new[] { "UserId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "ReportComment");

            migrationBuilder.DropTable(
                name: "UserBan");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "Report");

            migrationBuilder.DropTable(
                name: "Post");

            migrationBuilder.DropTable(
                name: "ReportReason");

            migrationBuilder.DropTable(
                name: "Discussion");

            migrationBuilder.DropTable(
                name: "Space");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Hub");

            migrationBuilder.DropTable(
                name: "Community");
        }
    }
}
