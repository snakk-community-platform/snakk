namespace Snakk.Application.DTOs.Responses;

public record FollowToggleResponse(bool IsFollowing);

public record SpaceFollowToggleResponse(bool IsFollowing, string Level);

public record SpaceFollowStatusResponse(bool IsFollowing, string? Level);

public record FollowedIdsResponse(List<string> PublicIds);
