namespace Snakk.Api.Helpers;

using Google.Protobuf.WellKnownTypes;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Protos;
using Snakk.Protos.Discussion;
using Snakk.Shared.Enums;
using Snakk.Shared.Helpers;
using Snakk.Shared.Models;

internal static class PagedDiscussionListMapper
{
    internal static PagedRecentDiscussionList Build(
        PagedResult<RecentDiscussionDto> result,
        IFileStorage fileStorage)
    {
        var response = new PagedRecentDiscussionList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        if (result.NextCursor is not null)
            response.NextCursor = result.NextCursor;

        foreach (var d in result.Items)
        {
            var item = new RecentDiscussionInfo
            {
                PublicId = d.PublicId,
                Title = d.Title,
                Slug = d.Slug,
                Type = ((DiscussionTypeEnum)d.Type).ToString(),
                CreatedAt = ToTimestamp(d.CreatedAt),
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                IsAdult = d.IsAdult,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,

                Space = new EntityRef
                {
                    PublicId = d.SpacePublicId,
                    Slug = d.SpaceSlug,
                    Name = d.SpaceName
                },
                Hub = new EntityRef
                {
                    PublicId = d.HubPublicId,
                    Slug = d.HubSlug,
                    Name = d.HubName
                },
                Community = new EntityRef
                {
                    PublicId = d.CommunityPublicId,
                    Slug = d.CommunitySlug,
                    Name = d.CommunityName
                },
                Author = new AuthorRef
                {
                    PublicId = d.CreatedByUserPublicId,
                    DisplayName = d.CreatedByUserDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName),
                    AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName, d.CreatedByUserAvatarThumbnailFileName),
                    AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName)
                }
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            item.Tags.AddRange(d.Tags ?? []);

            if (d.LastReplierPublicId is not null)
            {
                item.LastReplier = new AuthorRef
                {
                    PublicId = d.LastReplierPublicId,
                    DisplayName = d.LastReplierDisplayName ?? "",
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.LastReplierPublicId, AvatarEntityType.User, 0, d.LastReplierAvatarFileName),
                    AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(d.LastReplierPublicId, AvatarEntityType.User, 0, d.LastReplierAvatarFileName, d.LastReplierAvatarThumbnailFileName),
                    AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(d.LastReplierPublicId, AvatarEntityType.User, 0, d.LastReplierAvatarFileName)
                };
                item.LastPostExcerpt = d.LastPostExcerpt ?? "";
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
                        if (i.ThumbnailWidth is not null) pi.ThumbnailWidth = i.ThumbnailWidth.Value;
                        if (i.ThumbnailHeight is not null) pi.ThumbnailHeight = i.ThumbnailHeight.Value;
                        if (i.MediumThumbnailWidth is not null) pi.MediumThumbnailWidth = i.MediumThumbnailWidth.Value;
                        if (i.MediumThumbnailHeight is not null) pi.MediumThumbnailHeight = i.MediumThumbnailHeight.Value;
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

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
