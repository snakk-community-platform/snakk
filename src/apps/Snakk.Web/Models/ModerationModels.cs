namespace Snakk.Web.Models;

// ==================== Role Management ====================

public record UserRoleDto(
    string PublicId,
    string UserPublicId,
    string UserDisplayName,
    string Role,
    string? CommunityPublicId,
    string? CommunityName,
    string? HubPublicId,
    string? HubName,
    string? SpacePublicId,
    string? SpaceName,
    string AssignedByUserPublicId,
    string AssignedByUserDisplayName,
    DateTime AssignedAt,
    DateTime? RevokedAt);

public record UserRolesResult(IEnumerable<UserRoleDto> Items);

public record AssignRoleRequest(
    string TargetUserId,
    string RoleType,
    string? CommunityId,
    string? HubId,
    string? SpaceId);

// ==================== Ban Management ====================

public record UserBanDto(
    string PublicId,
    string UserPublicId,
    string UserDisplayName,
    string BanType,
    string? CommunityPublicId,
    string? CommunityName,
    string? HubPublicId,
    string? HubName,
    string? SpacePublicId,
    string? SpaceName,
    string? Reason,
    DateTime BannedAt,
    DateTime? ExpiresAt,
    string BannedByUserPublicId,
    string BannedByUserDisplayName,
    DateTime? UnbannedAt,
    string? UnbannedByUserPublicId,
    string? UnbannedByUserDisplayName);

public record UserBansResult(IEnumerable<UserBanDto> Items);

public record BanUserRequest(
    string TargetUserId,
    string BanType,
    string? CommunityId,
    string? HubId,
    string? SpaceId,
    string? Reason,
    DateTime? ExpiresAt);

public record BanCheckResult(
    bool IsBanned,
    UserBanDto? Ban);

// ==================== Report Management ====================

public record ReportDto(
    string PublicId,
    string Status,
    string ReporterUserPublicId,
    string? ReportedPostPublicId,
    string? ReportedDiscussionPublicId,
    string? ReportedUserPublicId,
    string? ReasonPublicId,
    string? Details,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolvedByUserPublicId,
    string? ResolutionNote);

public record ReportListDto(
    string PublicId,
    string Status,
    string ReporterUserPublicId,
    string ReporterUserDisplayName,
    string? ReportedPostPublicId,
    string? ReportedPostContentSnippet,
    string? ReportedDiscussionPublicId,
    string? ReportedDiscussionTitle,
    string? ReportedUserPublicId,
    string? ReportedUserDisplayName,
    string? ReasonName,
    string? Details,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolvedByUserPublicId,
    string? ResolvedByUserDisplayName,
    string? ResolutionNote,
    string? SpacePublicId,
    string? SpaceName,
    string? HubPublicId,
    string? HubName,
    string? CommunityPublicId,
    string? CommunityName,
    int CommentCount);

public record ReportDetailDto(
    string PublicId,
    string Status,
    string ReporterUserPublicId,
    string ReporterUserDisplayName,
    string? ReportedPostPublicId,
    string? ReportedPostContent,
    string? ReportedDiscussionPublicId,
    string? ReportedDiscussionTitle,
    string? ReportedUserPublicId,
    string? ReportedUserDisplayName,
    string? ReasonName,
    string? ReasonDescription,
    string? Details,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolvedByUserPublicId,
    string? ResolvedByUserDisplayName,
    string? ResolutionNote,
    string? SpacePublicId,
    string? SpaceName,
    string? HubPublicId,
    string? HubName,
    string? CommunityPublicId,
    string? CommunityName,
    IEnumerable<ReportCommentDto> Comments);

public record ReportCommentDto(
    string PublicId,
    string AuthorUserPublicId,
    string AuthorUserDisplayName,
    string Content,
    DateTime CreatedAt,
    DateTime? EditedAt);

public record ReportReasonDto(
    string PublicId,
    string Name,
    string? Description,
    string? CommunityPublicId,
    string? HubPublicId,
    string? SpacePublicId,
    int DisplayOrder);

public record ReportReasonsResult(IEnumerable<ReportReasonDto> Items);

public record CreateReportRequest(
    string? ReportedPostId,
    string? ReportedDiscussionId,
    string? ReportedUserId,
    string? ReasonId,
    string? Details);

public record ResolveReportRequest(
    string? ResolutionNote,
    bool Dismiss);

public record AddReportCommentRequest(string Content);

// ==================== Moderation Log ====================

public record ModerationLogDto(
    string PublicId,
    string ActorUserPublicId,
    string ActorUserDisplayName,
    string Action,
    string? TargetPostPublicId,
    string? TargetDiscussionPublicId,
    string? TargetDiscussionTitle,
    string? TargetUserPublicId,
    string? TargetUserDisplayName,
    string? CommunityPublicId,
    string? CommunityName,
    string? HubPublicId,
    string? HubName,
    string? SpacePublicId,
    string? SpaceName,
    string? Details,
    string? Reason,
    DateTime CreatedAt);

// ==================== Content Moderation ====================

public record ModerateContentRequest(string? Reason);

// ==================== Permission Checks ====================

public record CanModerateResult(bool CanModerate);
public record CanAdministerResult(bool CanAdminister);
public record PendingReportCountResult(int Count);

// ==================== Dashboard ====================

public record ModerationDashboardDto(
    int PendingReportCount,
    IEnumerable<ReportListDto> RecentReports,
    IEnumerable<ModerationLogDto> RecentLogs,
    IEnumerable<UserRoleDto> MyRoles);
