namespace Snakk.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;

public class SnakkDbContext(DbContextOptions<SnakkDbContext> options) : DbContext(options)
{
    // Community layer
    public DbSet<CommunityDatabaseEntity> Communities { get; set; } = null!;
    public DbSet<CommunityDomainDatabaseEntity> CommunityDomains { get; set; } = null!;
    public DbSet<GroupDatabaseEntity> Groups { get; set; } = null!;
    public DbSet<GroupMemberDatabaseEntity> GroupMembers { get; set; } = null!;
    public DbSet<GroupAccessDatabaseEntity> GroupAccess { get; set; } = null!;

    // Core entities
    public DbSet<HubDatabaseEntity> Hubs { get; set; } = null!;
    public DbSet<SpaceDatabaseEntity> Spaces { get; set; } = null!;
    public DbSet<RuleDatabaseEntity> Rules { get; set; } = null!;
    public DbSet<DiscussionDatabaseEntity> Discussions { get; set; } = null!;
    public DbSet<PostDatabaseEntity> Posts { get; set; } = null!;
    public DbSet<PostRevisionDatabaseEntity> PostRevisions { get; set; } = null!;
    public DbSet<UserDatabaseEntity> Users { get; set; } = null!;
    public DbSet<DiscussionReadStateDatabaseEntity> DiscussionReadStates { get; set; } = null!;

    // Social features
    public DbSet<PostReactionDatabaseEntity> PostReactions { get; set; } = null!;
    public DbSet<UserNotificationDatabaseEntity> UserNotifications { get; set; } = null!;
    public DbSet<UserFollowDatabaseEntity> UserFollows { get; set; } = null!;
    public DbSet<PostMentionDatabaseEntity> PostMentions { get; set; } = null!;

    // Moderation
    public DbSet<UserRoleDatabaseEntity> UserRoles { get; set; } = null!;
    public DbSet<UserBanDatabaseEntity> UserBans { get; set; } = null!;
    public DbSet<ReportDatabaseEntity> Reports { get; set; } = null!;
    public DbSet<ReportCommentDatabaseEntity> ReportComments { get; set; } = null!;
    public DbSet<ReportReasonDatabaseEntity> ReportReasons { get; set; } = null!;
    public DbSet<ModerationLogDatabaseEntity> ModerationLogs { get; set; } = null!;
    public DbSet<AuditLogDatabaseEntity> AuditLogs { get; set; } = null!;
    public DbSet<UserDisplayNameHistoryDatabaseEntity> UserDisplayNameHistories { get; set; } = null!;

    // Permissions
    public DbSet<PermissionDatabaseEntity> Permissions { get; set; } = null!;
    public DbSet<RolePermissionDatabaseEntity> RolePermissions { get; set; } = null!;
    public DbSet<TemporaryRoleElevationDatabaseEntity> TemporaryRoleElevations { get; set; } = null!;

    // Authentication & 2FA
    public DbSet<RefreshTokenDatabaseEntity> RefreshTokens { get; set; } = null!;
    public DbSet<TwoFactorTrustedDeviceDatabaseEntity> TwoFactorTrustedDevices { get; set; } = null!;
    public DbSet<TwoFactorBackupCodeDatabaseEntity> TwoFactorBackupCodes { get; set; } = null!;
    public DbSet<PasswordResetTokenDatabaseEntity> PasswordResetTokens { get; set; } = null!;
    public DbSet<PasswordResetRequestDatabaseEntity> PasswordResetRequests { get; set; } = null!;

    // Settings
    public DbSet<SystemSettingDatabaseEntity> SystemSettings { get; set; } = null!;

    // Integrations
    public DbSet<WebhookDatabaseEntity> Webhooks { get; set; } = null!;
    public DbSet<WebhookDeliveryLogDatabaseEntity> WebhookDeliveryLogs { get; set; } = null!;

    // Achievements
    public DbSet<AchievementDatabaseEntity> Achievements { get; set; } = null!;
    public DbSet<UserAchievementDatabaseEntity> UserAchievements { get; set; } = null!;
    public DbSet<UserAchievementProgressDatabaseEntity> UserAchievementProgress { get; set; } = null!;
    public DbSet<UserMetricDatabaseEntity> UserMetrics { get; set; } = null!;

    // Images
    public DbSet<ImageDatabaseEntity> Images { get; set; } = null!;
    public DbSet<PostImageDatabaseEntity> PostImages { get; set; } = null!;

    // Banners
    public DbSet<BannerDatabaseEntity> Banners { get; set; } = null!;

    // Consent
    public DbSet<ConsentTypeDatabaseEntity> ConsentTypes { get; set; } = null!;
    public DbSet<ConsentTypeVersionDatabaseEntity> ConsentTypeVersions { get; set; } = null!;
    public DbSet<UserConsentDatabaseEntity> UserConsents { get; set; } = null!;

    // Discussion types — allowed type permissions
    public DbSet<CommunityAllowedDiscussionTypeDatabaseEntity> CommunityAllowedDiscussionTypes { get; set; } = null!;
    public DbSet<HubAllowedDiscussionTypeDatabaseEntity> HubAllowedDiscussionTypes { get; set; } = null!;
    public DbSet<SpaceAllowedDiscussionTypeDatabaseEntity> SpaceAllowedDiscussionTypes { get; set; } = null!;

    // Discussion types — extension data
    public DbSet<DiscussionTypeLinkDatabaseEntity> DiscussionLinks { get; set; } = null!;
    public DbSet<DiscussionTypeImageDatabaseEntity> DiscussionImages { get; set; } = null!;
    public DbSet<DiscussionTypeImageAttachmentDatabaseEntity> DiscussionImageAttachments { get; set; } = null!;
    public DbSet<DiscussionTypePollDatabaseEntity> DiscussionPolls { get; set; } = null!;
    public DbSet<DiscussionTypePollOptionDatabaseEntity> DiscussionPollOptions { get; set; } = null!;
    public DbSet<DiscussionTypePollVoteDatabaseEntity> DiscussionPollVotes { get; set; } = null!;
    public DbSet<DiscussionTypeQuestionDatabaseEntity> DiscussionQuestions { get; set; } = null!;
    public DbSet<DiscussionTypeGuideDatabaseEntity> DiscussionGuides { get; set; } = null!;
    public DbSet<DiscussionTypeDebateDatabaseEntity> DiscussionDebates { get; set; } = null!;
    public DbSet<DiscussionTypeDebatePositionDatabaseEntity> DiscussionDebatePositions { get; set; } = null!;
    public DbSet<DiscussionTypeDebatePostPositionDatabaseEntity> DiscussionDebatePostPositions { get; set; } = null!;
    public DbSet<DiscussionTypeJournalDatabaseEntity> DiscussionJournals { get; set; } = null!;
    public DbSet<DiscussionTypeJournalEntryPostDatabaseEntity> DiscussionJournalEntryPosts { get; set; } = null!;
    public DbSet<DiscussionTypeIamaDatabaseEntity> DiscussionIamas { get; set; } = null!;
    public DbSet<DiscussionTypeIamaOfficialAnswerDatabaseEntity> DiscussionIamaOfficialAnswers { get; set; } = null!;
    public DbSet<DiscussionTypeIamaBestQuestionDatabaseEntity> DiscussionIamaBestQuestions { get; set; } = null!;

