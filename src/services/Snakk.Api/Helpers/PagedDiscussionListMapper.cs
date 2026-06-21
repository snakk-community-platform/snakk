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
        IFileStorage fileStorage,
        Dictionary<string, string>? authorSlugs = null,
        Dictionary<string, int>? unreadCounts = null)
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

                // Proto string setters throw ArgumentNullException on null — coalesce
                // every possibly-null source (GDPR-anonymized authors have a null
                // CreatedByUserPublicId; space-display cache misses leave hub/community
                // fields null). One null here would otherwise fail the WHOLE response.
                Space = new EntityRef
                {
                    PublicId = d.SpacePublicId ?? "",
                    Slug = d.SpaceSlug ?? "",
                    Name = d.SpaceName ?? ""
                },
                Hub = new EntityRef
                {
                    PublicId = d.HubPublicId ?? "",
                    Slug = d.HubSlug ?? "",
                    Name = d.HubName ?? ""
                },
                Community = new EntityRef
                {
                    PublicId = d.CommunityPublicId ?? "",
                    Slug = d.CommunitySlug ?? "",
                    Name = d.CommunityName ?? ""
                },
                Author = BuildAuthorRef(d.CreatedByUserPublicId, d.CreatedByUserDisplayName, d.CreatedByUserAvatarFileName, d.CreatedByUserAvatarThumbnailFileName, authorSlugs)
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            if (d.SavedAt.HasValue)
                item.SavedAt = ToTimestamp(d.SavedAt.Value);

            if (unreadCounts?.TryGetValue(d.PublicId, out var unread) == true && unread > 0)
                item.UnreadPostCount = unread;

            item.Tags.AddRange(d.Tags ?? []);

            if (d.LastReplierPublicId is not null)
            {
                item.LastReplier = BuildAuthorRef(d.LastReplierPublicId, d.LastReplierDisplayName, d.LastReplierAvatarFileName, d.LastReplierAvatarThumbnailFileName, authorSlugs);
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
                    if (lp.ImageWidth is not null) preview.Link.ImageWidth = lp.ImageWidth.Value;
                    if (lp.ImageHeight is not null) preview.Link.ImageHeight = lp.ImageHeight.Value;
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

    private static AuthorRef BuildAuthorRef(
        string? publicId,
        string? displayName,
        string? avatarFileName,
        string? avatarThumbnailFileName,
        Dictionary<string, string>? slugs)
    {
        var pid = publicId ?? "";
        var ref_ = new AuthorRef
        {
            PublicId = pid,
            DisplayName = displayName ?? "",
            AvatarUrl = AvatarHelper.GetAvatarUrl(pid, AvatarEntityType.User, 0, avatarFileName),
            AvatarThumbnailUrl = AvatarHelper.GetAvatarThumbnailUrl(pid, AvatarEntityType.User, 0, avatarFileName, avatarThumbnailFileName),
            AvatarMicroUrl = AvatarHelper.GetAvatarMicroUrl(pid, AvatarEntityType.User, 0, avatarFileName)
        };
        if (pid.Length > 0 && slugs?.TryGetValue(pid, out var slug) == true)
            ref_.Slug = slug;
        return ref_;
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
