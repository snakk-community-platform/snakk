using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Helpers;

public class TestDataBuilder
{
    private readonly SnakkDbContext _context;
    private int _counter;

    public TestDataBuilder(SnakkDbContext context) => _context = context;

    private string NextId() => $"{++_counter}_{Guid.NewGuid():N}";

    public async Task<UserDatabaseEntity> CreateUserAsync(string? displayName = null, string? email = null)
    {
        var id = NextId();
        var user = new UserDatabaseEntity
        {
            PublicId = $"u_{id}",
            DisplayName = displayName ?? $"User {id}",
            Email = email ?? $"user_{id}@test.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<CommunityDatabaseEntity> CreateCommunityAsync(string? name = null, string? slug = null)
    {
        var id = NextId();
        var community = new CommunityDatabaseEntity
        {
            PublicId = $"comm_{id}",
            Name = name ?? $"Community {id}",
            Slug = slug ?? $"community-{id}",
            CreatedAt = DateTime.UtcNow,
            VisibilityId = (int)CommunityVisibilityEnum.PublicListed
        };
        _context.Communities.Add(community);
        await _context.SaveChangesAsync();
        return community;
    }

    public async Task<HubDatabaseEntity> CreateHubAsync(int communityId, string? name = null, string? slug = null)
    {
        var id = NextId();
        var hub = new HubDatabaseEntity
        {
            PublicId = $"hub_{id}",
            CommunityId = communityId,
            Name = name ?? $"Hub {id}",
            Slug = slug ?? $"hub-{id}",
            CreatedAt = DateTime.UtcNow
        };
        _context.Hubs.Add(hub);
        await _context.SaveChangesAsync();
        return hub;
    }

    public async Task<SpaceDatabaseEntity> CreateSpaceAsync(int hubId, string? name = null, string? slug = null)
    {
        var id = NextId();
        var space = new SpaceDatabaseEntity
        {
            PublicId = $"space_{id}",
            HubId = hubId,
            Name = name ?? $"Space {id}",
            Slug = slug ?? $"space-{id}",
            CreatedAt = DateTime.UtcNow
        };
        _context.Spaces.Add(space);
        await _context.SaveChangesAsync();
        return space;
    }

    public async Task<DiscussionDatabaseEntity> CreateDiscussionAsync(int spaceId, int createdByUserId, string? title = null, string? slug = null)
    {
        var id = NextId();
        var discussion = new DiscussionDatabaseEntity
        {
            PublicId = $"disc_{id}",
            SpaceId = spaceId,
            CreatedByUserId = createdByUserId,
            Title = title ?? $"Discussion {id}",
            Slug = slug ?? $"discussion-{id}",
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();
        return discussion;
    }

    public async Task<PostDatabaseEntity> CreatePostAsync(int discussionId, int createdByUserId, string? content = null, bool isFirstPost = false)
    {
        var id = NextId();
        var post = new PostDatabaseEntity
        {
            PublicId = $"post_{id}",
            DiscussionId = discussionId,
            CreatedByUserId = createdByUserId,
            Content = content ?? $"Post content {id}",
            CreatedAt = DateTime.UtcNow,
            IsFirstPost = isFirstPost
        };
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task<UserRoleDatabaseEntity> AssignRoleAsync(
        int userId,
        UserRoleTypeEnum roleType,
        int assignedByUserId,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null)
    {
        var role = new UserRoleDatabaseEntity
        {
            PublicId = $"role_{NextId()}",
            UserId = userId,
            RoleId = (int)roleType,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = DateTime.UtcNow
        };
        _context.UserRoles.Add(role);
        await _context.SaveChangesAsync();
        return role;
    }

    public async Task<PermissionDatabaseEntity> CreatePermissionAsync(string name, string category = "General")
    {
        var permission = new PermissionDatabaseEntity
        {
            PublicId = $"perm_{NextId()}",
            Name = name,
            Category = category,
            CreatedAt = DateTime.UtcNow
        };
        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();
        return permission;
    }

    public async Task<RolePermissionDatabaseEntity> AssignPermissionToRoleAsync(int roleId, int permissionId, int? grantedById = null)
    {
        var rp = new RolePermissionDatabaseEntity
        {
            RoleId = roleId,
            PermissionId = permissionId,
            GrantedAt = DateTime.UtcNow,
            GrantedById = grantedById
        };
        _context.RolePermissions.Add(rp);
        await _context.SaveChangesAsync();
        return rp;
    }

    public async Task<TemporaryRoleElevationDatabaseEntity> CreateTemporaryElevationAsync(
        int userId,
        string roleType,
        string scope,
        int scopeId,
        int grantedById,
        DateTime? expiresAt = null,
        string? reason = null)
    {
        var elevation = new TemporaryRoleElevationDatabaseEntity
        {
            PublicId = $"temp_{NextId()}",
            UserId = userId,
            RoleType = roleType,
            Scope = scope,
            ScopeId = scopeId,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(4),
            Reason = reason ?? "Test elevation",
            GrantedById = grantedById,
            CreatedAt = DateTime.UtcNow
        };
        _context.TemporaryRoleElevations.Add(elevation);
        await _context.SaveChangesAsync();
        return elevation;
    }

    public async Task<ReportReasonDatabaseEntity> CreateReportReasonAsync(string? name = null, string? description = null)
    {
        var id = NextId();
        var reason = new ReportReasonDatabaseEntity
        {
            PublicId = $"rr_{id}",
            Name = name ?? $"Report Reason {id}",
            Description = description,
            CreatedAt = DateTime.UtcNow,
            DisplayOrder = 0,
            IsDeleted = false
        };
        _context.ReportReasons.Add(reason);
        await _context.SaveChangesAsync();
        return reason;
    }

    public async Task<ReportDatabaseEntity> CreateReportAsync(
        int reporterUserId,
        int reasonId,
        int? reportedPostId = null,
        int? reportedDiscussionId = null,
        int? reportedUserId = null,
        string? details = null,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null)
    {
        var id = NextId();
        var report = new ReportDatabaseEntity
        {
            PublicId = $"rpt_{id}",
            ReporterUserId = reporterUserId,
            ReasonId = reasonId,
            ReportedPostId = reportedPostId,
            ReportedDiscussionId = reportedDiscussionId,
            ReportedUserId = reportedUserId,
            Details = details,
            StatusId = 1, // Pending
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<ReportCommentDatabaseEntity> CreateReportCommentAsync(int reportId, int authorUserId, string? content = null)
    {
        var id = NextId();
        var comment = new ReportCommentDatabaseEntity
        {
            PublicId = $"rc_{id}",
            ReportId = reportId,
            AuthorUserId = authorUserId,
            Content = content ?? $"Report comment {id}",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _context.ReportComments.Add(comment);
        await _context.SaveChangesAsync();
        return comment;
    }

    public async Task<ModerationLogDatabaseEntity> CreateModerationLogAsync(
        int actorUserId,
        int actionId,
        int? targetPostId = null,
        int? targetDiscussionId = null,
        int? targetUserId = null,
        int? communityId = null,
        int? hubId = null,
        int? spaceId = null,
        string? reason = null,
        string? details = null)
    {
        var id = NextId();
        var log = new ModerationLogDatabaseEntity
        {
            PublicId = $"ml_{id}",
            ActorUserId = actorUserId,
            ActionId = actionId,
            TargetPostId = targetPostId,
            TargetDiscussionId = targetDiscussionId,
            TargetUserId = targetUserId,
            CommunityId = communityId,
            HubId = hubId,
            SpaceId = spaceId,
            Reason = reason,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };
        _context.ModerationLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<AchievementDatabaseEntity> CreateAchievementAsync(
        string? name = null,
        string? slug = null,
        int categoryId = 1,
        int tierLevel = 1,
        bool isActive = true)
    {
        var id = NextId();
        var achievement = new AchievementDatabaseEntity
        {
            PublicId = $"ach_{id}",
            Name = name ?? $"Achievement {id}",
            Slug = slug ?? $"achievement-{id}",
            Description = "Test achievement",
            CreatedAt = DateTime.UtcNow,
            CategoryId = categoryId,
            TierLevel = tierLevel,
            Points = 10,
            IsSecret = false,
            IsActive = isActive,
            DisplayOrder = 0,
            RequirementTypeId = 1,
            RequirementConfig = "{}"
        };
        _context.Achievements.Add(achievement);
        await _context.SaveChangesAsync();
        return achievement;
    }

    public async Task<UserAchievementDatabaseEntity> CreateUserAchievementAsync(int userId, int achievementId)
    {
        var id = NextId();
        var userAchievement = new UserAchievementDatabaseEntity
        {
            PublicId = $"ua_{id}",
            UserId = userId,
            AchievementId = achievementId,
            EarnedAt = DateTime.UtcNow,
            IsDisplayed = false,
            DisplayOrder = 0,
            NotificationSent = false
        };
        _context.UserAchievements.Add(userAchievement);
        await _context.SaveChangesAsync();
        return userAchievement;
    }

    public async Task<UserAchievementProgressDatabaseEntity> CreateAchievementProgressAsync(
        int userId,
        int achievementId,
        int currentValue = 0,
        int targetValue = 10)
    {
        var progress = new UserAchievementProgressDatabaseEntity
        {
            UserId = userId,
            AchievementId = achievementId,
            CurrentValue = currentValue,
            TargetValue = targetValue,
            LastUpdated = DateTime.UtcNow
        };
        _context.UserAchievementProgress.Add(progress);
        await _context.SaveChangesAsync();
        return progress;
    }

    public async Task<RefreshTokenDatabaseEntity> CreateRefreshTokenAsync(
        int userId,
        string? tokenValue = null,
        DateTime? expiresAt = null)
    {
        var id = NextId();
        var token = new RefreshTokenDatabaseEntity
        {
            PublicId = $"rt_{id}",
            UserId = userId,
            TokenValue = tokenValue ?? Guid.NewGuid().ToString("N"),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    public async Task<MentionDatabaseEntity> CreateMentionAsync(int postId, int mentionedUserId)
    {
        var id = NextId();
        var mention = new MentionDatabaseEntity
        {
            PublicId = $"men_{id}",
            PostId = postId,
            MentionedUserId = mentionedUserId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Mentions.Add(mention);
        await _context.SaveChangesAsync();
        return mention;
    }

    public async Task<PostRevisionDatabaseEntity> CreatePostRevisionAsync(
        int postId,
        string postPublicId,
        int editedByUserId,
        string editedByUserPublicId,
        string? content = null,
        int revisionNumber = 1)
    {
        var id = NextId();
        var revision = new PostRevisionDatabaseEntity
        {
            PostId = postId,
            PostPublicId = postPublicId,
            EditedByUserId = editedByUserId,
            EditedByUserPublicId = editedByUserPublicId,
            Content = content ?? $"Revised content {id}",
            CreatedAt = DateTime.UtcNow,
            RevisionNumber = revisionNumber
        };
        _context.PostRevisions.Add(revision);
        await _context.SaveChangesAsync();
        return revision;
    }

    public async Task<(
        UserDatabaseEntity User,
        CommunityDatabaseEntity Community,
        HubDatabaseEntity Hub,
        SpaceDatabaseEntity Space,
        DiscussionDatabaseEntity Discussion,
        PostDatabaseEntity Post)> CreateFullHierarchyAsync()
    {
        var user = await CreateUserAsync();
        var community = await CreateCommunityAsync();
        var hub = await CreateHubAsync(community.Id);
        var space = await CreateSpaceAsync(hub.Id);
        var discussion = await CreateDiscussionAsync(space.Id, user.Id);
        var post = await CreatePostAsync(discussion.Id, user.Id, isFirstPost: true);
        return (user, community, hub, space, discussion, post);
    }
}
