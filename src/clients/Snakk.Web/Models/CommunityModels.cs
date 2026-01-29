namespace Snakk.Web.Models;

public record CommunityDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt);

public record CommunityDetailDto(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt,
    DateTime? LastModifiedAt);
