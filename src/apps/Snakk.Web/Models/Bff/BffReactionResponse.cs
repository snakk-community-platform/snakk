namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF reaction-related responses
/// </summary>
public record BffReactionsResponse
{
    public Dictionary<string, int> Counts { get; init; } = new();
}

public record BffMyReactionsResponse
{
    public List<string> Reactions { get; init; } = [];
}