    // Note: Lookup tables removed - now using enums directly (see Snakk.Shared.Enums)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filters for soft delete
        modelBuilder.Entity<CommunityDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<HubDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SpaceDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DiscussionDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PostDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UserDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);

        // === Community Configuration ===

        // Configure Community -> Hubs relationship
        modelBuilder.Entity<HubDatabaseEntity>()
            .HasOne(h => h.Community)
            .WithMany(c => c.Hubs)
            .HasForeignKey(h => h.CommunityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Community -> Domains relationship
        modelBuilder.Entity<CommunityDomainDatabaseEntity>()
            .HasOne(d => d.Community)
            .WithMany(c => c.Domains)
            .HasForeignKey(d => d.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Groups ===

        modelBuilder.Entity<GroupDatabaseEntity>(entity =>
        {
            entity.Property(g => g.Name).HasMaxLength(100).IsRequired();
            entity.Property(g => g.Slug).HasMaxLength(100).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(500);

            entity.HasOne(g => g.Community)
                .WithMany(c => c.Groups)
                .HasForeignKey(g => g.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(g => g.PublicId).IsUnique();
            entity.HasIndex(g => new { g.CommunityId, g.Slug }).IsUnique();
            entity.HasIndex(g => new { g.CommunityId, g.IsPublic });
        });

        modelBuilder.Entity<GroupMemberDatabaseEntity>(entity =>
        {
            entity.HasOne(m => m.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.AddedByUser)
                .WithMany()
                .HasForeignKey(m => m.AddedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
            entity.HasIndex(m => new { m.UserId, m.GroupId });
        });

        // === Group Access ===

        modelBuilder.Entity<GroupAccessDatabaseEntity>(entity =>
        {
            entity.HasOne(a => a.Group)
                .WithMany()
                .HasForeignKey(a => a.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Community)
                .WithMany(c => c.GroupAccess)
                .HasForeignKey(a => a.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Hub)
                .WithMany(h => h.GroupAccess)
                .HasForeignKey(a => a.HubId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Space)
                .WithMany(s => s.GroupAccess)
                .HasForeignKey(a => a.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            // A group can only have one grant per scoped entity
            entity.HasIndex(a => new { a.GroupId, a.CommunityId }).IsUnique().HasFilter("\"CommunityId\" IS NOT NULL AND \"HubId\" IS NULL AND \"SpaceId\" IS NULL");
            entity.HasIndex(a => new { a.GroupId, a.HubId }).IsUnique().HasFilter("\"HubId\" IS NOT NULL AND \"SpaceId\" IS NULL");
            entity.HasIndex(a => new { a.GroupId, a.SpaceId }).IsUnique().HasFilter("\"SpaceId\" IS NOT NULL");

            entity.HasIndex(a => a.CommunityId);
            entity.HasIndex(a => a.HubId);
            entity.HasIndex(a => a.SpaceId);
        });

        // Language code max length constraints
        modelBuilder.Entity<CommunityDatabaseEntity>()
            .Property(e => e.LanguageCode).HasMaxLength(10);

        modelBuilder.Entity<HubDatabaseEntity>(entity =>
        {
            entity.Property(e => e.LanguageCode).HasMaxLength(10);
            entity.Property(e => e.CommunityLanguageCode).HasMaxLength(10);
        });

        modelBuilder.Entity<SpaceDatabaseEntity>(entity =>
        {
            entity.Property(e => e.LanguageCode).HasMaxLength(10);
            entity.Property(e => e.HubLanguageCode).HasMaxLength(10);
            entity.Property(e => e.CommunityLanguageCode).HasMaxLength(10);
        });

        // Community unique indexes
        modelBuilder.Entity<CommunityDatabaseEntity>()
            .HasIndex(c => c.PublicId)
            .IsUnique();

        modelBuilder.Entity<CommunityDatabaseEntity>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        // CommunityDomain unique indexes
        modelBuilder.Entity<CommunityDomainDatabaseEntity>()
            .HasIndex(d => d.PublicId)
            .IsUnique();

        modelBuilder.Entity<CommunityDomainDatabaseEntity>()
            .HasIndex(d => d.Domain)
            .IsUnique();

        // === Lookup Table Relationships ===
        // NOTE: Lookup tables now use enums instead of navigation properties
        // Foreign keys are still present for data integrity but no navigation properties

        // Configure Hub -> Spaces relationship
        modelBuilder.Entity<SpaceDatabaseEntity>()
            .HasOne(s => s.Hub)
            .WithMany(h => h.Spaces)
            .HasForeignKey(s => s.HubId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure unified Rule entity (nullable scope columns)
        modelBuilder.Entity<RuleDatabaseEntity>(entity =>
        {
            entity.Property(r => r.Title).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(500).IsRequired();

            entity.HasOne(r => r.Community)
                .WithMany(c => c.Rules)
                .HasForeignKey(r => r.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Hub)
                .WithMany(h => h.Rules)
                .HasForeignKey(r => r.HubId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Space)
                .WithMany(s => s.Rules)
                .HasForeignKey(r => r.SpaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => r.CommunityId);
            entity.HasIndex(r => r.HubId);
            entity.HasIndex(r => r.SpaceId);
        });

        // Configure Space -> Discussions relationship
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasOne(d => d.Space)
            .WithMany(s => s.Discussions)
            .HasForeignKey(d => d.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Discussion -> Posts relationship
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasOne(p => p.Discussion)
            .WithMany(d => d.Posts)
            .HasForeignKey(p => p.DiscussionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Post -> User (CreatedBy) relationship
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Discussion -> User (CreatedBy) relationship
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasOne(d => d.CreatedByUser)
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure PostRevision -> Post relationship
        modelBuilder.Entity<PostRevisionDatabaseEntity>()
            .HasOne(pr => pr.Post)
            .WithMany()
            .HasForeignKey(pr => pr.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure PostRevision -> User (EditedBy) relationship
        modelBuilder.Entity<PostRevisionDatabaseEntity>()
            .HasOne(pr => pr.EditedByUser)
            .WithMany()
            .HasForeignKey(pr => pr.EditedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure PublicId unique constraints
        modelBuilder.Entity<HubDatabaseEntity>()
            .HasIndex(h => h.PublicId)
            .IsUnique();

        modelBuilder.Entity<SpaceDatabaseEntity>()
            .HasIndex(s => s.PublicId)
            .IsUnique();

        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => d.PublicId)
            .IsUnique();

        // Configure Slug indexes for lookups (scoped to parent entity)
        modelBuilder.Entity<HubDatabaseEntity>()
            .HasIndex(h => new { h.CommunityId, h.Slug })
            .IsUnique();

        modelBuilder.Entity<SpaceDatabaseEntity>()
            .HasIndex(s => new { s.HubId, s.Slug })
            .IsUnique();

        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => d.Slug);

        // === Composite Indexes for Keyset Pagination ===

        // Discussion feed: global feed sorted by LastActivityAt (keyset pagination)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.LastActivityAt, d.Id })
            .IsDescending(true, true)
            .HasDatabaseName("IX_Discussion_LastActivityAt_Id_Desc");

        // Discussion feed: space-scoped feed
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.SpaceId, d.IsPinned, d.LastActivityAt, d.Id })
            .IsDescending(false, true, true, true)
            .HasDatabaseName("IX_Discussion_SpaceId_Pinned_LastActivityAt_Id");

        // Post feed: discussion posts sorted by CreatedAt (keyset pagination)
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasIndex(p => new { p.DiscussionId, p.CreatedAt, p.Id })
            .HasDatabaseName("IX_Post_DiscussionId_CreatedAt_Id");

        // Post feed: user's posts (for profile/search)
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasIndex(p => new { p.CreatedByUserId, p.CreatedAt, p.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_Post_CreatedByUserId_CreatedAt_Id_Desc");

        // Post PublicId unique index (for API lookups)
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasIndex(p => p.PublicId)
            .IsUnique();

        // Discussion feed: user's discussions (for profile/search)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.CreatedByUserId, d.CreatedAt, d.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_Discussion_CreatedByUserId_CreatedAt_Id_Desc");

        // === Social Features Configuration ===

        // Reaction: unique constraint (one reaction type per user per post)
        modelBuilder.Entity<PostReactionDatabaseEntity>()
            .HasIndex(r => new { r.PostId, r.UserId, r.TypeId })
            .IsUnique();

        modelBuilder.Entity<PostReactionDatabaseEntity>()
            .HasIndex(r => r.PublicId)
            .IsUnique();

        modelBuilder.Entity<PostReactionDatabaseEntity>()
            .HasOne(r => r.Post)
            .WithMany()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Use NoAction to avoid cascade path conflicts with User
        modelBuilder.Entity<PostReactionDatabaseEntity>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Follow: unique constraint (one follow per user per target)
        modelBuilder.Entity<UserFollowDatabaseEntity>()
            .HasIndex(f => new { f.UserId, f.TargetTypeId, f.DiscussionId, f.SpaceId, f.FollowedUserId })
            .IsUnique();

        modelBuilder.Entity<UserFollowDatabaseEntity>()
            .HasIndex(f => f.PublicId)
            .IsUnique();

        // Use NoAction to avoid multiple cascade path issues
        modelBuilder.Entity<UserFollowDatabaseEntity>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserFollowDatabaseEntity>()
            .HasOne(f => f.Discussion)
            .WithMany()
            .HasForeignKey(f => f.DiscussionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserFollowDatabaseEntity>()
            .HasOne(f => f.Space)
            .WithMany()
            .HasForeignKey(f => f.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserFollowDatabaseEntity>()
            .HasOne(f => f.FollowedUser)
            .WithMany()
            .HasForeignKey(f => f.FollowedUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Mention: unique constraint (one mention per user per post)
        modelBuilder.Entity<PostMentionDatabaseEntity>()
            .HasIndex(m => new { m.PostId, m.MentionedUserId })
            .IsUnique();

        modelBuilder.Entity<PostMentionDatabaseEntity>()
            .HasIndex(m => m.PublicId)
            .IsUnique();

        modelBuilder.Entity<PostMentionDatabaseEntity>()
            .HasOne(m => m.Post)
            .WithMany()
            .HasForeignKey(m => m.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Use NoAction to avoid cascade path conflicts
        modelBuilder.Entity<PostMentionDatabaseEntity>()
            .HasOne(m => m.MentionedUser)
            .WithMany()
            .HasForeignKey(m => m.MentionedUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Notification indexes for efficient querying
        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt });

        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasIndex(n => n.PublicId)
            .IsUnique();

        // Use NoAction for all Notification FKs to avoid multiple cascade paths
        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasOne(n => n.RecipientUser)
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasOne(n => n.SourcePost)
            .WithMany()
            .HasForeignKey(n => n.SourcePostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasOne(n => n.SourceDiscussion)
            .WithMany()
            .HasForeignKey(n => n.SourceDiscussionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasOne(n => n.SourceSpace)
            .WithMany()
            .HasForeignKey(n => n.SourceSpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserNotificationDatabaseEntity>()
            .HasOne(n => n.ActorUser)
            .WithMany()
            .HasForeignKey(n => n.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // === Moderation Configuration ===

        // Soft delete filter for ReportReason
        modelBuilder.Entity<ReportReasonDatabaseEntity>().HasQueryFilter(e => !e.IsDeleted);

        // UserRole configuration
        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasIndex(ur => ur.PublicId)
            .IsUnique();

        // Index for finding active roles by user
        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasIndex(ur => new { ur.UserId, ur.RoleId, ur.RevokedAt })
            .HasDatabaseName("IX_UserRole_UserId_RoleId_RevokedAt");

        // Index for finding roles by scope
        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasIndex(ur => new { ur.CommunityId, ur.RoleId, ur.RevokedAt })
            .HasDatabaseName("IX_UserRole_CommunityId_Role_RevokedAt");

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasIndex(ur => new { ur.HubId, ur.RoleId, ur.RevokedAt })
            .HasDatabaseName("IX_UserRole_HubId_Role_RevokedAt");

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasIndex(ur => new { ur.SpaceId, ur.RoleId, ur.RevokedAt })
            .HasDatabaseName("IX_UserRole_SpaceId_Role_RevokedAt");

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.Roles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(ur => ur.Community)
            .WithMany()
            .HasForeignKey(ur => ur.CommunityId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(ur => ur.Hub)
            .WithMany()
            .HasForeignKey(ur => ur.HubId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(ur => ur.Space)
            .WithMany()
            .HasForeignKey(ur => ur.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(ur => ur.AssignedByUser)
            .WithMany()
            .HasForeignKey(ur => ur.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(ur => ur.RevokedByUser)
            .WithMany()
            .HasForeignKey(ur => ur.RevokedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // UserBan configuration
        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasIndex(ub => ub.PublicId)
            .IsUnique();

        // Index for checking if user is banned in a scope
        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasIndex(ub => new { ub.UserId, ub.UnbannedAt, ub.ExpiresAt })
            .HasDatabaseName("IX_UserBan_UserId_UnbannedAt_ExpiresAt");

        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(ub => ub.User)
            .WithMany()
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(ub => ub.Community)
            .WithMany()
            .HasForeignKey(ub => ub.CommunityId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(ub => ub.Hub)
            .WithMany()
            .HasForeignKey(ub => ub.HubId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(ub => ub.Space)
            .WithMany()
            .HasForeignKey(ub => ub.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(ub => ub.BannedByUser)
            .WithMany()
            .HasForeignKey(ub => ub.BannedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(ub => ub.UnbannedByUser)
            .WithMany()
            .HasForeignKey(ub => ub.UnbannedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // ReportReason configuration
        modelBuilder.Entity<ReportReasonDatabaseEntity>()
            .HasIndex(rr => rr.PublicId)
            .IsUnique();

        modelBuilder.Entity<ReportReasonDatabaseEntity>()
            .HasOne(rr => rr.Community)
            .WithMany()
            .HasForeignKey(rr => rr.CommunityId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportReasonDatabaseEntity>()
            .HasOne(rr => rr.Hub)
            .WithMany()
            .HasForeignKey(rr => rr.HubId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportReasonDatabaseEntity>()
            .HasOne(rr => rr.Space)
            .WithMany()
            .HasForeignKey(rr => rr.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportReasonDatabaseEntity>()
            .HasOne(rr => rr.CreatedByUser)
            .WithMany()
            .HasForeignKey(rr => rr.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Report configuration
        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasIndex(r => r.PublicId)
            .IsUnique();

        // Index for moderator queue (pending reports by scope)
        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasIndex(r => new { r.StatusId, r.CommunityId, r.CreatedAt })
            .HasDatabaseName("IX_Report_Status_CommunityId_CreatedAt");

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasIndex(r => new { r.StatusId, r.HubId, r.CreatedAt })
            .HasDatabaseName("IX_Report_Status_HubId_CreatedAt");

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasIndex(r => new { r.StatusId, r.SpaceId, r.CreatedAt })
            .HasDatabaseName("IX_Report_Status_SpaceId_CreatedAt");

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.ReporterUser)
            .WithMany()
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.ReportedPost)
            .WithMany()
            .HasForeignKey(r => r.ReportedPostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.ReportedDiscussion)
            .WithMany()
            .HasForeignKey(r => r.ReportedDiscussionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.ReportedUser)
            .WithMany()
            .HasForeignKey(r => r.ReportedUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.Reason)
            .WithMany()
            .HasForeignKey(r => r.ReasonId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.ResolvedByUser)
            .WithMany()
            .HasForeignKey(r => r.ResolvedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.Space)
            .WithMany()
            .HasForeignKey(r => r.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.Hub)
            .WithMany()
            .HasForeignKey(r => r.HubId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.Community)
            .WithMany()
            .HasForeignKey(r => r.CommunityId)
            .OnDelete(DeleteBehavior.NoAction);

        // ReportComment configuration
        modelBuilder.Entity<ReportCommentDatabaseEntity>()
            .HasIndex(rc => rc.PublicId)
            .IsUnique();

        modelBuilder.Entity<ReportCommentDatabaseEntity>()
            .HasOne(rc => rc.Report)
            .WithMany(r => r.Comments)
            .HasForeignKey(rc => rc.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReportCommentDatabaseEntity>()
            .HasOne(rc => rc.AuthorUser)
            .WithMany()
            .HasForeignKey(rc => rc.AuthorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // ModerationLog configuration
        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasIndex(ml => ml.PublicId)
            .IsUnique();

        // Index for viewing moderation history by scope
        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasIndex(ml => new { ml.CommunityId, ml.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ModerationLog_CommunityId_CreatedAt_Desc");

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasIndex(ml => new { ml.ActorUserId, ml.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ModerationLog_ActorUserId_CreatedAt_Desc");

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.ActorUser)
            .WithMany()
            .HasForeignKey(ml => ml.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.TargetPost)
            .WithMany()
            .HasForeignKey(ml => ml.TargetPostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.TargetDiscussion)
            .WithMany()
            .HasForeignKey(ml => ml.TargetDiscussionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.TargetUser)
            .WithMany()
            .HasForeignKey(ml => ml.TargetUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.TargetReport)
            .WithMany()
            .HasForeignKey(ml => ml.TargetReportId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.TargetUserRole)
            .WithMany()
            .HasForeignKey(ml => ml.TargetUserRoleId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.TargetUserBan)
            .WithMany()
            .HasForeignKey(ml => ml.TargetUserBanId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.Community)
            .WithMany()
            .HasForeignKey(ml => ml.CommunityId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.Hub)
            .WithMany()
            .HasForeignKey(ml => ml.HubId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(ml => ml.Space)
            .WithMany()
            .HasForeignKey(ml => ml.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        // === Permissions Configuration ===

        // Permission indexes
        modelBuilder.Entity<PermissionDatabaseEntity>()
            .HasIndex(p => p.PublicId)
            .IsUnique();

        modelBuilder.Entity<PermissionDatabaseEntity>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<PermissionDatabaseEntity>()
            .HasIndex(p => p.Category)
            .HasDatabaseName("IX_Permission_Category");

        // RolePermission indexes
        modelBuilder.Entity<RolePermissionDatabaseEntity>()
            .HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .IsUnique();

        modelBuilder.Entity<RolePermissionDatabaseEntity>()
            .HasIndex(rp => rp.RoleId)
            .HasDatabaseName("IX_RolePermission_RoleId");

        modelBuilder.Entity<RolePermissionDatabaseEntity>()
            .HasIndex(rp => rp.PermissionId)
            .HasDatabaseName("IX_RolePermission_PermissionId");

        // RolePermission relationships
        modelBuilder.Entity<RolePermissionDatabaseEntity>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermissionDatabaseEntity>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermissionDatabaseEntity>()
            .HasOne(rp => rp.GrantedBy)
            .WithMany()
            .HasForeignKey(rp => rp.GrantedById)
            .OnDelete(DeleteBehavior.SetNull);

        // TemporaryRoleElevation indexes
        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasIndex(tre => tre.PublicId)
            .IsUnique();

        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasIndex(tre => tre.UserId)
            .HasDatabaseName("IX_TemporaryRoleElevation_UserId");

        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasIndex(tre => tre.ExpiresAt)
            .HasDatabaseName("IX_TemporaryRoleElevation_ExpiresAt");

        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasIndex(tre => new { tre.UserId, tre.ExpiresAt })
            .HasFilter("\"RevokedAt\" IS NULL")
            .HasDatabaseName("IX_TemporaryRoleElevation_UserId_ExpiresAt_Active");

        // TemporaryRoleElevation relationships
        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasOne(tre => tre.User)
            .WithMany()
            .HasForeignKey(tre => tre.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasOne(tre => tre.GrantedBy)
            .WithMany()
            .HasForeignKey(tre => tre.GrantedById)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TemporaryRoleElevationDatabaseEntity>()
            .HasOne(tre => tre.RevokedBy)
            .WithMany()
            .HasForeignKey(tre => tre.RevokedById)
            .OnDelete(DeleteBehavior.SetNull);

        // === User Indexes ===

        // PublicId unique constraint
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.PublicId)
            .IsUnique();

        // DisplayName index for user search/autocomplete
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.DisplayName)
            .HasDatabaseName("IX_User_DisplayName");

        // EmailHash index for lookups (deterministic SHA-256 hash of lowercased email)
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.EmailHash)
            .IsUnique()
            .HasFilter("\"EmailHash\" IS NOT NULL");

        // === Soft Delete Indexes ===
        // These indexes improve performance of global query filters

        // Discussion soft-delete index (improves WHERE IsDeleted = 0 queries)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => d.IsDeleted)
            .HasDatabaseName("IX_Discussion_IsDeleted");

        // Post soft-delete index
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasIndex(p => p.IsDeleted)
            .HasDatabaseName("IX_Post_IsDeleted");

        // User soft-delete index
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.IsDeleted)
            .HasDatabaseName("IX_User_IsDeleted");

        // === Trending/Activity Indexes ===

        // Discussion activity tracking (for trending/hot discussions)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.CreatedAt, d.IsDeleted })
            .IsDescending(true, false)
            .HasDatabaseName("IX_Discussion_CreatedAt_IsDeleted_Desc");

        // Post creation tracking (for trending posts)
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasIndex(p => new { p.CreatedAt, p.DiscussionId, p.IsDeleted })
            .IsDescending(true, false, false)
            .HasDatabaseName("IX_Post_CreatedAt_DiscussionId_IsDeleted");

        // Post reply-to index (for fetching reply chains)
        modelBuilder.Entity<PostDatabaseEntity>()
            .HasIndex(p => p.ReplyToPostId)
            .HasDatabaseName("IX_Post_ReplyToPostId");

        // === RefreshToken Configuration ===

        modelBuilder.Entity<RefreshTokenDatabaseEntity>()
            .HasIndex(r => r.TokenValue)
            .IsUnique();

        modelBuilder.Entity<RefreshTokenDatabaseEntity>()
            .HasIndex(r => r.UserId);

        modelBuilder.Entity<RefreshTokenDatabaseEntity>()
            .HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // RefreshToken: self-referencing relationship for token rotation
        modelBuilder.Entity<RefreshTokenDatabaseEntity>()
            .HasOne(r => r.ReplacedByToken)
            .WithMany()
            .HasForeignKey(r => r.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        // === PasswordResetToken Configuration ===

        modelBuilder.Entity<PasswordResetTokenDatabaseEntity>()
            .HasIndex(t => t.PublicId)
            .IsUnique();

        modelBuilder.Entity<PasswordResetTokenDatabaseEntity>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<PasswordResetTokenDatabaseEntity>()
            .HasIndex(t => t.UserId)
            .HasDatabaseName("IX_PasswordResetToken_UserId");

        modelBuilder.Entity<PasswordResetTokenDatabaseEntity>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // === PasswordResetRequest Configuration ===

        modelBuilder.Entity<PasswordResetRequestDatabaseEntity>()
            .HasIndex(r => new { r.IpAddress, r.RequestedAt })
            .HasDatabaseName("IX_PasswordResetRequest_IpAddress_RequestedAt");

        modelBuilder.Entity<PasswordResetRequestDatabaseEntity>()
            .HasIndex(r => r.RequestedAt)
            .HasDatabaseName("IX_PasswordResetRequest_RequestedAt");

        // === 2FA Configuration ===

        // TrustedDevice configuration
        modelBuilder.Entity<TwoFactorTrustedDeviceDatabaseEntity>()
            .HasIndex(t => t.PublicId)
            .IsUnique();

        modelBuilder.Entity<TwoFactorTrustedDeviceDatabaseEntity>()
            .HasOne(t => t.User)
            .WithMany(u => u.TwoFactorTrustedDevices)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TwoFactorTrustedDeviceDatabaseEntity>()
            .HasIndex(t => new { t.UserId, t.DeviceFingerprint });

        // BackupCode configuration
        modelBuilder.Entity<TwoFactorBackupCodeDatabaseEntity>()
            .HasIndex(b => b.PublicId)
            .IsUnique();

        modelBuilder.Entity<TwoFactorBackupCodeDatabaseEntity>()
            .HasOne(b => b.User)
            .WithMany(u => u.TwoFactorBackupCodes)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // === SystemSettings Configuration ===

        modelBuilder.Entity<SystemSettingDatabaseEntity>()
            .HasIndex(s => s.PublicId)
            .IsUnique();

        modelBuilder.Entity<SystemSettingDatabaseEntity>()
            .HasIndex(s => s.Category)
            .HasDatabaseName("IX_SystemSettings_Category");

        modelBuilder.Entity<SystemSettingDatabaseEntity>()
            .HasIndex(s => new { s.Category, s.Key })
            .IsUnique()
            .HasDatabaseName("IX_SystemSettings_Category_Key");

        modelBuilder.Entity<SystemSettingDatabaseEntity>()
            .HasOne(s => s.UpdatedBy)
            .WithMany()
            .HasForeignKey(s => s.UpdatedById)
            .OnDelete(DeleteBehavior.SetNull);

        // === Achievement System Configuration ===

        // Achievement indexes
        modelBuilder.Entity<AchievementDatabaseEntity>()
            .HasIndex(a => a.PublicId)
            .IsUnique();

        modelBuilder.Entity<AchievementDatabaseEntity>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        modelBuilder.Entity<AchievementDatabaseEntity>()
            .HasIndex(a => new { a.IsActive, a.DisplayOrder })
            .HasDatabaseName("IX_Achievement_IsActive_DisplayOrder");

        // Note: Achievement Category and RequirementType now use enums (no FK relationships)

        // UserAchievementProgress indexes
        modelBuilder.Entity<UserAchievementProgressDatabaseEntity>()
            .HasIndex(p => new { p.UserId, p.AchievementId })
            .IsUnique();

        // UserAchievementProgress relationships
        modelBuilder.Entity<UserAchievementProgressDatabaseEntity>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserAchievementProgressDatabaseEntity>()
            .HasOne(p => p.Achievement)
            .WithMany(a => a.UserAchievementProgress)
            .HasForeignKey(p => p.AchievementId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserAchievement indexes
        modelBuilder.Entity<UserAchievementDatabaseEntity>()
            .HasIndex(ua => ua.PublicId)
            .IsUnique();

        modelBuilder.Entity<UserAchievementDatabaseEntity>()
            .HasIndex(ua => new { ua.UserId, ua.AchievementId })
            .IsUnique();

        modelBuilder.Entity<UserAchievementDatabaseEntity>()
            .HasIndex(ua => new { ua.UserId, ua.EarnedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_UserAchievement_UserId_EarnedAt_Desc");

        modelBuilder.Entity<UserAchievementDatabaseEntity>()
            .HasIndex(ua => new { ua.UserId, ua.IsDisplayed, ua.DisplayOrder })
            .HasDatabaseName("IX_UserAchievement_UserId_IsDisplayed_DisplayOrder");

        // UserAchievement relationships
        modelBuilder.Entity<UserAchievementDatabaseEntity>()
            .HasOne(ua => ua.User)
            .WithMany()
            .HasForeignKey(ua => ua.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserAchievementDatabaseEntity>()
            .HasOne(ua => ua.Achievement)
            .WithMany(a => a.UserAchievements)
            .HasForeignKey(ua => ua.AchievementId)
            .OnDelete(DeleteBehavior.Restrict);

        // === UserMetrics Configuration ===

        // Composite primary key
        modelBuilder.Entity<UserMetricDatabaseEntity>()
            .HasKey(m => new { m.UserId, m.MetricType, m.Scope, m.ScopeId });

        // Index for finding recently updated metrics
        modelBuilder.Entity<UserMetricDatabaseEntity>()
            .HasIndex(m => m.LastUpdated)
            .HasDatabaseName("IX_UserMetric_LastUpdated");

        // Index for scope-based queries
        modelBuilder.Entity<UserMetricDatabaseEntity>()
            .HasIndex(m => new { m.UserId, m.Scope, m.ScopeId })
            .HasDatabaseName("IX_UserMetric_UserId_Scope_ScopeId");

        // UserMetrics relationship with User
        modelBuilder.Entity<UserMetricDatabaseEntity>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Webhooks Configuration ===

        // Webhook indexes
        modelBuilder.Entity<WebhookDatabaseEntity>()
            .HasIndex(w => w.Id)
            .IsUnique();

        modelBuilder.Entity<WebhookDatabaseEntity>()
            .HasIndex(w => w.IsActive)
            .HasDatabaseName("IX_Webhook_IsActive");

        modelBuilder.Entity<WebhookDatabaseEntity>()
            .HasIndex(w => w.CreatedAt)
            .IsDescending(true)
            .HasDatabaseName("IX_Webhook_CreatedAt_Desc");

        // Webhook relationships
        modelBuilder.Entity<WebhookDatabaseEntity>()
            .HasOne(w => w.CreatedByUser)
            .WithMany()
            .HasForeignKey(w => w.CreatedBy)
            .HasPrincipalKey(u => u.PublicId)
            .OnDelete(DeleteBehavior.Restrict);

        // WebhookDeliveryLog indexes
        modelBuilder.Entity<WebhookDeliveryLogDatabaseEntity>()
            .HasIndex(wdl => wdl.WebhookId)
            .HasDatabaseName("IX_WebhookDeliveryLog_WebhookId");

        modelBuilder.Entity<WebhookDeliveryLogDatabaseEntity>()
            .HasIndex(wdl => new { wdl.WebhookId, wdl.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_WebhookDeliveryLog_WebhookId_CreatedAt_Desc");

        modelBuilder.Entity<WebhookDeliveryLogDatabaseEntity>()
            .HasIndex(wdl => new { wdl.IsSuccess, wdl.NextRetryAt })
            .HasFilter("\"NextRetryAt\" IS NOT NULL")
            .HasDatabaseName("IX_WebhookDeliveryLog_IsSuccess_NextRetryAt");

        modelBuilder.Entity<WebhookDeliveryLogDatabaseEntity>()
            .HasIndex(wdl => wdl.EventType)
            .HasDatabaseName("IX_WebhookDeliveryLog_EventType");

        // WebhookDeliveryLog relationships
        modelBuilder.Entity<WebhookDeliveryLogDatabaseEntity>()
            .HasOne(wdl => wdl.Webhook)
            .WithMany(w => w.DeliveryLogs)
            .HasForeignKey(wdl => wdl.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // === AuditLog Indexes ===

        modelBuilder.Entity<AuditLogDatabaseEntity>()
            .HasIndex(a => a.PublicId)
            .IsUnique();

        // Primary query index: admin audit log page (paginated, filtered, sorted by CreatedAt)
        modelBuilder.Entity<AuditLogDatabaseEntity>()
            .HasIndex(a => new { a.CreatedAt, a.Category, a.Action })
            .IsDescending(true, false, false)
            .HasDatabaseName("IX_AuditLog_CreatedAt_Desc_Category_Action");

        // Suspicious activity detection (severity + time range)
        modelBuilder.Entity<AuditLogDatabaseEntity>()
            .HasIndex(a => new { a.SeverityId, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AuditLog_SeverityId_CreatedAt_Desc");

        // Moderation action panels (category-scoped + time-sorted)
        modelBuilder.Entity<AuditLogDatabaseEntity>()
            .HasIndex(a => new { a.Category, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AuditLog_Category_CreatedAt_Desc");

        // Actor-filtered audit log queries
        modelBuilder.Entity<AuditLogDatabaseEntity>()
            .HasIndex(a => a.ActorUserId)
            .HasDatabaseName("IX_AuditLog_ActorUserId");

        // Failed login detection (action + success + time range)
        modelBuilder.Entity<AuditLogDatabaseEntity>()
            .HasIndex(a => new { a.Action, a.Success, a.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_AuditLog_Action_Success_CreatedAt_Desc");

        // === Display Name History ===

        modelBuilder.Entity<UserDisplayNameHistoryDatabaseEntity>()
            .HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserDisplayNameHistoryDatabaseEntity>()
            .HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserDisplayNameHistoryDatabaseEntity>()
            .HasIndex(h => h.UserId)
            .HasDatabaseName("IX_DisplayNameHistory_UserId");

        modelBuilder.Entity<UserDisplayNameHistoryDatabaseEntity>()
            .HasIndex(h => h.NewName)
            .HasDatabaseName("IX_DisplayNameHistory_NewName");

        // === Additional Discussion Indexes ===

        // Discussion date-range queries scoped to a space (newDiscussionsToday/thisWeek)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.SpaceId, d.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Discussion_SpaceId_CreatedAt_Desc");

        // === Full-Text Search (tsvector stored generated columns + GIN indexes) ===
        // NpgsqlTsVector is PostgreSQL-specific; ignore for other providers (SQLite, InMemory)
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            modelBuilder.Entity<DiscussionDatabaseEntity>()
                .Property(d => d.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """to_tsvector('english', coalesce("Title", ''))""",
                    stored: true);

            modelBuilder.Entity<DiscussionDatabaseEntity>()
                .HasIndex(d => d.SearchVector)
                .HasMethod("GIN")
                .HasDatabaseName("IX_Discussion_SearchVector_Gin");

            modelBuilder.Entity<PostDatabaseEntity>()
                .Property(p => p.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """to_tsvector('english', coalesce("Content", ''))""",
                    stored: true);

            modelBuilder.Entity<PostDatabaseEntity>()
                .HasIndex(p => p.SearchVector)
                .HasMethod("GIN")
                .HasDatabaseName("IX_Post_SearchVector_Gin");
        }
        else
        {
            modelBuilder.Entity<DiscussionDatabaseEntity>()
                .Ignore(d => d.SearchVector);

            modelBuilder.Entity<PostDatabaseEntity>()
                .Ignore(p => p.SearchVector);
        }

        // === Discussion Types Configuration ===

        // CommunityAllowedDiscussionType: composite PK (link table)
        modelBuilder.Entity<CommunityAllowedDiscussionTypeDatabaseEntity>()
            .HasKey(x => new { x.CommunityId, x.DiscussionType });

        modelBuilder.Entity<CommunityAllowedDiscussionTypeDatabaseEntity>()
            .HasOne(x => x.Community)
            .WithMany(c => c.AllowedDiscussionTypes)
            .HasForeignKey(x => x.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);

        // HubAllowedDiscussionType: composite PK (link table)
        modelBuilder.Entity<HubAllowedDiscussionTypeDatabaseEntity>()
            .HasKey(x => new { x.HubId, x.DiscussionType });

        modelBuilder.Entity<HubAllowedDiscussionTypeDatabaseEntity>()
            .HasOne(x => x.Hub)
            .WithMany(h => h.AllowedDiscussionTypes)
            .HasForeignKey(x => x.HubId)
            .OnDelete(DeleteBehavior.Cascade);

        // SpaceAllowedDiscussionType: composite PK (link table)
        modelBuilder.Entity<SpaceAllowedDiscussionTypeDatabaseEntity>()
            .HasKey(x => new { x.SpaceId, x.DiscussionType });

        modelBuilder.Entity<SpaceAllowedDiscussionTypeDatabaseEntity>()
            .HasOne(x => x.Space)
            .WithMany(s => s.AllowedDiscussionTypes)
            .HasForeignKey(x => x.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // DiscussionLink: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypeLinkDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeLinkDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeLinkDatabaseEntity>()
            .Property(x => x.Url)
            .HasMaxLength(2048)
            .IsRequired();

        // DiscussionImages: one-to-one with Discussion (images layout settings)
        modelBuilder.Entity<DiscussionTypeImageDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeImageDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ImageAttachments: ordered join between DiscussionTypeImage and Image
        modelBuilder.Entity<DiscussionTypeImageAttachmentDatabaseEntity>()
            .HasOne(x => x.DiscussionTypeImage)
            .WithMany(g => g.Images)
            .HasForeignKey(x => x.DiscussionTypeImageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeImageAttachmentDatabaseEntity>()
            .HasOne(x => x.Image)
            .WithMany()
            .HasForeignKey(x => x.ImageId)
            .OnDelete(DeleteBehavior.Cascade);

        // DiscussionPoll: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypePollDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypePollDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        // PollOption: belongs to Poll
        modelBuilder.Entity<DiscussionTypePollOptionDatabaseEntity>()
            .HasOne(x => x.Poll)
            .WithMany(p => p.Options)
            .HasForeignKey(x => x.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypePollOptionDatabaseEntity>()
            .Property(x => x.Text)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<DiscussionTypePollOptionDatabaseEntity>()
            .HasIndex(x => new { x.PollId, x.DisplayOrder })
            .HasDatabaseName("IX_DiscussionTypePollOption_PollId_DisplayOrder");

        // PollVote: belongs to Option and User
        modelBuilder.Entity<DiscussionTypePollVoteDatabaseEntity>()
            .HasIndex(x => new { x.OptionId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_DiscussionTypePollVote_OptionId_UserId");

        modelBuilder.Entity<DiscussionTypePollVoteDatabaseEntity>()
            .HasOne(x => x.Option)
            .WithMany(o => o.Votes)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypePollVoteDatabaseEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Discussion Type index (for filtering by type)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.SpaceId, d.Type })
            .HasDatabaseName("IX_Discussion_SpaceId_Type");

        // DiscussionQuestion: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypeQuestionDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeQuestionDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeQuestionDatabaseEntity>()
            .HasOne(x => x.AcceptedPost)
            .WithMany()
            .HasForeignKey(x => x.AcceptedPostId)
            .OnDelete(DeleteBehavior.SetNull);

        // DiscussionGuide: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypeGuideDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeGuideDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DiscussionDebate: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypeDebateDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeDebateDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DiscussionDebatePosition: belongs to Debate
        modelBuilder.Entity<DiscussionTypeDebatePositionDatabaseEntity>()
            .HasOne(x => x.Debate)
            .WithMany(d => d.Positions)
            .HasForeignKey(x => x.DebateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeDebatePositionDatabaseEntity>()
            .HasIndex(x => new { x.DebateId, x.Index })
            .IsUnique()
            .HasDatabaseName("IX_DebatePosition_DebateId_Index");

        // PostDebatePosition: one-to-one with Post (PostId is PK)
        modelBuilder.Entity<DiscussionTypeDebatePostPositionDatabaseEntity>()
            .HasKey(x => x.PostId);

        modelBuilder.Entity<DiscussionTypeDebatePostPositionDatabaseEntity>()
            .HasOne(x => x.Post)
            .WithMany()
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeDebatePostPositionDatabaseEntity>()
            .HasOne(x => x.Position)
            .WithMany()
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DiscussionJournal: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypeJournalDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeJournalDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        // JournalEntryPost: one-to-one with Post (PostId is PK)
        modelBuilder.Entity<DiscussionTypeJournalEntryPostDatabaseEntity>()
            .HasKey(x => x.PostId);

        modelBuilder.Entity<DiscussionTypeJournalEntryPostDatabaseEntity>()
            .HasOne(x => x.Post)
            .WithMany()
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeJournalEntryPostDatabaseEntity>()
            .HasOne(x => x.Journal)
            .WithMany(j => j.Entries)
            .HasForeignKey(x => x.JournalId)
            .OnDelete(DeleteBehavior.Cascade);

        // DiscussionIama: one-to-one with Discussion
        modelBuilder.Entity<DiscussionTypeIamaDatabaseEntity>()
            .HasIndex(x => x.DiscussionId)
            .IsUnique();

        modelBuilder.Entity<DiscussionTypeIamaDatabaseEntity>()
            .HasOne(x => x.Discussion)
            .WithMany()
            .HasForeignKey(x => x.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);

        // IamaOfficialAnswer: belongs to Iama, references QuestionPost and AnswerPost
        modelBuilder.Entity<DiscussionTypeIamaOfficialAnswerDatabaseEntity>()
            .HasOne(x => x.Iama)
            .WithMany(i => i.OfficialAnswers)
            .HasForeignKey(x => x.IamaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeIamaOfficialAnswerDatabaseEntity>()
            .HasOne(x => x.QuestionPost)
            .WithMany()
            .HasForeignKey(x => x.QuestionPostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DiscussionTypeIamaOfficialAnswerDatabaseEntity>()
            .HasOne(x => x.AnswerPost)
            .WithMany()
            .HasForeignKey(x => x.AnswerPostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DiscussionTypeIamaOfficialAnswerDatabaseEntity>()
            .HasIndex(x => new { x.IamaId, x.QuestionPostId })
            .IsUnique()
            .HasDatabaseName("IX_IamaOfficialAnswer_IamaId_QuestionPostId");

        // IamaBestQuestion: belongs to Iama, references Post
        modelBuilder.Entity<DiscussionTypeIamaBestQuestionDatabaseEntity>()
            .HasOne(x => x.Iama)
            .WithMany(i => i.BestQuestions)
            .HasForeignKey(x => x.IamaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiscussionTypeIamaBestQuestionDatabaseEntity>()
            .HasOne(x => x.Post)
            .WithMany()
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DiscussionTypeIamaBestQuestionDatabaseEntity>()
            .HasIndex(x => new { x.IamaId, x.DisplayOrder })
            .IsUnique()
            .HasDatabaseName("IX_IamaBestQuestion_IamaId_DisplayOrder");

        // === Image Configuration ===

        modelBuilder.Entity<ImageDatabaseEntity>()
            .HasIndex(m => m.PublicId)
            .IsUnique()
            .HasDatabaseName("IX_Image_PublicId");

        modelBuilder.Entity<ImageDatabaseEntity>()
            .HasIndex(m => m.Sha256Hash)
            .HasDatabaseName("IX_Image_Sha256Hash");

        modelBuilder.Entity<ImageDatabaseEntity>()
            .HasOne(m => m.UploadedByUser)
            .WithMany()
            .HasForeignKey(m => m.UploadedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ImageDatabaseEntity>()
            .Property(m => m.PublicId)
            .HasMaxLength(36)
            .IsRequired();

        modelBuilder.Entity<ImageDatabaseEntity>()
            .Property(m => m.Sha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        modelBuilder.Entity<ImageDatabaseEntity>()
            .Property(m => m.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<ImageDatabaseEntity>()
            .Property(m => m.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        modelBuilder.Entity<ImageDatabaseEntity>()
            .Property(m => m.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        // Cleanup indexes
        modelBuilder.Entity<ImageDatabaseEntity>()
            .HasIndex(m => m.IsReadyForDeletion)
            .HasDatabaseName("IX_Image_IsReadyForDeletion");

        modelBuilder.Entity<ImageDatabaseEntity>()
            .HasIndex(m => m.IsDeleted)
            .HasDatabaseName("IX_Image_IsDeleted");

        // PostImage: composite PK (join table)
        modelBuilder.Entity<PostImageDatabaseEntity>()
            .HasKey(pm => new { pm.PostId, pm.ImageId });

        modelBuilder.Entity<PostImageDatabaseEntity>()
            .HasOne(pm => pm.Post)
            .WithMany()
            .HasForeignKey(pm => pm.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PostImageDatabaseEntity>()
            .HasOne(pm => pm.Image)
            .WithMany()
            .HasForeignKey(pm => pm.ImageId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Banner Configuration ===

        modelBuilder.Entity<BannerDatabaseEntity>()
            .HasIndex(a => a.PublicId)
            .IsUnique();

        modelBuilder.Entity<BannerDatabaseEntity>()
            .HasIndex(a => new { a.ScopeId, a.ScopeEntityId });

        modelBuilder.Entity<BannerDatabaseEntity>()
            .HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // === Consent Configuration ===

        modelBuilder.Entity<ConsentTypeDatabaseEntity>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        modelBuilder.Entity<ConsentTypeVersionDatabaseEntity>()
            .HasOne(v => v.ConsentType)
            .WithMany(c => c.Versions)
            .HasForeignKey(v => v.ConsentTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConsentTypeVersionDatabaseEntity>()
            .HasIndex(v => new { v.ConsentTypeId, v.VersionNumber })
            .IsUnique();

        modelBuilder.Entity<UserConsentDatabaseEntity>()
            .HasOne(uc => uc.User)
            .WithMany()
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserConsentDatabaseEntity>()
            .HasOne(uc => uc.ConsentTypeVersion)
            .WithMany()
            .HasForeignKey(uc => uc.ConsentTypeVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserConsentDatabaseEntity>()
            .HasIndex(uc => new { uc.UserId, uc.ConsentTypeVersionId })
            .IsUnique();
    }
}
