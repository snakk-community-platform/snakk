namespace Snakk.PublicApi.Endpoints;

using Grpc.Core;
using Snakk.Protos.Discussion;
using Snakk.PublicApi.Models;
using ProtoImagesPreview = Snakk.Protos.Discussion.ImagesPreview;
using ProtoPollPreview = Snakk.Protos.Discussion.PollPreview;
using ModelImagesPreview = Snakk.PublicApi.Models.ImagesPreview;
using ModelImagePreviewItem = Snakk.PublicApi.Models.ImagePreviewItem;
using ModelPollPreview = Snakk.PublicApi.Models.PollPreview;
using ModelPollPreviewOption = Snakk.PublicApi.Models.PollPreviewOption;

public static class DiscussionEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/discussions")
            .RequireRateLimiting("public-api");

        group.MapGet("/{publicId}", GetDiscussion);
        group.MapGet("/recent", GetRecent);
        group.MapGet("/trending", GetTrending);
        group.MapGet("/top", GetTop);
    }

    private static async Task<IResult> GetDiscussion(
        string publicId,
        DiscussionService.DiscussionServiceClient client)
    {
        try
        {
            var reply = await client.GetDiscussionAsync(new GetDiscussionRequest { PublicId = publicId });
            return Results.Ok(MapDiscussion(reply));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { return Results.NotFound(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied) { return Results.Forbid(); }
        catch (RpcException) { return Results.Problem(statusCode: 500); }
    }

    private static async Task<IResult> GetRecent(
        DiscussionService.DiscussionServiceClient client,
        string? cursor = null,
        int limit = 20,
        string? community = null,
        string? hub = null,
        string? space = null,
        string? author = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        try
        {
            var request = new GetRecentDiscussionsRequest
            {
                PageSize = limit,
                ViewerAllowsAdult = false
            };
            if (cursor is not null) request.Cursor = cursor;
            if (community is not null) request.CommunityId = community;
            if (hub is not null) request.HubId = hub;
            if (space is not null) request.SpaceId = space;
            if (author is not null) request.AuthorId = author;

            var reply = await client.GetRecentDiscussionsAsync(request);
            return Results.Ok(MapPaged(reply));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { return Results.NotFound(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied) { return Results.Forbid(); }
        catch (RpcException) { return Results.Problem(statusCode: 500); }
    }

    private static async Task<IResult> GetTrending(
        DiscussionService.DiscussionServiceClient client,
        string? cursor = null,
        int limit = 20,
        string? community = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        try
        {
            var request = new GetTrendingDiscussionsRequest
            {
                PageSize = limit,
                ViewerAllowsAdult = false
            };
            if (community is not null) request.CommunityId = community;

            var reply = await client.GetTrendingDiscussionsAsync(request);
            return Results.Ok(MapPaged(reply));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { return Results.NotFound(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied) { return Results.Forbid(); }
        catch (RpcException) { return Results.Problem(statusCode: 500); }
    }

    private static async Task<IResult> GetTop(
        DiscussionService.DiscussionServiceClient client,
        int page = 0,
        int pageSize = 20,
        string? community = null,
        string timePeriod = "week")
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        try
        {
            var request = new GetTopDiscussionsRequest
            {
                Offset = page * pageSize,
                PageSize = pageSize,
                TimePeriod = timePeriod,
                ViewerAllowsAdult = false
            };
            if (community is not null) request.CommunityId = community;

            var reply = await client.GetTopDiscussionsAsync(request);
            return Results.Ok(MapPaged(reply));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { return Results.NotFound(); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied) { return Results.Forbid(); }
        catch (RpcException) { return Results.Problem(statusCode: 500); }
    }

    private static DiscussionResponse MapDiscussion(DiscussionInfo d) => new(
        Id: d.PublicId,
        Title: d.Title,
        Slug: d.Slug,
        Type: d.Type.ToString(),
        SpaceId: d.SpaceId,
        CreatedAt: d.CreatedAt.ToDateTimeOffset(),
        LastActivityAt: d.LastActivityAt?.ToDateTimeOffset(),
        IsPinned: d.IsPinned,
        IsLocked: d.IsLocked,
        IsAdult: d.IsAdult,
        PostCount: d.PostCount);

    private static PagedResponse<DiscussionListItem> MapPaged(PagedRecentDiscussionList reply) => new(
        Items: reply.Items.Select(MapListItem).ToList(),
        HasMore: reply.HasMoreItems,
        NextCursor: reply.HasNextCursor ? reply.NextCursor : null);

    private static DiscussionListItem MapListItem(RecentDiscussionInfo d) => new(
        Id: d.PublicId,
        Title: d.Title,
        Slug: d.Slug,
        Type: d.Type.ToString(),
        CreatedAt: d.CreatedAt.ToDateTimeOffset(),
        LastActivityAt: d.LastActivityAt?.ToDateTimeOffset(),
        IsPinned: d.IsPinned,
        IsLocked: d.IsLocked,
        IsAdult: d.IsAdult,
        PostCount: d.PostCount,
        ReactionCount: d.ReactionCount,
        Author: d.Author is { } a ? new AuthorSummary(a.PublicId, a.DisplayName, a.HasAvatarUrl ? a.AvatarUrl : null, a.HasAvatarMicroUrl ? a.AvatarMicroUrl : null) : null,
        Space: d.Space is { } s ? new EntitySummary(s.PublicId, s.Slug, s.Name) : null,
        Hub: d.Hub is { } h ? new EntitySummary(h.PublicId, h.Slug, h.Name) : null,
        Community: d.Community is { } c ? new EntitySummary(c.PublicId, c.Slug, c.Name) : null,
        Images: d.Preview?.Images != null ? MapImages(d.Preview.Images) : null,
        Poll: d.Preview?.Poll != null ? MapPoll(d.Preview.Poll) : null,
        LastReplier: d.LastReplier != null ? new AuthorSummary(d.LastReplier.PublicId, d.LastReplier.DisplayName, d.LastReplier.HasAvatarUrl ? d.LastReplier.AvatarUrl : null, d.LastReplier.HasAvatarMicroUrl ? d.LastReplier.AvatarMicroUrl : null) : null,
        LastPostExcerpt: string.IsNullOrEmpty(d.LastPostExcerpt) ? null : d.LastPostExcerpt);

    private static ModelImagesPreview MapImages(ProtoImagesPreview p) => new(
        ImageCount: p.ImageCount,
        Items: p.Items.Select(i => new ModelImagePreviewItem(
            Url: i.Url,
            ThumbnailUrl: i.HasThumbnailUrl ? i.ThumbnailUrl : null,
            MediumThumbnailUrl: i.HasMediumThumbnailUrl ? i.MediumThumbnailUrl : null,
            BlurDataUri: i.HasBlurDataUri ? i.BlurDataUri : null,
            Width: i.Width,
            Height: i.Height)).ToList(),
        IsSpoiler: p.IsSpoiler,
        Layout: p.Layout);

    private static ModelPollPreview MapPoll(ProtoPollPreview p) => new(
        Options: p.Options.Select(o => new ModelPollPreviewOption(Text: o.Text, VoteCount: o.VoteCount)).ToList(),
        TotalVotes: p.TotalVotes,
        IsSecret: p.IsSecret,
        ClosesAt: p.ClosesAt?.ToDateTimeOffset());
}
