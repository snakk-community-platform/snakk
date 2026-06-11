namespace Snakk.Application.DTOs.Gdpr;

public record UserDataExportBundle(
    ExportProfileDto Profile,
    List<ExportDisplayNameHistoryDto> DisplayNameHistory,
    List<ExportSocialLinkDto> SocialLinks,
    List<ExportDiscussionDto> Discussions,
    List<ExportPostDto> Posts,
    List<ExportReactionDto> Reactions,
    List<ExportFollowDto> Follows,
    List<ExportSaveDto> Saves,
    List<ExportLoginHistoryDto> LoginHistory,
    List<ExportConsentDto> ConsentRecords,
    List<ExportDmConversationDto> DirectMessages,
    DateTime ExportedAt);

public record ExportProfileDto(
    string PublicId,
    string? DisplayName,
    string? Email,
    string? Bio,
    string? Timezone,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    DateTime? LastSeenAt);

public record ExportDisplayNameHistoryDto(
    string PreviousName,
    string NewName,
    DateTime ChangedAt);

public record ExportSocialLinkDto(
    string Platform,
    string Username);

public record ExportDiscussionDto(
    string PublicId,
    string Title,
    DateTime CreatedAt,
    bool IsDeleted);

public record ExportPostDto(
    string PublicId,
    string DiscussionPublicId,
    string? Content,
    DateTime CreatedAt,
    bool IsDeleted);

public record ExportReactionDto(
    string PostPublicId,
    string ReactionType,
    DateTime CreatedAt);

public record ExportFollowDto(
    string? FollowedUserPublicId,
    string? FollowedDiscussionPublicId,
    string? FollowedSpacePublicId,
    DateTime CreatedAt);

public record ExportSaveDto(
    string? DiscussionPublicId,
    string? PostPublicId,
    DateTime CreatedAt);

public record ExportLoginHistoryDto(
    string? IpAddress,
    string? UserAgent,
    string? DeviceHint,
    bool Success,
    DateTime CreatedAt);

public record ExportConsentDto(
    string ConsentType,
    DateTime AcceptedAt);

public record ExportDmConversationDto(
    string ConversationPublicId,
    string OtherPartyPublicId,
    string? OtherPartyDisplayName,
    DateTime CreatedAt,
    List<ExportDmMessageDto> Messages);

public record ExportDmMessageDto(
    string SenderPublicId,
    string Content,
    DateTime CreatedAt);
