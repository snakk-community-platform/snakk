namespace Snakk.Application.DTOs.Responses;

public record PlatformStatsResponse(
    int HubCount,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);
