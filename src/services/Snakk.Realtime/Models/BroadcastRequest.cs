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
    public string? SpaceId { get; init; }
    public string? HubId { get; init; }
    public string? Title { get; init; }
    public int? Delta { get; init; }
    public string? AuthorId { get; init; }
    public string? AuthorName { get; init; }
    public IReadOnlyList<DebateBroadcastPosition>? DebatePositions { get; init; }
    public IReadOnlyList<PollBroadcastOption>? PollOptions { get; init; }
    public int? TotalVotes { get; init; }
    public string? LastPostExcerpt { get; init; }
    public string? LastReplierId { get; init; }
    public string? LastReplierName { get; init; }
    public string? LastReplierAvatarUrl { get; init; }
    public long? LastActivityAtUnix { get; init; }
    public int? UnreadCount { get; init; }
    public string? ConversationId { get; init; }
    public IReadOnlyList<string>? MessageIds { get; init; }
}

public record DebateBroadcastPosition(int Index, string Label, int PostCount, int Pct);
public record PollBroadcastOption(string Text, int VoteCount, int Pct);

public record ActivityBroadcastRequest
{
    public required string ActivityType { get; init; }
    public required string TargetGroup { get; init; }
    public required object Data { get; init; }
}
