namespace Snakk.Infrastructure.Services;

using Snakk.Domain.Entities;

/// <summary>
/// Infrastructure service for rendering Post entities to HTML fragments.
/// This is a presentation concern and lives in the Infrastructure layer.
/// </summary>
public interface IPostHtmlRenderer
{
    /// <summary>
    /// Renders a complete post card (for new posts added to discussion)
    /// </summary>
    string RenderPostCard(Post post, User author, string hubSlug, string spaceSlug, string discussionSlug, string tempUserId);

    /// <summary>
    /// Renders just the post content area (for edit updates)
    /// </summary>
    string RenderPostContent(Post post);

    /// <summary>
    /// Renders a tombstone message for soft-deleted posts
    /// </summary>
    string RenderTombstone();
}
