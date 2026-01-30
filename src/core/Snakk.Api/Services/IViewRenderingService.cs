using Snakk.Domain.Entities;

namespace Snakk.Api.Services;

public interface IViewRenderingService
{
    /// <summary>
    /// Renders a post card for HTMX response
    /// </summary>
    string RenderPostCard(Post post, string userId);

    /// <summary>
    /// Renders post history list for HTMX modal
    /// </summary>
    string RenderPostHistory(IEnumerable<PostRevision> revisions, Dictionary<string, string> authorNames);

    /// <summary>
    /// Renders error alert for HTMX response
    /// </summary>
    string RenderError(string message);

    /// <summary>
    /// Renders success alert for HTMX response
    /// </summary>
    string RenderSuccess(string message);

    /// <summary>
    /// Renders a soft-deleted post tombstone
    /// </summary>
    string RenderDeletedPostTombstone(string postId);
}
