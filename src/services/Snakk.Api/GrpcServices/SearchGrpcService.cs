using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Services;
using Snakk.Domain.Repositories;
using Snakk.Protos;
using Snakk.Protos.Discussion;
using Snakk.Protos.Search;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class SearchGrpcService(
    Application.Repositories.ISearchRepository searchRepository,
    ICurrentUserService currentUser,
    IFileStorage fileStorage,
    IUserRepository userRepository) : SearchService.SearchServiceBase
{
    public override async Task<PagedDiscussionSearchResults> SearchDiscussions(SearchDiscussionsRequest request, ServerCallContext context)
    {
        if (request.Query?.Length > 500)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Search query must be 500 characters or less"));

        var ct = context.CancellationToken;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await searchRepository.SearchDiscussionsAsync(
            request.Query!,
            request.HasAuthorId ? request.AuthorId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.HasHubId ? request.HubId : null,
            request.Offset,
            pageSize,
            currentUser.GetCurrentUserId(),
            request.ViewerAllowsAdult,
            request.HasSortBy ? request.SortBy : null,
            request.HasDateRange ? request.DateRange : null,
            ct);

        var discussionAuthorIds = result.Items
            .SelectMany(d => new[] { d.CreatedByUserPublicId, d.LastReplierPublicId })
            .OfType<string>()
            .Distinct()
            .ToList();
        var discussionSlugs = await userRepository.GetSlugsByPublicIdsAsync(discussionAuthorIds, ct);

        var response = new PagedDiscussionSearchResults
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var d in result.Items)
        {
            var authorRef = new AuthorRef
            {
                PublicId = d.CreatedByUserPublicId,
                DisplayName = d.CreatedByUserDisplayName,
                AvatarUrl = AvatarHelper.GetAvatarUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName, d.CreatedByUserAvatarThumbnailFileName),
                AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName)
            };
            if (discussionSlugs.TryGetValue(d.CreatedByUserPublicId, out var authorSlug))
                authorRef.Slug = authorSlug;

            var item = new RecentDiscussionInfo
            {
                PublicId = d.PublicId,
                Title = d.Title,
                Slug = d.Slug,
                Type = ((DiscussionTypeEnum)d.Type).ToString(),
                CreatedAt = ToTimestamp(d.CreatedAt),
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,
                LastPostExcerpt = d.LastPostExcerpt ?? "",

                Space = new EntityRef { PublicId = d.SpacePublicId, Slug = d.SpaceSlug, Name = d.SpaceName },
                Hub = new EntityRef { PublicId = d.HubPublicId ?? "", Slug = d.HubSlug ?? "", Name = d.HubName ?? "" },
                Community = new EntityRef { PublicId = d.CommunityPublicId ?? "", Slug = d.CommunitySlug ?? "", Name = d.CommunityName ?? "" },
                Author = authorRef
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            item.Tags.AddRange(d.Tags ?? []);

            if (d.LastReplierPublicId is not null)
            {
                var replierRef = new AuthorRef
                {
                    PublicId = d.LastReplierPublicId,
                    DisplayName = d.LastReplierDisplayName ?? "",
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.LastReplierPublicId, AvatarEntityType.User, 0, d.LastReplierAvatarFileName),
                    AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(d.LastReplierPublicId, AvatarEntityType.User, 0, d.LastReplierAvatarFileName, d.LastReplierAvatarThumbnailFileName),
                    AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(d.LastReplierPublicId, AvatarEntityType.User, 0, d.LastReplierAvatarFileName)
                };
                if (discussionSlugs.TryGetValue(d.LastReplierPublicId, out var replierSlug))
                    replierRef.Slug = replierSlug;
                item.LastReplier = replierRef;
            }

            if (d.Preview is not null)
            {
                var preview = new DiscussionPreview();

                if (d.Preview.Poll is not null)
                {
                    preview.Poll = new PollPreview
                    {
                        TotalVotes = d.Preview.Poll.TotalVotes,
                        IsSecret = d.Preview.Poll.IsSecret
                    };
                    if (d.Preview.Poll.ClosesAt.HasValue)
                        preview.Poll.ClosesAt = ToTimestamp(d.Preview.Poll.ClosesAt.Value);
                    preview.Poll.Options.AddRange(d.Preview.Poll.Options.Select(o =>
                        new PollPreviewOption { Text = o.Text, VoteCount = o.VoteCount }));
                }

                if (d.Preview.Debate is not null)
                {
                    preview.Debate = new DebatePreview();
                    preview.Debate.Positions.AddRange(d.Preview.Debate.Positions.Select(p =>
                        new DebatePreviewPosition { Label = p.Label, Index = p.Index, PostCount = p.PostCount }));
                }

                if (d.Preview.Link is not null)
                {
                    var lp = d.Preview.Link;
                    preview.Link = new LinkPreview { Url = lp.Url, IsInternal = lp.IsInternal };
                    if (lp.Title is not null) preview.Link.Title = lp.Title;
                    if (lp.Description is not null) preview.Link.Description = lp.Description;
                    if (lp.Domain is not null) preview.Link.Domain = lp.Domain;
                    if (lp.ImageUrl is not null) preview.Link.ImageUrl = lp.ImageUrl;
                    if (lp.ImagePath is not null) preview.Link.ImagePathUrl = fileStorage.GetPublicUrl(lp.ImagePath);
                    if (lp.ImageThumbnailPath is not null) preview.Link.ImageThumbnailUrl = fileStorage.GetPublicUrl(lp.ImageThumbnailPath);
                    if (lp.OEmbedHtml is not null) preview.Link.OembedHtml = lp.OEmbedHtml;
                    if (lp.BlurDataUri is not null) preview.Link.BlurDataUri = lp.BlurDataUri;
                }

                if (d.Preview.Images is not null)
                {
                    preview.Images = new ImagesPreview
                    {
                        ImageCount = d.Preview.Images.ImageCount,
                        IsSpoiler = d.Preview.Images.IsSpoiler,
                        Layout = d.Preview.Images.Layout
                    };
                    preview.Images.Items.AddRange(d.Preview.Images.Items.Select(i =>
                    {
                        var pi = new ImagesPreviewItem { Url = i.Url, Width = i.Width, Height = i.Height };
                        if (i.ThumbnailUrl is not null) pi.ThumbnailUrl = i.ThumbnailUrl;
                        if (i.MediumThumbnailUrl is not null) pi.MediumThumbnailUrl = i.MediumThumbnailUrl;
                        if (i.BlurDataUri is not null) pi.BlurDataUri = i.BlurDataUri;
                        if (i.ThumbnailWidth > 0) pi.ThumbnailWidth = i.ThumbnailWidth.GetValueOrDefault();
                        if (i.ThumbnailHeight > 0) pi.ThumbnailHeight = i.ThumbnailHeight.GetValueOrDefault();
                        if (i.MediumThumbnailWidth > 0) pi.MediumThumbnailWidth = i.MediumThumbnailWidth.GetValueOrDefault();
                        if (i.MediumThumbnailHeight > 0) pi.MediumThumbnailHeight = i.MediumThumbnailHeight.GetValueOrDefault();
                        return pi;
                    }));
                }

                if (d.Preview.Iama is not null)
                {
                    var ip = d.Preview.Iama;
                    preview.Iama = new IamaPreview
                    {
                        Phase = ip.Phase,
                        OfficialAnswerCount = ip.OfficialAnswerCount,
                        BestQuestionCount = ip.BestQuestionCount,
                        IsVerified = ip.IsVerified
                    };
                    if (ip.ScheduledStartUtc.HasValue) preview.Iama.ScheduledStartUtc = ToTimestamp(ip.ScheduledStartUtc.Value);
                    if (ip.ScheduledEndUtc.HasValue) preview.Iama.ScheduledEndUtc = ToTimestamp(ip.ScheduledEndUtc.Value);
                }

                item.Preview = preview;
            }

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<PagedPostSearchResults> SearchPosts(SearchPostsRequest request, ServerCallContext context)
    {
        if (request.Query?.Length > 500)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Search query must be 500 characters or less"));

        var ct = context.CancellationToken;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await searchRepository.SearchPostsAsync(
            request.Query!,
            request.HasAuthorId ? request.AuthorId : null,
            request.HasDiscussionId ? request.DiscussionId : null,
            request.HasSpaceId ? request.SpaceId : null,
            request.Offset,
            pageSize,
            currentUser.GetCurrentUserId(),
            request.HasSortBy ? request.SortBy : null,
            request.HasDateRange ? request.DateRange : null,
            ct);

        var postAuthorIds = result.Items.Select(p => p.AuthorPublicId).Distinct().ToList();
        var postSlugs = await userRepository.GetSlugsByPublicIdsAsync(postAuthorIds, ct);

        var response = new PagedPostSearchResults
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        foreach (var p in result.Items)
        {
            var postAuthorRef = new AuthorRef
            {
                PublicId = p.AuthorPublicId,
                DisplayName = p.AuthorDisplayName,
                AvatarUrl = AvatarHelper.GetAvatarUrl(p.AuthorPublicId, AvatarEntityType.User, 0, p.AuthorAvatarFileName),
                AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(p.AuthorPublicId, AvatarEntityType.User, 0, p.AuthorAvatarFileName, p.AuthorAvatarThumbnailFileName),
                AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(p.AuthorPublicId, AvatarEntityType.User, 0, p.AuthorAvatarFileName)
            };
            if (postSlugs.TryGetValue(p.AuthorPublicId, out var postAuthorSlug))
                postAuthorRef.Slug = postAuthorSlug;

            response.Items.Add(new PostSearchResult
            {
                PublicId = p.PublicId,
                // Despite the proto name (`content_highlight`) and the DTO field
                // name (`RenderedContent`), this carries the post's PLAIN-TEXT
                // excerpt — SearchRepository.SearchPostsAsync projects
                // `PlainTextExcerpt ?? ""` into the DTO. It is NOT HTML and NOT
                // highlighted; consumers must HTML-encode it on render. Renaming
                // the proto field would be a breaking change, so the misnomer stays.
                ContentHighlight = p.RenderedContent,
                CreatedAt = ToTimestamp(p.CreatedAt),
                DiscussionPublicId = p.DiscussionPublicId,
                DiscussionTitle = p.DiscussionTitle,
                DiscussionSlug = p.DiscussionSlug,
                CommunitySlug = p.CommunitySlug,
                Author = postAuthorRef,
                Space = new EntityRef
                {
                    Slug = p.SpaceSlug,
                    Name = p.SpaceName
                },
                Hub = new EntityRef
                {
                    Slug = p.HubSlug,
                    Name = p.HubName
                }
            });
        }

        return response;
    }

    public override async Task<SitemapResponse> GetSitemapDiscussions(SitemapRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 25000, 1, 25000);

        var (discussions, totalCount) = await searchRepository.GetSitemapDiscussionsAsync(page, pageSize, ct);

        var response = new SitemapResponse { TotalCount = totalCount };

        foreach (var d in discussions)
        {
            response.Discussions.Add(new SitemapDiscussion
            {
                PublicId = d.PublicId,
                Slug = d.Slug,
                HubSlug = d.HubSlug,
                SpaceSlug = d.SpaceSlug,
                CommunitySlug = d.CommunitySlug,
                LastModified = ToTimestamp(d.LastModified),
                IsPinned = d.IsPinned
            });
        }

        return response;
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
