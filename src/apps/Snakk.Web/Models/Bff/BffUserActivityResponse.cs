namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF user activity-related responses
/// </summary>
public record BffUserActivityResponse
{
    public required List<BffDailyActivityResponse> Activities { get; init; }
}

public record BffDailyActivityResponse
{
    public required string Date { get; init; }
    public required int PostCount { get; init; }
    public required int DiscussionCount { get; init; }
}

public record BffUserFollowStatusResponse
{
    public required bool IsFollowing { get; init; }
}
