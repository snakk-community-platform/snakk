namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF follow-related responses
/// </summary>
public record BffFollowStatusResponse
{
    public required bool IsFollowing { get; init; }
    public string? Level { get; init; }
}

public record BffFollowResultResponse
{
    public required bool IsFollowing { get; init; }
    public string? Level { get; init; }
}

public record BffFollowedEntitiesResponse
{
    public required List<string> Items { get; init; }
}
