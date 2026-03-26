namespace Snakk.Application.DTOs.Responses;

public record BannerResponse(
    string PublicId,
    string Title,
    string Content,
    string RenderedContent,
    string Type,
    string Scope,
    string ScopeEntityId,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    bool IsDismissible,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? LastModifiedAt,
    string CreatedByUserId);

public record ActiveBannerResponse(
    string PublicId,
    string Title,
    string RenderedContent,
    string Type,
    string Scope,
    bool IsDismissible,
    int SortOrder,
    DateTime CreatedAt);
