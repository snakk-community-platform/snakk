namespace Snakk.Api.Models;

using Snakk.Domain.ValueObjects;

public record CreateCommunityRequest(
    string Name,
    string Slug,
    string? Description,
    CommunityVisibility? Visibility,
    bool? ExposeToPlatformFeed);
