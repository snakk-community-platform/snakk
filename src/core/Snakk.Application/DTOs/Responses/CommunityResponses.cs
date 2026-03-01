namespace Snakk.Application.DTOs.Responses;

public record CommunityResponse(
    string PublicId,
    string Name,
    string Slug,
    string? Description,
    string Visibility,
    bool ExposeToPlatformFeed,
    DateTime CreatedAt,
    DateTime? LastModifiedAt = null);
