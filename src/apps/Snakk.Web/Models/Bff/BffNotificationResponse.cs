namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF notification response - decouples frontend from API notification structure
/// </summary>
public record BffNotificationResponse
{
    public required string PublicId { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public string? Body { get; init; }
    public required bool IsRead { get; init; }
    public required string CreatedAt { get; init; }
    public string? SourceDiscussionId { get; init; }
}

public record BffNotificationsResponse
{
    public required List<BffNotificationResponse> Items { get; init; }
}

public record BffNotificationCountResponse
{
    public required int Count { get; init; }
}
