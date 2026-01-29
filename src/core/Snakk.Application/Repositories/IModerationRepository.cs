namespace Snakk.Application.Repositories;

using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

/// <summary>
/// Unified moderation repository interface for role management, bans, reports, and moderation logs
/// </summary>
public interface IModerationRepository
{
    // ==================== Role Management ====================
    
    Task<UserRoleDto?> GetRoleByPublicIdAsync(string publicId);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForUserAsync(string userPublicId);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForCommunityAsync(string communityPublicId);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForHubAsync(string hubPublicId);
    Task<IEnumerable<UserRoleDto>> GetActiveRolesForSpaceAsync(string spacePublicId);
    Task<IEnumerable<UserRoleDto>> GetGlobalAdminsAsync();
    
    Task<UserRoleDto> AssignRoleAsync(
        string targetUserPublicId,
        UserRoleType roleType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string assignedByUserPublicId);
    
    Task RevokeRoleAsync(string rolePublicId, string revokedByUserPublicId);
    
    Task<bool> CanModerateAsync(string userPublicId, string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null);
    Task<bool> CanAdministerAsync(string userPublicId, string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null);
    
    // ==================== Ban Management ====================
    
    Task<UserBanDto?> GetBanByPublicIdAsync(string publicId);
    Task<IEnumerable<UserBanDto>> GetActiveBansForUserAsync(string userPublicId);
    Task<UserBanDto?> GetActiveBanForScopeAsync(string userPublicId, string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null);
    Task<bool> IsUserBannedAsync(string userPublicId, string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null);
    
    Task<UserBanDto> BanUserAsync(
        string targetUserPublicId,
        BanType banType,
        string? communityPublicId,
        string? hubPublicId,
        string? spacePublicId,
        string? reason,
        DateTime? expiresAt,
        string bannedByUserPublicId);
    
    Task UnbanUserAsync(string banPublicId, string unbannedByUserPublicId);
    
    // ==================== Report Management ====================
    
    Task<ReportDto?> GetReportByPublicIdAsync(string publicId);
    Task<ReportDetailDto?> GetReportDetailByPublicIdAsync(string publicId);
    Task<PagedResult<ReportListDto>> GetReportsForCommunityAsync(string communityPublicId, string? status, int offset, int pageSize);
    Task<PagedResult<ReportListDto>> GetReportsForHubAsync(string hubPublicId, string? status, int offset, int pageSize);
    Task<PagedResult<ReportListDto>> GetReportsForSpaceAsync(string spacePublicId, string? status, int offset, int pageSize);
    Task<PagedResult<ReportListDto>> GetReportsForModeratorAsync(string moderatorPublicId, string? status, int offset, int pageSize);
    Task<int> GetPendingReportCountForModeratorAsync(string moderatorPublicId);
    
    Task<ReportDto> CreateReportAsync(
        string reporterUserPublicId,
        string? reportedPostPublicId,
        string? reportedDiscussionPublicId,
        string? reportedUserPublicId,
        string? reasonPublicId,
        string? details);
    
    Task ResolveReportAsync(string reportPublicId, string resolvedByUserPublicId, string? resolutionNote, bool dismiss);
    
    Task<ReportCommentDto> AddReportCommentAsync(string reportPublicId, string authorUserPublicId, string content);
    
    // ==================== Report Reasons ====================
    
    Task<IEnumerable<ReportReasonDto>> GetReportReasonsForScopeAsync(string? communityPublicId = null, string? hubPublicId = null, string? spacePublicId = null);
    Task<IEnumerable<ReportReasonDto>> GetGlobalReportReasonsAsync();
    
    // ==================== Moderation Log ====================
    
    Task<PagedResult<ModerationLogDto>> GetModerationLogForCommunityAsync(string communityPublicId, int offset, int pageSize);
    Task<PagedResult<ModerationLogDto>> GetModerationLogForHubAsync(string hubPublicId, int offset, int pageSize);
    Task<PagedResult<ModerationLogDto>> GetModerationLogForSpaceAsync(string spacePublicId, int offset, int pageSize);
    Task<PagedResult<ModerationLogDto>> GetModerationLogByActorAsync(string actorUserPublicId, int offset, int pageSize);
    
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
        string? reason = null);
    
    // ==================== Content Moderation ====================
    
    Task ModeratorDeletePostAsync(string postPublicId, string moderatorPublicId, string? reason);
    Task ModeratorDeleteDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason);
    Task LockDiscussionAsync(string discussionPublicId, string moderatorPublicId, string? reason);
    Task UnlockDiscussionAsync(string discussionPublicId, string moderatorPublicId);
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
