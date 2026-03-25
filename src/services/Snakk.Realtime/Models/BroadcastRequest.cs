namespace Snakk.Realtime.Models;

public record BroadcastRequest
{
    public required string EventType { get; init; }
    public required string TargetGroup { get; init; }
    public string TargetId { get; init; } = "";
    public string HtmlContent { get; init; } = "";
    public string SwapStrategy { get; init; } = "";
    public string? PostId { get; init; }
    public Dictionary<string, int>? Counts { get; init; }
    public string? DiscussionId { get; init; }
    public string? Title { get; init; }
    public int? Delta { get; init; }
    public string? AuthorId { get; init; }
    public string? AuthorName { get; init; }
}

public record ActivityBroadcastRequest
{
    public required string ActivityType { get; init; }
    public required string TargetGroup { get; init; }
    public required object Data { get; init; }
}
