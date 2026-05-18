namespace Snakk.Application.Repositories;

using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

/// <summary>
/// Unified moderation repository interface for role management, bans, reports, and moderation logs
/// </summary>
public interface IModerationRepository
{
    // ==================== Role Management ====================
    
    Task<UserRoleDto?> GetRoleByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForUserAsync(string userPublicId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForCommunityAsync(string communityPublicId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForHubAsync(string hubPublicId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForSpaceAsync(string spacePublicId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDto>> GetGlobalAdminsAsync(CancellationToken ct = default);

    Task<UserRoleDto> AssignRoleAsync(
        string targetUserPublicId,
        UserRoleType roleType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string assignedByUserPublicId,
        CancellationToken ct = default);

    Task RevokeRoleAsync(string rolePublicId, string revokedByUserPublicId, CancellationToken ct = default);

    Task<bool> CanModerateAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    Task<bool> CanAdministerAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);
    
    // ==================== Ban Management ====================
    
    Task<UserBanDto?> GetBanByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<IEnumerable<UserBanDto>> GetActiveBansForUserAsync(string userPublicId, CancellationToken ct = default);
    Task<UserBanDto?> GetActiveBanForScopeAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    Task<bool> IsUserBannedAsync(
        string userPublicId,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);

    Task<UserBanDto> BanUserAsync(
        string targetUserPublicId,
        BanType banType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string? reason,
        DateTime? expiresAt,
        string bannedByUserPublicId,
        CancellationToken ct = default);

    Task UnbanUserAsync(string banPublicId, string unbannedByUserPublicId, CancellationToken ct = default);
    
    // ==================== Report Management ====================
    
    Task<ReportDto?> GetReportByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<ReportDetailDto?> GetReportDetailByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(string communityPublicId, int? statusId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ReportListDto>> GetReportsForHubAsync(string hubPublicId, int? statusId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(string spacePublicId, int? statusId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ReportListDto>> GetReportsForModeratorAsync(string moderatorPublicId, int? statusId, int offset, int pageSize, CancellationToken ct = default);
    Task<int> GetPendingReportCountForModeratorAsync(string moderatorPublicId, CancellationToken ct = default);

    Task<ReportDto> CreateReportAsync(
        string reporterUserPublicId,
        string? reportedPostPublicId,
        string? reportedDiscussionPublicId,
        string? reportedUserPublicId,
        string? reasonPublicId,
        string? details,
        CancellationToken ct = default);

    Task ResolveReportAsync(string reportPublicId, string resolvedByUserPublicId, string? resolutionNote, bool dismiss, CancellationToken ct = default);

    Task<ReportCommentDto> AddReportCommentAsync(string reportPublicId, string authorUserPublicId, string content, CancellationToken ct = default);
    
    // ==================== Report Reasons ====================
    
    Task<IEnumerable<ReportReasonDto>> GetReportReasonsForScopeAsync(
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default);
    Task<IEnumerable<ReportReasonDto>> GetGlobalReportReasonsAsync(CancellationToken ct = default);

    Task<IEnumerable<ReportReasonDto>> GetReportReasonsForExactScopeAsync(
        string scopeType, string scopePublicId, CancellationToken ct = default);

    Task ReplaceReportReasonsForScopeAsync(
        string scopeType, string scopePublicId, string userPublicId,
        IEnumerable<(string Name, string? Description, int DisplayOrder)> reasons,
        CancellationToken ct = default);

    // ==================== Scope-Based Queries ====================

    Task<IEnumerable<UserBanDto>> GetActiveBansForScopeAsync(string scopeType, string scopePublicId, CancellationToken ct = default);
    Task<int> GetActiveBanCountForScopeAsync(string scopeType, string scopePublicId, CancellationToken ct = default);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForScopeAsync(string scopeType, string scopePublicId, CancellationToken ct = default);

    Task<PagedResult<ReportListDto>> GetReportsForScopeAsync(
        string scopeType, string scopePublicId, int? statusId, int offset, int pageSize, CancellationToken ct = default);

    Task<int> GetOpenReportCountForScopeAsync(string scopeType, string scopePublicId, CancellationToken ct = default);

    Task<PagedResult<ModerationLogDto>> GetModerationLogForScopeAsync(
        string scopeType, string scopePublicId, int offset, int pageSize, CancellationToken ct = default);

    // ==================== Moderation Log ====================
    
    Task<PagedResult<ModerationLogDto>> GetModerationLogForCommunityAsync(string communityPublicId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ModerationLogDto>> GetModerationLogForHubAsync(string hubPublicId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ModerationLogDto>> GetModerationLogForSpaceAsync(string spacePublicId, int offset, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ModerationLogDto>> GetModerationLogByActorAsync(string actorUserPublicId, int offset, int pageSize, CancellationToken ct = default);

    Task LogModerationActionAsync(
        string actorUserPublicId,
        string action,
        string? targetPostPublicId = null,
        string? targetDiscussionPublicId = null,
        string? targetUserPublicId = null,
        string? communityPublicId = null,
        string? hubPublicId = null,
        string? spacePublicId = null,
        string? details = null,
        string? reason = null,
        CancellationToken ct = default);

    // ==================== Content Moderation ====================

    Task ModeratorDeletePostAsync(string postPublicId, string moderatorPublicId, string? reason, CancellationToken ct = default);
    Task ModeratorDeleteDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason, CancellationToken ct = default);
    Task LockDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason, CancellationToken ct = default);
    Task UnlockDiscussionAsync(string discussionPublicId, string moderatorPublicId, CancellationToken ct = default);
}

// ==================== DTOs ====================

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
