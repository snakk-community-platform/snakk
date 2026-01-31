namespace Snakk.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database.Entities;

public class SnakkDbContext(DbContextOptions<SnakkDbContext> options) : DbContext(options)
{
    // Community layer
    public DbSet<CommunityDatabaseEntity> Communities { get; set; } = null!;
    public DbSet<CommunityDomainDatabaseEntity> CommunityDomains { get; set; } = null!;

    // Core entities
    public DbSet<HubDatabaseEntity> Hubs { get; set; } = null!;
    public DbSet<SpaceDatabaseEntity> Spaces { get; set; } = null!;
    public DbSet<DiscussionDatabaseEntity> Discussions { get; set; } = null!;
    public DbSet<PostDatabaseEntity> Posts { get; set; } = null!;
    public DbSet<PostRevisionDatabaseEntity> PostRevisions { get; set; } = null!;
    public DbSet<UserDatabaseEntity> Users { get; set; } = null!;
    public DbSet<DiscussionReadStateDatabaseEntity> DiscussionReadStates { get; set; } = null!;

    // Social features
    public DbSet<ReactionDatabaseEntity> Reactions { get; set; } = null!;
    public DbSet<NotificationDatabaseEntity> Notifications { get; set; } = null!;
    public DbSet<FollowDatabaseEntity> Follows { get; set; } = null!;
    public DbSet<MentionDatabaseEntity> Mentions { get; set; } = null!;

    // Moderation
    public DbSet<UserRoleDatabaseEntity> UserRoles { get; set; } = null!;
    public DbSet<UserBanDatabaseEntity> UserBans { get; set; } = null!;
    public DbSet<ReportDatabaseEntity> Reports { get; set; } = null!;
    public DbSet<ReportCommentDatabaseEntity> ReportComments { get; set; } = null!;
    public DbSet<ReportReasonDatabaseEntity> ReportReasons { get; set; } = null!;
    public DbSet<ModerationLogDatabaseEntity> ModerationLogs { get; set; } = null!;

    // Lookup tables
    public DbSet<Entities.Lookups.CommunityVisibilityLookup> CommunityVisibilityLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.FollowTargetTypeLookup> FollowTargetTypeLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.FollowLevelLookup> FollowLevelLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.ModerationActionLookup> ModerationActionLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.NotificationTypeLookup> NotificationTypeLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.ReactionTypeLookup> ReactionTypeLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.ReportStatusLookup> ReportStatusLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.BanTypeLookup> BanTypeLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.UserRoleLookup> UserRoleLookups { get; set; } = null!;
    public DbSet<Entities.Lookups.UserRoleTypeLookup> UserRoleTypeLookups { get; set; } = null!;

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

        // CommunityVisibility
        modelBuilder.Entity<CommunityDatabaseEntity>()
            .HasOne(c => c.Visibility)
            .WithMany(v => v.Communities)
            .HasForeignKey(c => c.VisibilityId)
            .OnDelete(DeleteBehavior.Restrict);

