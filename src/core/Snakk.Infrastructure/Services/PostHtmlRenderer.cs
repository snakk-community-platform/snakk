namespace Snakk.Infrastructure.Services;

using System.Net;
using System.Text;
using Snakk.Domain.Entities;
using Snakk.Infrastructure.Rendering;

/// <summary>
/// Renders Post entities to HTML fragments for realtime updates.
/// Matches the structure in Detail.cshtml to ensure consistency.
/// </summary>
public class PostHtmlRenderer : IPostHtmlRenderer
{
    private readonly IMarkupParser _markupParser;

    public PostHtmlRenderer(IMarkupParser markupParser)
    {
        _markupParser = markupParser;
    }

    public string RenderPostCard(Post post, User author, string hubSlug, string spaceSlug, string discussionSlug, string tempUserId)
    {
        var sb = new StringBuilder();
        var renderedContent = _markupParser.ToHtml(post.Content);
        var editedIndicator = post.EditedAt.HasValue ? "<span class=\"ml-2\">(edited)</span>" : "";
        var firstPostBadge = post.IsFirstPost ? "<div class=\"badge badge-info mb-2\">Original Post</div>" : "";
        var slugWithId = $"{discussionSlug}~{post.PublicId.Value}";

        sb.Append($@"
<div id=""post-{post.PublicId.Value}"" class=""card bg-base-100 shadow-md mb-4"">
    <div class=""card-body"">
        <div class=""flex justify-between items-start"">
            <div class=""flex-1"">
                {firstPostBadge}
                <div id=""post-content-{post.PublicId.Value}"" class=""prose prose-sm max-w-none"">
                    {renderedContent}
                </div>
                <div class=""text-sm opacity-70 mt-2"">
                    Posted {post.CreatedAt:MMM dd, yyyy HH:mm}
                    {editedIndicator}
                </div>
            </div>
            <div class=""dropdown dropdown-end"">
                <button tabindex=""0"" class=""btn btn-ghost btn-sm"">⋮</button>
                <ul tabindex=""0"" class=""dropdown-content menu p-2 shadow bg-base-200 rounded-box w-52 z-10"">
                    <li><button hx-get=""/api/posts/{post.PublicId.Value}/history"" hx-target=""#history-modal-content"" hx-swap=""innerHTML"" onclick=""history_modal.showModal()"">View History</button></li>
                    <li><button onclick=""editPost('{post.PublicId.Value}', '{tempUserId}')"">Edit</button></li>
                    <li><button hx-delete=""/api/posts/{post.PublicId.Value}?userId={tempUserId}"" hx-target=""#post-{post.PublicId.Value}"" hx-swap=""outerHTML"" hx-confirm=""Are you sure you want to delete this post?"">Delete</button></li>
                </ul>
            </div>
        </div>
    </div>
</div>");

        return sb.ToString();
    }

    public string RenderPostContent(Post post)
    {
        var renderedContent = _markupParser.ToHtml(post.Content);
        var editedIndicator = post.EditedAt.HasValue ? "<span class=\"ml-2\">(edited)</span>" : "";

        return $@"
<div class=""prose prose-sm max-w-none"">{renderedContent}</div>
<div class=""text-sm opacity-70 mt-2"">
    Posted {post.CreatedAt:MMM dd, yyyy HH:mm}
    {editedIndicator}
</div>";
    }

    public string RenderTombstone()
    {
        return @"
<div class=""card bg-base-100 shadow-md mb-4"">
    <div class=""card-body"">
        <p class=""text-base-content/50 italic"">[This post has been deleted]</p>
    </div>
</div>";
    }
}
