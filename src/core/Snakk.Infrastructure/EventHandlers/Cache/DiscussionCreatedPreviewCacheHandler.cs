namespace Snakk.Infrastructure.EventHandlers.Cache;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Snakk.Application.Events;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Domain.Events;
using Snakk.Infrastructure.Database;

public class DiscussionCreatedPreviewCacheHandler(
    SnakkDbContext context,
    HybridCache cache,
    IFileStorage fileStorage) : IDomainEventHandler<DiscussionCreatedEvent>
{
    private static readonly HybridCacheEntryOptions _options = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromHours(24),
    };

    public async Task HandleAsync(DiscussionCreatedEvent @event, CancellationToken ct = default)
    {
        var publicId = @event.DiscussionId.Value;

        var discussion = await context.Discussions
            .Where(d => d.PublicId == publicId)
            .Select(d => new { d.Id, d.Type })
            .FirstOrDefaultAsync(ct);

        if (discussion is null) return;

        switch (discussion.Type)
        {
            case 2:
                var poll = await FetchPollPreviewAsync(discussion.Id, ct);
                if (poll is not null) await cache.SetAsync($"preview:poll:{publicId}", poll, _options, cancellationToken: ct);
                break;
            case 4:
                var link = await FetchLinkPreviewAsync(discussion.Id, ct);
                if (link is not null) await cache.SetAsync($"preview:link:{publicId}", link, _options, cancellationToken: ct);
                break;
            case 5:
                var images = await FetchImagesPreviewAsync(discussion.Id, ct);
                if (images is not null) await cache.SetAsync($"preview:images:{publicId}", images, _options, cancellationToken: ct);
                break;
            case 7:
                var debate = await FetchDebatePreviewAsync(discussion.Id, ct);
                if (debate is not null) await cache.SetAsync($"preview:debate:{publicId}", debate, _options, cancellationToken: ct);
                break;
            case 9:
                var iama = await FetchIamaPreviewAsync(discussion.Id, ct);
                if (iama is not null) await cache.SetAsync($"preview:iama:{publicId}", iama, _options, cancellationToken: ct);
                break;
        }
    }

    private async Task<DiscussionPreviewDto?> FetchPollPreviewAsync(int discussionId, CancellationToken ct)
    {
        var poll = await context.DiscussionPolls
            .Where(p => p.DiscussionId == discussionId)
            .Select(p => new
            {
                p.VotesVisible,
                p.ClosesAt,
                Options = p.Options.OrderBy(o => o.DisplayOrder).Select(o => new { o.Text, o.VoteCount }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (poll is null) return null;

        var isSecret = !poll.VotesVisible;
        var isClosed = poll.ClosesAt.HasValue && poll.ClosesAt.Value <= DateTime.UtcNow;
        var hideVotes = isSecret && !isClosed;
        var options = poll.Options
            .Select(o => new PollOptionPreviewDto(o.Text, hideVotes ? 0 : o.VoteCount))
            .ToList();
        return new(Poll: new(options, hideVotes ? 0 : options.Sum(o => o.VoteCount), IsSecret: isSecret, ClosesAt: poll.ClosesAt));
    }

    private async Task<DiscussionPreviewDto?> FetchDebatePreviewAsync(int discussionId, CancellationToken ct)
    {
        var debate = await context.DiscussionDebates
            .Where(db => db.DiscussionId == discussionId)
            .Select(db => new
            {
                Positions = db.Positions.OrderBy(p => p.Index).Select(p => new { p.Id, p.Label, p.Index }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (debate is null) return null;

        // New discussions have no posts yet, so all position counts are 0.
        var positions = debate.Positions
            .Select(p => new DebatePositionPreviewDto(p.Label, p.Index, 0))
            .ToList();
        return new(Debate: new(positions));
    }

    private async Task<DiscussionPreviewDto?> FetchLinkPreviewAsync(int discussionId, CancellationToken ct)
    {
        var link = await context.DiscussionLinks
            .Where(l => l.DiscussionId == discussionId)
            .Select(l => new
            {
                l.Url, l.Title, l.Description, l.Domain,
                l.ImageUrl, l.ImagePath, l.ImageThumbnailPath, l.OEmbedHtml, l.IsInternal,
                l.ImageBlurDataUri, l.ImageWidth, l.ImageHeight
            })
            .FirstOrDefaultAsync(ct);

        if (link is null) return null;

        return new(Link: new(
            link.Url, link.Title, link.Description, link.Domain,
            link.ImageUrl, link.ImagePath, link.ImageThumbnailPath, link.OEmbedHtml, link.IsInternal,
            link.ImageBlurDataUri, link.ImageWidth, link.ImageHeight));
    }

    private async Task<DiscussionPreviewDto?> FetchImagesPreviewAsync(int discussionId, CancellationToken ct)
    {
        var img = await context.DiscussionImages
            .Where(g => g.DiscussionId == discussionId)
            .Select(g => new
            {
                g.IsSpoiler,
                g.Layout,
                Items = g.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new
                    {
                        Url = i.Image.StoragePath,
                        ThumbnailUrl = i.Image.ThumbnailPath,
                        i.Image.ThumbnailWidth,
                        i.Image.ThumbnailHeight,
                        MediumThumbnailUrl = i.Image.MediumThumbnailPath,
                        i.Image.MediumThumbnailWidth,
                        i.Image.MediumThumbnailHeight,
                        i.Image.BlurDataUri,
                        i.Image.Width,
                        i.Image.Height
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (img is null) return null;

        var items = img.Items
            .Select(i => new ImagePreviewItemDto(
                fileStorage.GetPublicUrl(i.Url),
                i.ThumbnailUrl is not null ? fileStorage.GetPublicUrl(i.ThumbnailUrl) : null,
                i.MediumThumbnailUrl is not null ? fileStorage.GetPublicUrl(i.MediumThumbnailUrl) : null,
                i.BlurDataUri,
                i.Width, i.Height,
                i.ThumbnailWidth, i.ThumbnailHeight,
                i.MediumThumbnailWidth, i.MediumThumbnailHeight))
            .ToList();
        return new(Images: new(items.Count, items, img.IsSpoiler, img.Layout));
    }

    private async Task<DiscussionPreviewDto?> FetchIamaPreviewAsync(int discussionId, CancellationToken ct)
    {
        var iama = await context.DiscussionIamas
            .Where(i => i.DiscussionId == discussionId)
            .Select(i => new
            {
                i.Phase,
                i.ScheduledStartUtc,
                i.ScheduledEndUtc,
                i.OfficialAnswerCount,
                i.BestQuestionCount,
                IsVerified = i.VerificationNote != null && i.VerificationNote != ""
            })
            .FirstOrDefaultAsync(ct);

        if (iama is null) return null;

        return new(Iama: new(
            iama.Phase,
            iama.ScheduledStartUtc,
            iama.ScheduledEndUtc,
            iama.OfficialAnswerCount,
            iama.BestQuestionCount,
            iama.IsVerified));
    }
}
