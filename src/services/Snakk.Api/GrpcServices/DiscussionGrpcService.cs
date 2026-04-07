using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Snakk.Shared.Helpers;
using Snakk.Api.Services;
using Snakk.Application.Repositories;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.ValueObjects;
using Snakk.Protos;
using Snakk.Protos.Discussion;
using Snakk.Shared.Enums;

namespace Snakk.Api.GrpcServices;

public class DiscussionGrpcService(
    DiscussionUseCase discussionUseCase,
    ISearchRepository searchRepository,
    StatisticsUseCase statisticsUseCase,
    ICurrentUserService currentUser,
    IUserGrantsCacheService grantsCache,
    IEntityHierarchyCacheService hierarchyCache,
    IAllowedTypesService allowedTypesService,
    IDiscussionExtensionService extensionService,
    IPollService pollService,
    IDiscussionTypeQueryService typeQueryService,
    IFileStorage fileStorage,
    IGroupAccessService groupAccessService) : DiscussionService.DiscussionServiceBase
{
    public override async Task<DiscussionInfo> GetDiscussion(GetDiscussionRequest request, ServerCallContext context)
    {
        var result = await discussionUseCase.GetDiscussionAsync(DiscussionId.From(request.PublicId));

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var d = result.Value;

        if (!await IsDiscussionAccessibleAsync(d.PublicId.Value, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));
        var postCount = await searchRepository.GetDiscussionPostCountAsync(d.PublicId.Value);
        var info = new DiscussionInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            SpaceId = d.SpaceId.Value,
            CreatedAt = ToTimestamp(d.CreatedAt),
            IsPinned = d.IsPinned,
            IsLocked = d.IsLocked,
            Type = d.Type.ToString(),
            PostCount = postCount
        };

        if (d.LastActivityAt.HasValue)
            info.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

        return info;
    }

    public override async Task<DiscussionCreatedInfo> CreateDiscussion(CreateDiscussionRequest request, ServerCallContext context)
    {
        if (request.Title?.Length > 300)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Title must be 300 characters or less"));

        if (request.Content?.Length > 50000)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Content must be 50,000 characters or less"));

        var userId = RequireAuth();

        if (!await IsSpaceAccessibleAsync(request.SpaceId, userId.Value))
            throw new RpcException(new Status(StatusCode.NotFound, "Space not found"));

        // Enforce write permissions: Author or Write required to create discussions
        var access = await groupAccessService.CheckAccessForSpaceAsync(userId.Value, request.SpaceId, context.CancellationToken);
        if (!access.CanAuthor)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "You don't have permission to create discussions here"));

        var type = (DiscussionTypeEnum)request.Type;

        // Validate type is allowed in this space
        var effectiveTypes = await allowedTypesService.GetSpaceEffectiveAllowedTypesAsync(request.SpaceId);
        if (!effectiveTypes.Contains(type))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Discussion type '{type}' is not allowed in this space"));

        // Validate type-specific required fields
        ValidateTypeSpecificFields(request, type);

        var slug = SlugHelper.ToSlug(request.Title);
        var result = await discussionUseCase.CreateDiscussionAsync(
            SpaceId.From(request.SpaceId),
            userId,
            request.Title,
            slug,
            request.Content,
            type);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                result.Error ?? "Failed to create discussion"));

        var d = result.Value;

        // Create type-specific extension records
        await CreateExtensionRecordsAsync(d.PublicId.Value, type, request);

        return new DiscussionCreatedInfo
        {
            PublicId = d.PublicId.Value,
            Title = d.Title,
            Slug = d.Slug,
            CreatedAt = ToTimestamp(d.CreatedAt),
            Type = d.Type.ToString()
        };
    }

    private static void ValidateTypeSpecificFields(CreateDiscussionRequest request, DiscussionTypeEnum type)
    {
        switch (type)
        {
            case DiscussionTypeEnum.Poll:
                if (request.PollOptions.Count < 2)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Poll requires at least 2 options"));
                if (request.PollOptions.Count > 20)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Poll cannot have more than 20 options"));
                break;

            case DiscussionTypeEnum.Debate:
                if (request.DebatePositions.Count < 2)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Debate requires at least 2 positions"));
                if (request.DebatePositions.Count > 3)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Debate cannot have more than 3 positions"));
                break;

            case DiscussionTypeEnum.Link:
                if (!request.HasLinkUrl || string.IsNullOrWhiteSpace(request.LinkUrl))
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Link discussions require a URL"));
                break;
                
            case DiscussionTypeEnum.Iama:
                if (request.IamaIsScheduled && request.IamaScheduledStart is null)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Scheduled AMAs require a start time"));
                break;
        }
    }

    private async Task CreateExtensionRecordsAsync(string discussionPublicId, DiscussionTypeEnum type, CreateDiscussionRequest request)
    {
        switch (type)
        {
            case DiscussionTypeEnum.Question:
                await extensionService.CreateQuestionAsync(discussionPublicId);
                break;

            case DiscussionTypeEnum.Guide:
                await extensionService.CreateGuideAsync(discussionPublicId);
                break;

            case DiscussionTypeEnum.Poll:
                await extensionService.CreatePollAsync(
                    discussionPublicId,
                    request.PollOptions.ToList(),
                    request.PollAllowMultiple,
                    request.PollAllowChangeVote,
                    request.PollClosesAt is not null ? request.PollClosesAt.ToDateTime() : null,
                    !request.PollSecret);
                break;

            case DiscussionTypeEnum.Link:
                await extensionService.CreateLinkAsync(
                    discussionPublicId,
                    request.LinkUrl);
                break;

            case DiscussionTypeEnum.Debate:
                await extensionService.CreateDebateAsync(
                    discussionPublicId,
                    request.DebatePositions.ToList(),
                    request.DebateAllowNeutral);
                break;

            case DiscussionTypeEnum.Journal:
                await extensionService.CreateJournalAsync(discussionPublicId);
                break;

            case DiscussionTypeEnum.Images:
                await extensionService.CreateImagesAsync(
                    discussionPublicId,
                    request.HasImagesLayout ? request.ImagesLayout : "grid",
                    request.ImagesImageUrls.Count > 0 ? request.ImagesImageUrls.ToList() : null);
                break;

            case DiscussionTypeEnum.Iama:
                await extensionService.CreateIamaAsync(
                    discussionPublicId,
                    request.IamaIsScheduled,
                    request.IamaScheduledStart is not null ? request.IamaScheduledStart.ToDateTime() : null,
                    request.IamaScheduledEnd is not null ? request.IamaScheduledEnd.ToDateTime() : null,
                    request.HasIamaVerificationNote ? request.IamaVerificationNote : null);
                break;
        }
    }

    public override async Task<PagedRecentDiscussionList> GetRecentDiscussions(GetRecentDiscussionsRequest request, ServerCallContext context)
    {
        var result = await searchRepository.GetRecentDiscussionsAsync(
            request.Offset,
            request.PageSize,
            request.HasCommunityId ? request.CommunityId : null,
            request.HasHubId ? request.HubId : null,
            request.HasCursor ? request.Cursor : null,
            currentUser.GetCurrentUserId(),
            request.HasAuthorId ? request.AuthorId : null);

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
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.CreatedByUserPublicId, AvatarEntityType.User, 0, d.CreatedByUserAvatarFileName)
                }
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            item.Tags.AddRange(d.Tags ?? []);

            // Map preview data
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
                }

                if (d.Preview.Images is not null)
                {
                    preview.Images = new ImagesPreview { ImageCount = d.Preview.Images.ImageCount };
                    preview.Images.Items.AddRange(d.Preview.Images.Items.Select(i =>
                    {
                        var pi = new ImagesPreviewItem { Url = i.Url };
                        if (i.ThumbnailUrl is not null) pi.ThumbnailUrl = i.ThumbnailUrl;
                        if (i.MediumThumbnailUrl is not null) pi.MediumThumbnailUrl = i.MediumThumbnailUrl;
                        if (i.BlurDataUri is not null) pi.BlurDataUri = i.BlurDataUri;
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

    public override async Task<PagedDiscussionBySpaceList> GetDiscussionsBySpace(GetDiscussionsBySpaceRequest request, ServerCallContext context)
    {
        int? typeFilter = request.HasTypeFilter ? request.TypeFilter : null;

        var result = await searchRepository.GetDiscussionsBySpaceAsync(
            request.SpaceId,
            request.Offset,
            request.PageSize,
            typeFilter,
            currentUser.GetCurrentUserId(),
            request.HasCursor ? request.Cursor : null);

        var response = new PagedDiscussionBySpaceList
        {
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        };

        if (result.NextCursor is not null)
            response.NextCursor = result.NextCursor;

        foreach (var d in result.Items)
        {
            var item = new DiscussionBySpaceInfo
            {
                PublicId = d.PublicId,
                SpaceId = d.SpacePublicId,
                Title = d.Title,
                Slug = d.Slug,
                Type = ((DiscussionTypeEnum)d.Type).ToString(),
                CreatedAt = ToTimestamp(d.CreatedAt),
                IsPinned = d.IsPinned,
                IsLocked = d.IsLocked,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,

                Author = new AuthorRef
                {
                    PublicId = d.AuthorPublicId,
                    DisplayName = d.AuthorDisplayName,
                    AvatarUrl = AvatarHelper.GetAvatarUrl(d.AuthorPublicId, AvatarEntityType.User, 0, d.AuthorAvatarFileName)
                }
            };

            if (d.LastActivityAt.HasValue)
                item.LastActivityAt = ToTimestamp(d.LastActivityAt.Value);

            if (d.Tags is not null)
            {
                var tags = d.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                item.Tags.AddRange(tags);
            }

            response.Items.Add(item);
        }

        return response;
    }

    public override async Task<DiscussionPreviewInfo> GetDiscussionPreview(GetDiscussionPreviewRequest request, ServerCallContext context)
    {
        if (!await IsDiscussionAccessibleAsync(request.DiscussionId, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var result = await discussionUseCase.GetFirstPostPreviewAsync(DiscussionId.From(request.DiscussionId));

        if (!result.IsSuccess)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        return new DiscussionPreviewInfo { Content = result.Value ?? "" };
    }

    public override async Task<DiscussionStats> GetDiscussionStats(GetDiscussionStatsRequest request, ServerCallContext context)
    {
        if (!await IsDiscussionAccessibleAsync(request.PublicId, currentUser.GetCurrentUserId()))
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var result = await statisticsUseCase.GetDiscussionStatsAsync(request.PublicId);

        if (!result.IsSuccess || result.Value is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Discussion not found"));

        var stats = result.Value;

        return new DiscussionStats
        {
            PublicId = stats.PublicId,
            Title = stats.Title,
            ReplyCount = stats.ReplyCount,
            FollowerCount = stats.FollowerCount
        };
    }

    private async Task<bool> IsDiscussionAccessibleAsync(string discussionPublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetDiscussionHierarchyAsync(discussionPublicId);
        if (h is null) return false;

        var spaceGate = restricted.SpaceIds.Contains(h.SpaceId);
        var hubGate = restricted.HubIds.Contains(h.HubId);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!spaceGate && !hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return (!spaceGate || grants.SpaceIds.Contains(h.SpaceId))
            && (!hubGate || grants.HubIds.Contains(h.HubId))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    // --- Poll RPCs ---

    public override async Task<PollResponse> GetPoll(GetPollRequest request, ServerCallContext context)
    {
        var userId = currentUser.GetCurrentUserId();
        var data = await pollService.GetPollAsync(request.DiscussionId, userId);

        if (data is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Poll not found"));

        var response = new PollResponse
        {
            AllowMultiple = data.AllowMultipleChoices,
            AllowChangeVote = data.AllowChangeVote,
            IsClosed = data.IsClosed,
            TotalVotes = data.TotalVotes,
            IsSecret = data.IsSecret
        };

        if (data.ClosesAt.HasValue)
            response.ClosesAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(data.ClosesAt.Value, DateTimeKind.Utc));

        response.Options.AddRange(data.Options.Select(o => new PollOptionInfo
        {
            Id = o.Id,
            Text = o.Text,
            VoteCount = o.VoteCount,
            DisplayOrder = o.DisplayOrder
        }));

        response.UserVotedOptionIds.AddRange(data.UserVotedOptionIds);

        return response;
    }

    public override async Task<VotePollResponse> VotePoll(VotePollRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await pollService.VoteAsync(request.DiscussionId, request.OptionId, userId.Value);

        return new VotePollResponse { Success = success, Error = error };
    }

    public override async Task<VotePollResponse> RemovePollVote(RemovePollVoteRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await pollService.RemoveVoteAsync(request.DiscussionId, request.OptionId, userId.Value);

        return new VotePollResponse { Success = success, Error = error };
    }

    // --- Question RPCs ---

    public override async Task<QuestionStatusResponse> GetQuestionStatus(GetQuestionStatusRequest request, ServerCallContext context)
    {
        var status = await typeQueryService.GetQuestionStatusAsync(request.DiscussionId);
        if (status is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Question not found"));

        var response = new QuestionStatusResponse { IsSolved = status.IsSolved };
        if (status.AcceptedPostPublicId is not null) response.AcceptedPostPublicId = status.AcceptedPostPublicId;
        if (status.SolvedAt.HasValue)
            response.SolvedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(status.SolvedAt.Value, DateTimeKind.Utc));
        return response;
    }

    public override async Task<MarkQuestionSolvedResponse> MarkQuestionSolved(MarkQuestionSolvedRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await typeQueryService.MarkQuestionSolvedAsync(request.DiscussionId, request.PostPublicId, userId.Value);
        return new MarkQuestionSolvedResponse { Success = success, Error = error };
    }

    // --- Debate RPCs ---

    public override async Task<DebateInfoResponse> GetDebateInfo(GetDebateInfoRequest request, ServerCallContext context)
    {
        var info = await typeQueryService.GetDebateInfoAsync(request.DiscussionId);
        if (info is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Debate not found"));

        var response = new DebateInfoResponse { AllowNeutral = info.AllowNeutral };
        response.Positions.AddRange(info.Positions.Select(p => new DebatePositionInfo
        {
            Id = p.Id, Label = p.Label, Index = p.Index, PostCount = p.PostCount
        }));
        foreach (var (postId, posId) in info.PostPositions)
            response.PostPositions[postId] = posId;
        return response;
    }

    // --- Link RPCs ---

    public override async Task<DiscussionLinkResponse> GetDiscussionLink(GetDiscussionLinkRequest request, ServerCallContext context)
    {
        var link = await typeQueryService.GetLinkInfoAsync(request.DiscussionId);
        if (link is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Link not found"));

        var response = new DiscussionLinkResponse { Url = link.Url };
        if (link.Title is not null) response.Title = link.Title;
        if (link.Description is not null) response.Description = link.Description;
        if (link.ImageUrl is not null) response.ImageUrl = link.ImageUrl;
        if (link.Domain is not null) response.Domain = link.Domain;
        if (link.OEmbedHtml is not null) response.OembedHtml = link.OEmbedHtml;
        if (link.ImagePath is not null) response.ImagePathUrl = fileStorage.GetPublicUrl(link.ImagePath);
        if (link.BlurDataUri is not null) response.BlurDataUri = link.BlurDataUri;
        response.IsInternal = link.IsInternal;
        return response;
    }

    // --- Images RPCs ---

    public override async Task<ImagesLayoutResponse> GetImagesLayout(GetImagesLayoutRequest request, ServerCallContext context)
    {
        var layout = await typeQueryService.GetImagesLayoutAsync(request.DiscussionId);
        var images = await typeQueryService.GetImagesListAsync(request.DiscussionId);

        var response = new ImagesLayoutResponse { Layout = layout ?? "grid" };
        foreach (var img in images)
        {
            var proto = new ImagesImageProto { Url = img.Url };
            if (img.ThumbnailUrl != null) proto.ThumbnailUrl = img.ThumbnailUrl;
            if (img.BlurDataUri != null) proto.BlurDataUri = img.BlurDataUri;
            response.Images.Add(proto);
        }

        return response;
    }

    // --- Journal RPCs ---

    public override async Task<JournalEntriesResponse> GetJournalEntries(GetJournalEntriesRequest request, ServerCallContext context)
    {
        var info = await typeQueryService.GetJournalInfoAsync(request.DiscussionId);
        if (info is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Journal not found"));

        var response = new JournalEntriesResponse();
        response.EntryPostPublicIds.AddRange(info.EntryPostPublicIds);
        return response;
    }

    public override async Task<SetPostDebatePositionResponse> SetPostDebatePosition(SetPostDebatePositionRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await typeQueryService.SetPostDebatePositionAsync(request.DiscussionId, request.PostPublicId, request.PositionId, userId.Value);
        return new SetPostDebatePositionResponse { Success = success, Error = error };
    }

    public override async Task<AddJournalEntryResponse> AddJournalEntry(AddJournalEntryRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await typeQueryService.AddJournalEntryAsync(request.DiscussionId, request.PostPublicId, userId.Value);
        return new AddJournalEntryResponse { Success = success, Error = error };
    }

    // --- IAMA RPCs ---

    public override async Task<IamaInfoResponse> GetIamaInfo(GetIamaInfoRequest request, ServerCallContext context)
    {
        var info = await typeQueryService.GetIamaInfoAsync(request.DiscussionId);
        if (info is null)
            throw new RpcException(new Status(StatusCode.NotFound, "AMA not found"));

        var response = new IamaInfoResponse
        {
            Phase = info.Phase,
            IsScheduled = info.IsScheduled,
            VerificationNote = info.VerificationNote
        };

        if (info.ScheduledStartUtc.HasValue)
            response.ScheduledStartUtc = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(info.ScheduledStartUtc.Value, DateTimeKind.Utc));

        if (info.ScheduledEndUtc.HasValue)
            response.ScheduledEndUtc = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                DateTime.SpecifyKind(info.ScheduledEndUtc.Value, DateTimeKind.Utc));

        response.OfficialAnswers.Add(info.OfficialAnswers);
        response.BestQuestionPostPublicIds.AddRange(info.BestQuestionPostPublicIds);
        return response;
    }

    public override async Task<MarkIamaOfficialAnswerResponse> MarkIamaOfficialAnswer(MarkIamaOfficialAnswerRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await typeQueryService.MarkIamaOfficialAnswerAsync(
            request.DiscussionId, request.QuestionPostPublicId, request.AnswerPostPublicId, userId.Value);
        return new MarkIamaOfficialAnswerResponse { Success = success, Error = error };
    }

    public override async Task<SetIamaBestQuestionsResponse> SetIamaBestQuestions(SetIamaBestQuestionsRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await typeQueryService.SetIamaBestQuestionsAsync(
            request.DiscussionId, request.PostPublicIds.ToList(), userId.Value);
        return new SetIamaBestQuestionsResponse { Success = success, Error = error };
    }

    public override async Task<TransitionIamaPhaseResponse> TransitionIamaPhase(TransitionIamaPhaseRequest request, ServerCallContext context)
    {
        var userId = RequireAuth();
        var (success, error) = await typeQueryService.TransitionIamaPhaseAsync(
            request.DiscussionId, request.NewPhase, userId.Value);
        return new TransitionIamaPhaseResponse { Success = success, Error = error };
    }

    private async Task<bool> IsSpaceAccessibleAsync(string spacePublicId, string? userId)
    {
        var restricted = await grantsCache.GetRestrictedEntitiesAsync();
        if (restricted.IsEmpty) return true;

        var h = await hierarchyCache.GetSpaceHierarchyAsync(spacePublicId);
        if (h is null) return false;

        var spaceGate = restricted.SpaceIds.Contains(h.Id);
        var hubGate = restricted.HubIds.Contains(h.HubId);
        var communityGate = restricted.CommunityIds.Contains(h.CommunityId);

        if (!spaceGate && !hubGate && !communityGate) return true;
        if (userId is null) return false;

        var grants = await grantsCache.GetGrantsAsync(userId);
        return (!spaceGate || grants.SpaceIds.Contains(h.Id))
            && (!hubGate || grants.HubIds.Contains(h.HubId))
            && (!communityGate || grants.CommunityIds.Contains(h.CommunityId));
    }

    private UserId RequireAuth()
    {
        if (!currentUser.IsAuthenticated())
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        var userId = currentUser.GetCurrentUserId();

        if (userId is null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Not authenticated"));

        return UserId.From(userId);
    }

    private static Timestamp ToTimestamp(DateTime dt) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
