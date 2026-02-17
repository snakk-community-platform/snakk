using Snakk.Domain.Entities;

namespace Snakk.Api.Services;

public class ViewRenderingService : IViewRenderingService
{
    public string RenderPostCard(Post post, string userId)
    {
        var editedText = post.EditedAt.HasValue
            ? "<span class='text-sm text-base-content/60'>(edited)</span>"
            : "";

        return $@"
            <div id='post-{post.PublicId.Value}' class='card bg-base-100 shadow-md mb-4'>
                <div class='card-body'>
                    <div class='flex justify-between items-start'>
                        <div class='flex-1'>
                            <p class='whitespace-pre-wrap'>{post.Content}</p>
                            <div class='text-sm text-base-content/60 mt-2'>
                                Posted {post.CreatedAt:MMM dd, yyyy} {editedText}
                            </div>
                        </div>
                        <div class='dropdown dropdown-end'>
                            <button tabindex='0' class='btn btn-ghost btn-sm'>⋮</button>
                            <ul tabindex='0' class='dropdown-content menu p-2 shadow bg-base-200 rounded-box w-52'>
                                <li><button hx-get='/api/posts/{post.PublicId.Value}/history' hx-target='#history-modal-content' hx-swap='innerHTML' onclick='history_modal.showModal()'>View History</button></li>
                                <li><button onclick='editPost(""{post.PublicId.Value}"", ""{userId}"")'>Edit</button></li>
                                <li><button hx-delete='/api/posts/{post.PublicId.Value}?userId={userId}' hx-target='#post-{post.PublicId.Value}' hx-swap='outerHTML' hx-confirm='Are you sure you want to delete this post?'>Delete</button></li>
                            </ul>
                        </div>
                    </div>
                </div>
            </div>";
    }

    public string RenderPostHistory(IEnumerable<PostRevision> revisions, Dictionary<string, string> authorNames)
    {
        var itemsHtml = string.Join("", revisions.Select(r =>
        {
            var authorName = authorNames.TryGetValue(r.EditedByUserId.Value, out var name)
                ? name
                : "Deleted User";

            return $@"
                <div class='border-b border-base-300 pb-4 mb-4 last:border-0'>
                    <div class='text-sm text-base-content/60 mb-2'>
                        Edited by {authorName} on {r.CreatedAt:MMM dd, yyyy 'at' HH:mm}
                    </div>
                    <p class='whitespace-pre-wrap'>{r.Content}</p>
                </div>";
        }));

        return $@"
            <div class='space-y-4'>
                <h3 class='text-lg font-semibold'>Post Edit History</h3>
                {itemsHtml}
            </div>";
    }

    public string RenderError(string message)
    {
        return $"<div class='alert alert-error'>{message}</div>";
    }

    public string RenderSuccess(string message)
    {
        return $"<div class='alert alert-success'>{message}</div>";
    }

    public string RenderDeletedPostTombstone(string postId)
    {
        return $@"
            <div id='post-{postId}' class='card bg-base-200 shadow-md mb-4 opacity-50'>
                <div class='card-body'>
                    <p class='italic text-base-content/60'>[This post has been deleted]</p>
                </div>
            </div>";
    }
}
