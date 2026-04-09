using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Services;

public class DiscussionExtensionService(
    SnakkDbContext context,
    IConfiguration configuration,
    ILinkMetadataService linkMetadataService,
    Microsoft.Extensions.Logging.ILogger<DiscussionExtensionService> logger) : IDiscussionExtensionService
{
    private readonly string _mediaUrlBase = configuration["FileStorage:MediaUrlBase"] ?? "";
    public async Task CreateQuestionAsync(string discussionPublicId)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionQuestions.Add(new DiscussionTypeQuestionDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync();
    }

    public async Task CreateGuideAsync(string discussionPublicId)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionGuides.Add(new DiscussionTypeGuideDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync();
    }

    public async Task CreateImagesAsync(string discussionPublicId, string layout = "grid", List<string>? imageUrls = null, bool isSpoiler = false)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        var images = new DiscussionTypeImageDatabaseEntity
        {
            DiscussionId = discussionId,
            Layout = layout,
            IsSpoiler = isSpoiler
        };

        context.DiscussionImages.Add(images);
        await context.SaveChangesAsync();

        // Resolve image URLs to Media records and create ordered links
        if (imageUrls is { Count: > 0 })
        {
            var urlBase = _mediaUrlBase.TrimEnd('/') + "/";
            var storagePaths = imageUrls
                .Where(url => url.StartsWith(urlBase, StringComparison.OrdinalIgnoreCase))
                .Select(url => url[urlBase.Length..])
                .ToList();

            if (storagePaths.Count > 0)
            {
                var mediaRecords = await context.Images
                    .AsTracking()
                    .Where(m => storagePaths.Contains(m.StoragePath) && !m.IsDeleted)
                    .ToListAsync();

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

                await context.SaveChangesAsync();
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
        string? segmentOptionB = null)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

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
        await context.SaveChangesAsync();

        for (var i = 0; i < options.Count; i++)
        {
            context.DiscussionPollOptions.Add(new DiscussionTypePollOptionDatabaseEntity
            {
                PollId = poll.Id,
                Text = options[i],
                DisplayOrder = i
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task CreateLinkAsync(
        string discussionPublicId,
        string url,
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        string? domain = null)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        // Dedup: check if we already have metadata for this exact URL
        var existing = await context.DiscussionLinks
            .AsNoTracking()
            .Where(l => l.Url == url && l.Title != null)
            .Select(l => new { l.Title, l.Description, l.ImageUrl, l.Domain, l.OEmbedHtml, l.ImagePath, l.ImageThumbnailPath, l.ImageBlurDataUri })
            .FirstOrDefaultAsync();

        LinkMetadata? metadata = null;

        if (existing is not null)
        {
            // Reuse metadata from a previous link to the same URL
            metadata = new LinkMetadata(
                existing.Title, existing.Description, existing.ImageUrl,
                existing.Domain, existing.OEmbedHtml,
                existing.ImagePath, existing.ImageThumbnailPath, existing.ImageBlurDataUri);
        }
        else
        {
            // Fetch fresh metadata
            try
            {
                metadata = await linkMetadataService.FetchAsync(url);
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
            IsInternal = metadata?.IsInternal ?? false
        });

        await context.SaveChangesAsync();
    }

    public async Task CreateDebateAsync(
        string discussionPublicId,
        List<string> positionLabels,
        bool allowNeutral = false)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        var debate = new DiscussionTypeDebateDatabaseEntity
        {
            DiscussionId = discussionId,
            AllowNeutral = allowNeutral
        };

        context.DiscussionDebates.Add(debate);
        await context.SaveChangesAsync();

        for (var i = 0; i < positionLabels.Count; i++)
        {
            context.DiscussionDebatePositions.Add(new DiscussionTypeDebatePositionDatabaseEntity
            {
                DebateId = debate.Id,
                Index = i,
                Label = positionLabels[i]
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task MarkQuestionSolvedAsync(string discussionPublicId, string acceptedPostPublicId)
    {
        var question = await context.DiscussionQuestions
            .AsTracking()
            .Where(q => q.Discussion.PublicId == discussionPublicId && !q.Discussion.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Question not found");

        var postId = await context.Posts
            .Where(p => p.PublicId == acceptedPostPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            throw new InvalidOperationException("Post not found");

        question.AcceptedPostId = postId;
        question.SolvedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task CreateJournalAsync(string discussionPublicId)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionJournals.Add(new DiscussionTypeJournalDatabaseEntity
        {
            DiscussionId = discussionId
        });

        await context.SaveChangesAsync();
    }

    public async Task AddJournalEntryAsync(string discussionPublicId, string postPublicId)
    {
        var journal = await context.DiscussionJournals
            .Where(j => j.Discussion.PublicId == discussionPublicId && !j.Discussion.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Journal not found");

        var postId = await context.Posts
            .Where(p => p.PublicId == postPublicId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (postId == 0)
            throw new InvalidOperationException("Post not found");

        context.DiscussionJournalEntryPosts.Add(new DiscussionTypeJournalEntryPostDatabaseEntity
        {
            PostId = postId,
            JournalId = journal.Id
        });

        await context.SaveChangesAsync();
    }

    public async Task CreateIamaAsync(
        string discussionPublicId,
        bool isScheduled = false,
        DateTime? scheduledStartUtc = null,
        DateTime? scheduledEndUtc = null,
        string? verificationNote = null)
    {
        var discussionId = await GetDiscussionIdAsync(discussionPublicId);

        context.DiscussionIamas.Add(new DiscussionTypeIamaDatabaseEntity
        {
            DiscussionId = discussionId,
            Phase = isScheduled ? 0 : 1, // Announced if scheduled, Live if open-ended
            IsScheduled = isScheduled,
            ScheduledStartUtc = scheduledStartUtc,
            ScheduledEndUtc = scheduledEndUtc,
            VerificationNote = verificationNote
        });

        await context.SaveChangesAsync();
    }

    private async Task<int> GetDiscussionIdAsync(string publicId)
    {
        var id = await context.Discussions
            .Where(d => d.PublicId == publicId && !d.IsDeleted)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        return id == 0
            ? throw new InvalidOperationException($"Discussion '{publicId}' not found")
            : id;
    }
}
