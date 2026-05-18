using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class DiscussionExtensionService(
    SnakkDbContext context,
    IFileStorage fileStorage,
    ILinkMetadataService linkMetadataService,
    IMarkupParser markupParser,
    Microsoft.Extensions.Logging.ILogger<DiscussionExtensionService> logger) : IDiscussionExtensionService
{
    public async Task CreateQuestionAsync(string discussionPublicId, CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        context.DiscussionQuestions.Add(new DiscussionTypeQuestionDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync(ct);
    }

    public async Task CreateGuideAsync(string discussionPublicId, CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        context.DiscussionGuides.Add(new DiscussionTypeGuideDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync(ct);
    }

    public async Task CreateImagesAsync(string discussionPublicId, string layout = "grid", List<string>? imageUrls = null, bool isSpoiler = false, CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        var images = new DiscussionTypeImageDatabaseEntity
        {
            DiscussionId = discussionId,
            Layout = layout,
            IsSpoiler = isSpoiler
        };

        context.DiscussionImages.Add(images);
        await context.SaveChangesAsync(ct);

        // Resolve image URLs to Media records and create ordered links
        if (imageUrls is { Count: > 0 })
        {
            var urlPrefix = fileStorage.GetPublicUrl("__probe__");
            urlPrefix = urlPrefix[..urlPrefix.IndexOf("__probe__", StringComparison.Ordinal)];
            var storagePaths = imageUrls
                .Where(url => url.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(url => url[urlPrefix.Length..])
                .ToList();

            if (storagePaths.Count > 0)
            {
                var mediaRecords = await context.Images
                    .AsTracking()
                    .Where(m => storagePaths.Contains(m.StoragePath) && !m.IsDeleted)
                    .ToListAsync(ct);

                var mediaByPath = mediaRecords.ToDictionary(m => m.StoragePath, m => m);

                for (var i = 0; i < storagePaths.Count; i++)
                {
                    if (mediaByPath.TryGetValue(storagePaths[i], out var media))
                    {
                        context.DiscussionImageAttachments.Add(new DiscussionTypeImageAttachmentDatabaseEntity
                        {
                            DiscussionTypeImageId = images.Id,
                            ImageId = media.Id,
                            DisplayOrder = i
                        });

                        // Publish draft media so cleanup worker doesn't delete it
                        if (media.IsDraft)
                        {
                            media.IsDraft = false;
                            media.PublishedAt = DateTime.UtcNow;
                        }
                    }
                }

                await context.SaveChangesAsync(ct);
            }
        }
    }

    public async Task CreatePollAsync(
        string discussionPublicId,
        List<string> options,
        bool allowMultipleChoices = false,
        bool allowChangeVote = false,
        DateTime? closesAt = null,
        bool votesVisible = true,
        bool isSegmented = false,
        string? segmentLabel = null,
        string? segmentOptionA = null,
        string? segmentOptionB = null,
        CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        var poll = new DiscussionTypePollDatabaseEntity
        {
            DiscussionId = discussionId,
            AllowMultipleChoices = allowMultipleChoices,
            AllowChangeVote = allowChangeVote,
            ClosesAt = closesAt,
            VotesVisible = votesVisible,
            IsSegmented = isSegmented,
            SegmentLabel = segmentLabel,
            SegmentOptionA = segmentOptionA,
            SegmentOptionB = segmentOptionB
        };

        context.DiscussionPolls.Add(poll);
        await context.SaveChangesAsync(ct);

        for (var i = 0; i < options.Count; i++)
        {
            context.DiscussionPollOptions.Add(new DiscussionTypePollOptionDatabaseEntity
            {
                PollId = poll.Id,
                Text = options[i],
                DisplayOrder = i
            });
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task CreateLinkAsync(
        string discussionPublicId,
        string url,
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        string? domain = null,
        CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        // Dedup: check if we already have metadata for this exact URL
        var existing = await context.DiscussionLinks
            .AsNoTracking()
            .Where(l => l.Url == url && l.Title != null)
            .Select(l => new { l.Title, l.Description, l.ImageUrl, l.Domain, l.OEmbedHtml, l.ImagePath, l.ImageThumbnailPath, l.ImageBlurDataUri, l.ImageWidth, l.ImageHeight })
            .FirstOrDefaultAsync(ct);

        LinkMetadata? metadata = null;

        if (existing is not null)
        {
            // Reuse metadata from a previous link to the same URL
            metadata = new LinkMetadata(
                existing.Title, existing.Description, existing.ImageUrl,
                existing.Domain, existing.OEmbedHtml,
                existing.ImagePath, existing.ImageThumbnailPath, existing.ImageBlurDataUri,
                ImageWidth: existing.ImageWidth, ImageHeight: existing.ImageHeight);
        }
        else
        {
            // Resolve the space's effective language for Accept-Language header
            var effectiveLanguage = await context.Discussions
                .Where(d => d.PublicId == discussionPublicId)
                .Select(d => d.Space.LanguageCode ?? d.Space.HubLanguageCode ?? d.Space.CommunityLanguageCode)
                .FirstOrDefaultAsync(ct);

            // Fetch fresh metadata
            try
            {
                metadata = await linkMetadataService.FetchAsync(url, effectiveLanguage);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch link metadata for {Url}", url);
            }
        }

        var parsedDomain = domain;
        if (string.IsNullOrEmpty(parsedDomain) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            parsedDomain = uri.Host.TrimStart('w', 'w', 'w', '.');

        context.DiscussionLinks.Add(new DiscussionTypeLinkDatabaseEntity
        {
            DiscussionId = discussionId,
            Url = url,
            Title = title ?? metadata?.Title,
            Description = description ?? metadata?.Description,
            ImageUrl = imageUrl ?? metadata?.ImageUrl,
            Domain = parsedDomain ?? metadata?.Domain,
            OEmbedHtml = metadata?.OEmbedHtml,
            ImagePath = metadata?.ImagePath,
            ImageThumbnailPath = metadata?.ImageThumbnailPath,
            ImageBlurDataUri = metadata?.ImageBlurDataUri,
            ImageWidth = metadata?.ImageWidth,
            ImageHeight = metadata?.ImageHeight,
            IsInternal = metadata?.IsInternal ?? false
        });

        await context.SaveChangesAsync(ct);
    }

    public async Task CreateDebateAsync(
        string discussionPublicId,
        List<string> positionLabels,
        bool allowNeutral = false,
        CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        var debate = new DiscussionTypeDebateDatabaseEntity
        {
            DiscussionId = discussionId,
            AllowNeutral = allowNeutral
        };

        context.DiscussionDebates.Add(debate);
        await context.SaveChangesAsync(ct);

        for (var i = 0; i < positionLabels.Count; i++)
        {
            context.DiscussionDebatePositions.Add(new DiscussionTypeDebatePositionDatabaseEntity
            {
                DebateId = debate.Id,
                Index = i,
                Label = positionLabels[i]
            });
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task MarkQuestionSolvedAsync(string discussionPublicId, string acceptedPostPublicId, CancellationToken ct = default)
    {
        var question = await context.DiscussionQuestions
            .AsTracking()
            .Where(q => q.Discussion.PublicId == discussionPublicId && !q.Discussion.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Question not found");

        var postId = await context.Posts
            .Where(p => p.PublicId == acceptedPostPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (postId == 0)
            throw new InvalidOperationException("Post not found");

        question.AcceptedPostId = postId;
        question.SolvedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }

    public async Task CreateJournalAsync(string discussionPublicId, CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        context.DiscussionJournals.Add(new DiscussionTypeJournalDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync(ct);
    }

    public async Task AddJournalEntryAsync(string discussionPublicId, string postPublicId, CancellationToken ct = default)
    {
        var journal = await context.DiscussionJournals
            .Where(j => j.Discussion.PublicId == discussionPublicId && !j.Discussion.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Journal not found");

        var postId = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (postId == 0)
            throw new InvalidOperationException("Post not found");

        context.DiscussionJournalEntryPosts.Add(new DiscussionTypeJournalEntryPostDatabaseEntity
        {
            PostId = postId,
            JournalId = journal.Id
        });

        await context.SaveChangesAsync(ct);
    }

    public async Task CreateIamaAsync(
        string discussionPublicId,
        bool isScheduled = false,
        DateTime? scheduledStartUtc = null,
        DateTime? scheduledEndUtc = null,
        string? verificationNote = null,
        CancellationToken ct = default)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId, ct);

        context.DiscussionIamas.Add(new DiscussionTypeIamaDatabaseEntity
        {
            DiscussionId = discussionId,
            Phase = isScheduled ? 0 : 1, // Announced if scheduled, Live if open-ended
            IsScheduled = isScheduled,
            ScheduledStartUtc = scheduledStartUtc,
            ScheduledEndUtc = scheduledEndUtc,
            VerificationNote = verificationNote,
            VerificationNoteHtml = verificationNote != null ? markupParser.ToHtml(verificationNote) : null,
        });

        await context.SaveChangesAsync(ct);
    }

    private async Task<int> GetDiscussionIdAsync(string publicId, CancellationToken ct = default)
    {
        var id = await context.Discussions
            .Where(d => d.PublicId == publicId && !d.IsDeleted)
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);

        return id == 0
            ? throw new InvalidOperationException($"Discussion '{publicId}' not found")
            : id;
    }
}
