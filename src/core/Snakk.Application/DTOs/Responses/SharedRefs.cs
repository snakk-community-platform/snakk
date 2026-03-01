namespace Snakk.Application.DTOs.Responses;

/// <summary>Short reference to an entity (used in nested responses).</summary>
public record EntityRef(string PublicId, string Slug, string Name, string? AvatarUrl = null);

/// <summary>Short reference to a user/author.</summary>
public record AuthorRef(string PublicId, string DisplayName, string? AvatarUrl = null, string? Role = null, bool IsDeleted = false);

/// <summary>Short reference to a reply-to post.</summary>
public record ReplyToRef(string AuthorName, string ContentSnippet);
