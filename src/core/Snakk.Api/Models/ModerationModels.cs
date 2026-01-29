namespace Snakk.Api.Models;

public record AssignRoleRequest(
    string TargetUserId,
    string RoleType,       // GlobalAdmin, CommunityAdmin, CommunityMod, HubMod, SpaceMod
    string? CommunityId,
    string? HubId,
    string? SpaceId);

public record BanUserRequest(
    string TargetUserId,
    string BanType,        // WriteOnly, ReadWrite
    string? CommunityId,
    string? HubId,
    string? SpaceId,
    string? Reason,
    DateTime? ExpiresAt);

public record CreateReportRequest(
    string? PostId,
    string? DiscussionId,
    string? UserId,
    string? ReasonId,
    string? Details);

public record ResolveReportRequest(
    string? ResolutionNote);

public record AddReportCommentRequest(
    string Content);

public record ModerationActionRequest(
    string? Reason);

public record CreateReportReasonRequest(
    string Name,
    string? Description,
    string? CommunityId,
    string? HubId,
    string? SpaceId,
    int DisplayOrder);
