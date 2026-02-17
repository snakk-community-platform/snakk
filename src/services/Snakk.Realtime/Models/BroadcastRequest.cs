namespace Snakk.Realtime.Models;

public record BroadcastRequest
{
    public required string EventType { get; init; }
    public required string TargetGroup { get; init; }
    public required string TargetId { get; init; }
    public required string HtmlContent { get; init; }
    public required string SwapStrategy { get; init; }
    public string? PostId { get; init; }
    public Dictionary<string, int>? Counts { get; init; }
}

public record ActivityBroadcastRequest
{
    public required string ActivityType { get; init; }
    public required string TargetGroup { get; init; }
    public required object Data { get; init; }
}
