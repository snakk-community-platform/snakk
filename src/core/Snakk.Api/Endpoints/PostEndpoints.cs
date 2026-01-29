namespace Snakk.Api.Endpoints;

using Snakk.Api.Models;
using Snakk.Application.UseCases;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Rendering;

public static class PostEndpoints
{
    public static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/posts")
            .WithTags("Posts");

        group.MapPost("/", CreatePostAsync)
            .WithName("CreatePost");

        // API endpoints under /api/posts
        var apiGroup = app.MapGroup("/api/posts")
            .WithTags("Posts API");

        apiGroup.MapPost("/{publicId}/edit", EditPostHtmxAsync)
            .WithName("EditPostHtmx");

        apiGroup.MapDelete("/{publicId}", DeletePostHtmxAsync)
            .WithName("DeletePostHtmx");

        apiGroup.MapGet("/{publicId}/history", GetPostHistoryHtmxAsync)
            .WithName("GetPostHistoryHtmx");
    }

    private static async Task<IResult> CreatePostAsync(
        CreatePostRequest request,
        PostUseCase useCase)
    {
        var result = await useCase.CreatePostAsync(
            DiscussionId.From(request.DiscussionId),
            UserId.From(request.UserId),
            request.Content,
            request.ReplyToPostId != null ? PostId.From(request.ReplyToPostId) : null);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/posts/{result.Value!.PublicId}", new
        {
            publicId = result.Value.PublicId.Value,
            content = result.Value.Content,
            createdAt = result.Value.CreatedAt,
            discussionId = result.Value.DiscussionId.Value
        });
    }

    private static async Task<IResult> EditPostHtmxAsync(
        string publicId,
        string userId,
        string content,
        PostUseCase useCase)
    {
        var result = await useCase.UpdatePostAsync(
            PostId.From(publicId),
            UserId.From(userId),
            content);

        if (!result.IsSuccess)
            return Results.Content($"<div class='alert alert-error'>{result.Error}</div>", "text/html");

        var post = result.Value!;
        var editedText = post.EditedAt.HasValue ? $"<span class='text-sm text-base-content/60'>(edited)</span>" : "";

        return Results.Content($@"
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
            </div>
        ", "text/html");
    }

    private static async Task<IResult> DeletePostHtmxAsync(
        string publicId,
        string userId,
        PostUseCase useCase)
    {
        var result = await useCase.DeletePostAsync(
            PostId.From(publicId),
            UserId.From(userId));

        if (!result.IsSuccess)
            return Results.Content($"<div class='alert alert-error'>{result.Error}</div>", "text/html");

        // Return empty content for hard delete, or tombstone for soft delete
        var post = await useCase.GetPostAsync(PostId.From(publicId));
        if (post.IsSuccess && post.Value!.IsDeleted)
        {
            return Results.Content($@"
                <div id='post-{publicId}' class='card bg-base-200 shadow-md mb-4 opacity-50'>
                    <div class='card-body'>
                        <p class='italic text-base-content/60'>[This post has been deleted]</p>
                    </div>
                </div>
            ", "text/html");
        }

        return Results.Content("", "text/html");
    }

    private static async Task<IResult> GetPostHistoryHtmxAsync(
        string publicId,
        PostUseCase useCase,
        IUserRepository userRepository)
    {
        var revisions = await useCase.GetPostHistoryAsync(PostId.From(publicId));
        var revisionList = revisions.ToList();

        if (!revisionList.Any())
            return Results.Content("<p>No edit history available.</p>", "text/html");

        // Batch fetch all editors in one query
        var editorIds = revisionList.Select(r => r.EditedByUserId).Distinct().ToList();
        var editors = await userRepository.GetByPublicIdsAsync(editorIds);
        var editorsDict = editors.ToDictionary(u => u.PublicId.Value);

        var html = "<div class='relative'>";

        for (int i = 0; i < revisionList.Count; i++)
        {
            var revision = revisionList[i];
            var userName = editorsDict.TryGetValue(revision.EditedByUserId.Value, out var user)
                ? user.DisplayName
                : "Unknown User";
            var isLatest = i == revisionList.Count - 1;
            var badgeColor = isLatest ? "badge-primary" : "badge-neutral";

            html += $@"
                <div class='relative pl-8 pb-8 {(i == revisionList.Count - 1 ? "" : "border-l-2 border-base-300")} ml-3'>
                    <div class='absolute left-0 top-0 -ml-3'>
                        <div class='badge {badgeColor} badge-lg font-bold'>{revision.RevisionNumber}</div>
                    </div>
                    <div class='card bg-base-200 shadow-sm'>
                        <div class='card-body p-4'>
                            <div class='flex items-center justify-between mb-2'>
                                <div class='font-semibold text-base'>
                                    {(isLatest ? "Current Version" : $"Version {revision.RevisionNumber}")}
                                </div>
                                {(isLatest ? "<span class='badge badge-success badge-sm'>Latest</span>" : "")}
                            </div>
                            <div class='flex items-center gap-2 text-sm text-base-content/70 mb-3'>
                                <svg xmlns='http://www.w3.org/2000/svg' class='h-4 w-4' fill='none' viewBox='0 0 24 24' stroke='currentColor'>
                                    <path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z' />
                                </svg>
                                <span class='font-medium'>{userName}</span>
                                <span class='text-base-content/50'>•</span>
                                <svg xmlns='http://www.w3.org/2000/svg' class='h-4 w-4' fill='none' viewBox='0 0 24 24' stroke='currentColor'>
                                    <path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z' />
                                </svg>
                                <span>{revision.CreatedAt:MMM dd, yyyy 'at' HH:mm}</span>
                            </div>
                            <div class='bg-base-100 p-3 rounded-lg'>
                                <p class='whitespace-pre-wrap text-sm leading-relaxed'>{revision.Content}</p>
                            </div>
                        </div>
                    </div>
                </div>
            ";
        }
        html += "</div>";

        return Results.Content(html, "text/html");
    }
}
