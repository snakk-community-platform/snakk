namespace Snakk.Web.Models.Bff;

/// <summary>
/// BFF content-related responses (discussions, posts, previews, etc.)
/// </summary>
public record BffDiscussionPreviewResponse
{
    public required string Content { get; init; }
}

public record BffMarkupPreviewResponse
{
    public required string Html { get; init; }
}
