namespace Snakk.Application.DTOs.Responses;

public record RoleAssignedResponse(string PublicId, string Role, DateTime AssignedAt);

public record UserRolesResponse(IEnumerable<UserRoleItemResponse> Items);

public record UserRoleItemResponse(
    string PublicId,
    string Role,
    string? CommunityId,
    string? CommunityName,
    string? HubId,
    string? HubName,
    string? SpaceId,
    string? SpaceName,
    DateTime AssignedAt);

public record BanCreatedResponse(
    string PublicId,
    string BanType,
    DateTime BannedAt,
    DateTime? ExpiresAt);

public record BanStatusResponse(bool IsBanned);

public record ReportCreatedResponse(string PublicId, string Status, DateTime CreatedAt);

public record ReportReasonsResponse(IEnumerable<ReportReasonResponse> Items);

public record ReportReasonResponse(string PublicId, string Name, string? Description);

public record ReportCommentCreatedResponse(string PublicId, string Content, DateTime CreatedAt);

public record FollowLevelResponse(string Level);

// Admin moderation
public record AdminBanResponse(
    string PublicId,
    string BanType,
    DateTime BannedAt,
    DateTime? ExpiresAt,
    string? Reason);

public record AdminRoleResponse(string PublicId, string Role, DateTime AssignedAt);

public record AdminReportListResponse(
    IEnumerable<AdminReportItemResponse> Reports,
    int Total,
    int Page,
    int PageSize);

public record AdminReportItemResponse(
    string Id,
    string? ReporterUsername,
    string? ReportedUsername,
    string ContentType,
    string? Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolverUsername,
    string? Details);

public record AdminReportDetailResponse(
    string Id,
    string ReporterId,
    string ReporterUsername,
    string? ReportedUserId,
    string? ReportedUsername,
    string? PostId,
    string? DiscussionId,
    string? Reason,
    string? ReasonDescription,
    string? Details,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolverId,
    string? ResolverUsername,
    string? ResolutionNote);

public record AdminModerationLogResponse(
    IEnumerable<AdminModerationLogItemResponse> Actions,
    int Total,
    int Page,
    int PageSize);

public record AdminModerationLogItemResponse(
    string Id,
    string ActionType,
    string? ModeratorUsername,
    string TargetType,
    string TargetId,
    string? Reason,
    string? Details,
    DateTime CreatedAt,
    string? CommunityName,
    string? HubName,
    string? SpaceName);

public record AdminActiveBansResponse(
    IEnumerable<AdminActiveBanItemResponse> Bans,
    int Total,
    int Page,
    int PageSize);

public record AdminActiveBanItemResponse(
    string UserId,
    string Username,
    string? Reason,
    DateTime BannedAt,
    DateTime? ExpiresAt,
    string? BannedByUsername);
