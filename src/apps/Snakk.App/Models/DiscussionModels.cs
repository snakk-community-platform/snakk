namespace Snakk.App.Models;

using System.Text.Json.Serialization;

public record AuthorSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("avatar_micro_url")] string? AvatarMicroUrl = null);

public record EntitySummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name);

public record PollPreviewOption(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("vote_count")] int VoteCount);

public record PollPreview(
    [property: JsonPropertyName("options")] List<PollPreviewOption> Options,
    [property: JsonPropertyName("total_votes")] int TotalVotes,
    [property: JsonPropertyName("is_secret")] bool IsSecret,
    [property: JsonPropertyName("closes_at")] DateTimeOffset? ClosesAt = null);

public record ImagePreviewItem(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl,
    [property: JsonPropertyName("medium_thumbnail_url")] string? MediumThumbnailUrl,
    [property: JsonPropertyName("blur_data_uri")] string? BlurDataUri,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height);

public record ImagesPreview(
    [property: JsonPropertyName("image_count")] int ImageCount,
    [property: JsonPropertyName("items")] List<ImagePreviewItem> Items,
    [property: JsonPropertyName("is_spoiler")] bool IsSpoiler,
    [property: JsonPropertyName("layout")] string Layout);

public record DiscussionListItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_activity_at")] DateTimeOffset? LastActivityAt,
    [property: JsonPropertyName("is_pinned")] bool IsPinned,
    [property: JsonPropertyName("is_locked")] bool IsLocked,
    [property: JsonPropertyName("post_count")] int PostCount,
    [property: JsonPropertyName("reaction_count")] int ReactionCount,
    [property: JsonPropertyName("author")] AuthorSummary? Author,
    [property: JsonPropertyName("community")] EntitySummary? Community,
    [property: JsonPropertyName("is_adult")] bool IsAdult = false,
    [property: JsonPropertyName("space")] EntitySummary? Space = null,
    [property: JsonPropertyName("hub")] EntitySummary? Hub = null,
    [property: JsonPropertyName("images")] ImagesPreview? Images = null,
    [property: JsonPropertyName("poll")] PollPreview? Poll = null,
    [property: JsonPropertyName("last_replier")] AuthorSummary? LastReplier = null,
    [property: JsonPropertyName("last_post_excerpt")] string? LastPostExcerpt = null);

public record PagedResponse<T>(
    [property: JsonPropertyName("items")] List<T> Items,
    [property: JsonPropertyName("has_more")] bool HasMore,
    [property: JsonPropertyName("next_cursor")] string? NextCursor);
