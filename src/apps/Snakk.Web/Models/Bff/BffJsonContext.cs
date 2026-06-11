using System.Text.Json.Serialization;

namespace Snakk.Web.Models.Bff;

/// <summary>
/// Source-generated JSON serialization context for BFF response types.
/// Eliminates reflection-based serialization overhead for known types.
/// </summary>
[JsonSerializable(typeof(BffAuthStatusResponse))]
[JsonSerializable(typeof(BffDiscussionPreviewResponse))]
[JsonSerializable(typeof(BffMarkupPreviewResponse))]
[JsonSerializable(typeof(BffHubStatsResponse))]
[JsonSerializable(typeof(BffSpaceStatsResponse))]
[JsonSerializable(typeof(BffCommunityStatsResponse))]
[JsonSerializable(typeof(BffFollowStatusResponse))]
[JsonSerializable(typeof(BffFollowResultResponse))]
[JsonSerializable(typeof(BffFollowedEntitiesResponse))]
[JsonSerializable(typeof(BffNotificationResponse))]
[JsonSerializable(typeof(BffNotificationsResponse))]
[JsonSerializable(typeof(BffNotificationCountResponse))]
[JsonSerializable(typeof(BffReactionsResponse))]
[JsonSerializable(typeof(BffMyReactionsResponse))]
[JsonSerializable(typeof(BffSearchResponse<BffDiscussionSearchItem>))]
[JsonSerializable(typeof(BffSearchResponse<BffPostSearchItem>))]
[JsonSerializable(typeof(BffUserActivityResponse))]
[JsonSerializable(typeof(BffDailyActivityResponse))]
[JsonSerializable(typeof(BffUserFollowStatusResponse))]
[JsonSerializable(typeof(BffUserStatsResponse))]
[JsonSerializable(typeof(BffEntityResolveResponse))]
[JsonSerializable(typeof(BffDmConversationResponse))]
[JsonSerializable(typeof(BffDmConversationsResponse))]
[JsonSerializable(typeof(BffDmMessageResponse))]
[JsonSerializable(typeof(BffDmMessagesResponse))]
[JsonSerializable(typeof(BffDmUnreadCountResponse))]
[JsonSerializable(typeof(BffDmDeleteMessagesRequest))]
[JsonSerializable(typeof(BffDmPinRequest))]
public partial class BffJsonContext : JsonSerializerContext;
