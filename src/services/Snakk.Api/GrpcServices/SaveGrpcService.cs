using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Api.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Protos.Save;

namespace Snakk.Api.GrpcServices;

public class SaveGrpcService(
    ISaveRepository saveRepository,
    ISearchRepository searchRepository,
    ICurrentUserService currentUser,
    IFileStorage fileStorage,
    IUserRepository userRepository,
    IDiscussionReadStateRepository readStateRepository) : SaveService.SaveServiceBase
{
    public override async Task<SaveToggleResponse> ToggleSaveDiscussion(ToggleSaveDiscussionRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var isSaved = await saveRepository.ToggleSaveDiscussionAsync(userId, request.DiscussionId, ct);
        return new SaveToggleResponse { IsSaved = isSaved };
    }

    public override async Task<SaveToggleResponse> ToggleSavePost(ToggleSavePostRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var isSaved = await saveRepository.ToggleSavePostAsync(userId, request.PostId, ct);
        return new SaveToggleResponse { IsSaved = isSaved };
    }

    public override async Task<SavedIdsResponse> GetSavedDiscussionIds(GetSavedIdsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();
        if (userId is null) return new SavedIdsResponse();

        var ids = await saveRepository.GetSavedDiscussionIdsAsync(userId, ct);
        var response = new SavedIdsResponse();
        response.PublicIds.AddRange(ids);
        return response;
    }

    public override async Task<SavedIdsResponse> GetSavedPostIds(GetSavedIdsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();
        if (userId is null) return new SavedIdsResponse();

        var ids = await saveRepository.GetSavedPostIdsAsync(userId, ct);
        var response = new SavedIdsResponse();
        response.PublicIds.AddRange(ids);
        return response;
    }

    public override async Task<Snakk.Protos.Discussion.PagedRecentDiscussionList> GetSavedDiscussions(GetSavedDiscussionsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var result = await saveRepository.GetSavedDiscussionsAsync(userId, request.Offset, pageSize, ct);

        var publicIds = result.Items.Select(d => d.PublicId).ToList();
        var previewMap = await searchRepository.FetchPreviewsByPublicIdsAsync(publicIds, ct);

        var enrichedItems = result.Items.Select(d =>
            previewMap.TryGetValue(d.PublicId, out var preview) ? d with { Preview = preview } : d).ToList();

        var enrichedResult = result with { Items = enrichedItems };

        var authorIds = enrichedItems
            .SelectMany(d => new[] { d.CreatedByUserPublicId, d.LastReplierPublicId })
            .OfType<string>()
            .Distinct()
            .ToList();
        var authorSlugs = await userRepository.GetSlugsByPublicIdsAsync(authorIds, ct);

        Dictionary<string, int>? unreadCounts = null;
        if (publicIds.Count > 0)
        {
            var lastReadAtTask = readStateRepository.GetLastReadAtByDiscussionAsync(userId, publicIds, ct);
            var lastVisitAtTask = userRepository.GetLastVisitAtAsync(userId, ct);
            await Task.WhenAll(lastReadAtTask, lastVisitAtTask);
            var lastReadAtMap = lastReadAtTask.Result;
            var lastVisitAt = lastVisitAtTask.Result;

            var cutoffs = BuildCutoffMap(publicIds, lastReadAtMap, lastVisitAt);
            if (cutoffs.Count > 0)
                unreadCounts = await readStateRepository.GetUnreadPostCountsAsync(cutoffs, ct);
        }

        return PagedDiscussionListMapper.Build(enrichedResult, fileStorage, authorSlugs, unreadCounts);
    }

    private static Dictionary<string, DateTime> BuildCutoffMap(
        List<string> discussionIds,
        Dictionary<string, DateTime> lastReadAtMap,
        DateTime? lastVisitAt)
    {
        var cutoffs = new Dictionary<string, DateTime>();
        foreach (var id in discussionIds)
        {
            if (lastReadAtMap.TryGetValue(id, out var perDiscussion))
                cutoffs[id] = perDiscussion;
            else if (lastVisitAt.HasValue)
                cutoffs[id] = lastVisitAt.Value;
            // No fallback available — skip this discussion (no badge)
        }
        return cutoffs;
    }

    public override async Task<PagedSavedPostsList> GetSavedPosts(GetSavedPostsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = RequireAuth();
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var result = await saveRepository.GetSavedPostsAsync(userId, request.Offset, pageSize, ct);

        var response = new PagedSavedPostsList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var p in result.Items)
        {
            var item = new SavedPostInfo
            {
                PublicId = p.PublicId,
                ContentExcerpt = p.ContentExcerpt,
                CreatedAt = ToTimestamp(p.CreatedAt),
                DiscussionPublicId = p.DiscussionPublicId,
                DiscussionTitle = p.DiscussionTitle,
                DiscussionSlug = p.DiscussionSlug,
                SpaceSlug = p.SpaceSlug,
                SpaceName = p.SpaceName,
                HubSlug = p.HubSlug,
                HubName = p.HubName,
                CommunitySlug = p.CommunitySlug,
                // Coalesce: GDPR-anonymized authors have null ids; proto setters throw on null
                AuthorPublicId = p.AuthorPublicId ?? "",
                AuthorDisplayName = p.AuthorDisplayName ?? "",
                SavedAt = ToTimestamp(p.SavedAt)
            };

            if (p.AuthorAvatarFileName is not null)
                item.AuthorAvatarFileName = p.AuthorAvatarFileName;
            if (p.AuthorSlug is not null)
                item.AuthorSlug = p.AuthorSlug;

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<SaveCountsResponse> GetSaveCounts(GetSaveCountsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = TryGetAuthUserId();
        if (userId is null) return new SaveCountsResponse();

        var (discussionCount, postCount) = await saveRepository.GetSaveCountsAsync(userId, ct);
        return new SaveCountsResponse
        {
            DiscussionCount = discussionCount,
            PostCount = postCount
        };
    }

    private string? TryGetAuthUserId() =>
        currentUser.IsAuthenticated() ? currentUser.GetCurrentUserId() : null;

    private string RequireAuth() =>
        TryGetAuthUserId()
        ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
