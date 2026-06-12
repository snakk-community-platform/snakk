namespace Snakk.Web.Models.Bff;

public record BffDiscussionSearchItem
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string HubName { get; init; }
    public required string SpaceName { get; init; }
    public required int PostCount { get; init; }
    public required int ReactionCount { get; init; }
    public required string CreatedAt { get; init; }
    public required string? LastActivityAt { get; init; }
}

public record BffPostSearchItem
{
    public required string Url { get; init; }
    public required string DiscussionTitle { get; init; }
    public required string HubName { get; init; }
    public required string SpaceName { get; init; }
    public required string SpaceGradientCss { get; init; }
    public required string ContentPreview { get; init; }
    public required string CreatedAt { get; init; }
    public required string AuthorPublicId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public required string AuthorAvatarUrl { get; init; }
    public required string PostPublicId { get; init; }
}

public record BffSearchResponse<T>
{
    public required List<T> Items { get; init; }
    public required int Offset { get; init; }
    public required int PageSize { get; init; }
    public required bool HasMoreItems { get; init; }
}