        // FollowTargetType and FollowLevel
        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasOne(f => f.TargetType)
            .WithMany(t => t.Follows)
            .HasForeignKey(f => f.TargetTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasOne(f => f.Level)
            .WithMany(l => l.Follows)
            .HasForeignKey(f => f.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // ModerationAction
        modelBuilder.Entity<ModerationLogDatabaseEntity>()
            .HasOne(m => m.Action)
            .WithMany(a => a.ModerationLogs)
            .HasForeignKey(m => m.ActionId)
            .OnDelete(DeleteBehavior.Restrict);

        // NotificationType
        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasOne(n => n.Type)
            .WithMany(t => t.Notifications)
            .HasForeignKey(n => n.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ReactionType
        modelBuilder.Entity<ReactionDatabaseEntity>()
            .HasOne(r => r.Type)
            .WithMany(t => t.Reactions)
            .HasForeignKey(r => r.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // ReportStatus
        modelBuilder.Entity<ReportDatabaseEntity>()
            .HasOne(r => r.Status)
            .WithMany(s => s.Reports)
            .HasForeignKey(r => r.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // BanType
        modelBuilder.Entity<UserBanDatabaseEntity>()
            .HasOne(b => b.BanType)
            .WithMany(t => t.UserBans)
            .HasForeignKey(b => b.BanTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserRole (nullable)
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserRoleType
        modelBuilder.Entity<UserRoleDatabaseEntity>()
            .HasOne(r => r.Role)
            .WithMany(t => t.UserRoles)
            .HasForeignKey(r => r.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Hub -> Spaces relationship
        modelBuilder.Entity<SpaceDatabaseEntity>()
            .HasOne(s => s.Hub)
            .WithMany(h => h.Spaces)
            .HasForeignKey(s => s.HubId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // Configure Slug indexes for lookups
        modelBuilder.Entity<HubDatabaseEntity>()
            .HasIndex(h => h.Slug);

        modelBuilder.Entity<SpaceDatabaseEntity>()
            .HasIndex(s => s.Slug);

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

        // Discussion feed: user's discussions (for profile/search)
        modelBuilder.Entity<DiscussionDatabaseEntity>()
            .HasIndex(d => new { d.CreatedByUserId, d.CreatedAt, d.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_Discussion_CreatedByUserId_CreatedAt_Id_Desc");

        // === Social Features Configuration ===

        // Reaction: unique constraint (one reaction type per user per post)
        modelBuilder.Entity<ReactionDatabaseEntity>()
            .HasIndex(r => new { r.PostId, r.UserId, r.TypeId })
            .IsUnique();

        modelBuilder.Entity<ReactionDatabaseEntity>()
            .HasIndex(r => r.PublicId)
            .IsUnique();

        modelBuilder.Entity<ReactionDatabaseEntity>()
            .HasOne(r => r.Post)
            .WithMany()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Use NoAction to avoid cascade path conflicts with User
        modelBuilder.Entity<ReactionDatabaseEntity>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Follow: unique constraint (one follow per user per target)
        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasIndex(f => new { f.UserId, f.TargetTypeId, f.DiscussionId, f.SpaceId, f.FollowedUserId })
            .IsUnique();

        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasIndex(f => f.PublicId)
            .IsUnique();

        // Use NoAction to avoid multiple cascade path issues
        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasOne(f => f.Discussion)
            .WithMany()
            .HasForeignKey(f => f.DiscussionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasOne(f => f.Space)
            .WithMany()
            .HasForeignKey(f => f.SpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<FollowDatabaseEntity>()
            .HasOne(f => f.FollowedUser)
            .WithMany()
            .HasForeignKey(f => f.FollowedUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Mention: unique constraint (one mention per user per post)
        modelBuilder.Entity<MentionDatabaseEntity>()
            .HasIndex(m => new { m.PostId, m.MentionedUserId })
            .IsUnique();

        modelBuilder.Entity<MentionDatabaseEntity>()
            .HasIndex(m => m.PublicId)
            .IsUnique();

        modelBuilder.Entity<MentionDatabaseEntity>()
            .HasOne(m => m.Post)
            .WithMany()
            .HasForeignKey(m => m.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Use NoAction to avoid cascade path conflicts
        modelBuilder.Entity<MentionDatabaseEntity>()
            .HasOne(m => m.MentionedUser)
            .WithMany()
            .HasForeignKey(m => m.MentionedUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Notification indexes for efficient querying
        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt });

        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasIndex(n => n.PublicId)
            .IsUnique();

        // Use NoAction for all Notification FKs to avoid multiple cascade paths
        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasOne(n => n.RecipientUser)
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasOne(n => n.SourcePost)
            .WithMany()
            .HasForeignKey(n => n.SourcePostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasOne(n => n.SourceDiscussion)
            .WithMany()
            .HasForeignKey(n => n.SourceDiscussionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<NotificationDatabaseEntity>()
            .HasOne(n => n.SourceSpace)
            .WithMany()
            .HasForeignKey(n => n.SourceSpaceId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<NotificationDatabaseEntity>()
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
            .WithMany()
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

        // === User Indexes ===

        // PublicId unique constraint
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.PublicId)
            .IsUnique();

        // DisplayName index for user search/autocomplete
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.DisplayName)
            .HasDatabaseName("IX_User_DisplayName");

        // Email index for lookups
        modelBuilder.Entity<UserDatabaseEntity>()
            .HasIndex(u => u.Email)
            .IsUnique();

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
    }
}
