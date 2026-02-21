namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF reaction-related responses
/// </summary>
public record BffReactionsResponse
{
    public int ThumbsUp { get; init; }
    public int Heart { get; init; }
    public int Eyes { get; init; }
    public int Crazy { get; init; }
}

public record BffMyReactionResponse
{
    public string? Reaction { get; init; }
}
