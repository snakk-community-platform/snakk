# Realtime Architecture

## Overview

Snakk uses a **swappable realtime notification system** with clean architecture. The API broadcasts events via `IRealtimeNotifier` interface, which can use either:

1. **SignalR Direct** (default) - SignalR hub running in the API process
2. **HTTP Microservice** - Dedicated SignalR service called via HTTP

## Architecture Layers

```
┌─────────────────────────────────────────┐
│         Snakk.Api Endpoints             │
│   (HTTP concerns only, no broadcasting) │
└──────────────────┬──────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────┐
│      Application Services/UseCases      │
│    (orchestrates, calls IRealtimeNotifier)│
└──────────────────┬──────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────┐
│   IRealtimeNotifier (Application layer) │
│           Interface only                │
└──────────────────┬──────────────────────┘
                   │
        ┌──────────┴──────────┐
        ↓                     ↓
┌──────────────────┐  ┌──────────────────┐
│ SignalRRealtime  │  │ HttpRealtime     │
│ Notifier         │  │ Notifier         │
│ (Direct)         │  │ (Microservice)   │
└──────────────────┘  └──────────────────┘
```

## Implementations

### 1. SignalRRealtimeNotifier (Default)

**File:** `Snakk.Infrastructure/Services/SignalRRealtimeNotifier.cs`

**How it works:**
- Uses `IHubContext<SnakkHub>` to broadcast directly
- SignalR hub runs in the same process as API
- Lowest latency, simplest deployment

**When to use:**
- Development
- Small to medium deployments
- Single-instance deployments

### 2. HttpRealtimeNotifier (Microservice)

**File:** `Snakk.Infrastructure/Realtime/HttpRealtimeNotifier.cs`

**How it works:**
- Makes HTTP POST to dedicated SignalR microservice
- Fire-and-forget (failures are logged but don't throw)
- 5-second timeout

**When to use:**
- Large-scale deployments
- Multi-instance API servers
- Want to scale SignalR independently
- Need Redis backplane anyway

## Configuration

### appsettings.json

```json
{
  "Realtime": {
    "Provider": "SignalR",           // or "Http"
    "HttpBaseUrl": "http://localhost:5300",  // for Http provider
    "ApiKey": "your-api-key-here"            // for Http provider
  }
}
```

### Switch to HTTP Microservice

```json
{
  "Realtime": {
    "Provider": "Http",
    "HttpBaseUrl": "http://realtime-service:5300",
    "ApiKey": "secure-api-key-from-env"
  }
}
```

## Fire-and-Forget Pattern

All realtime broadcasts are **fire-and-forget** for performance:

```csharp
// In Application Service
public async Task CreatePostAsync(...)
{
    // 1. Save to database
    var post = new Post(...);
    await _db.SaveChangesAsync();

    // 2. Fire-and-forget broadcast (doesn't block)
    _ = _realtimeNotifier.NotifyPostCreatedAsync(post, author, discussion);

    // 3. Return immediately
    return post;
}
```

**Benefits:**
- Zero latency impact on main request
- Realtime failures don't break core functionality
- Logged for debugging

## API Methods

### IRealtimeNotifier

```csharp
// Post events
Task NotifyPostCreatedAsync(Post post, User author, Discussion discussion);
Task NotifyPostEditedAsync(Post post, User author, Discussion discussion);
Task NotifyPostDeletedAsync(PostId postId, DiscussionId discussionId, bool isHardDelete);

// Reaction events
Task NotifyReactionUpdatedAsync(PostId postId, DiscussionId discussionId, Dictionary<ReactionType, int> counts);

// User notifications
Task NotifyUserAsync(UserId userId, object notification);
Task NotifyUnreadCountUpdatedAsync(UserId userId, int count);
```

## HTTP API Contract (for Microservice)

### POST /api/broadcast

**Request:**
```json
{
  "EventType": "post-created",
  "TargetGroup": "discussion:abc123",
  "TargetId": "posts-container",
  "HtmlContent": "<div>...</div>",
  "SwapStrategy": "beforeend"
}
```

**Response:** 200 OK

**Event Types:**
- `post-created` - New post in discussion
- `post-edited` - Post content updated
- `post-deleted` - Post removed
- `reaction-updated` - Reaction counts changed
- `notification` - User notification
- `notification-count` - Unread count changed

## Future: Redis Implementation

To add Redis-based broadcasting:

1. Create `RedisRealtimeNotifier.cs` in `Infrastructure/Realtime`
2. Implement `IRealtimeNotifier` using Redis pub/sub
3. Update `ServiceCollectionExtensions.cs`:
   ```csharp
   else if (realtimeProvider == "Redis")
   {
       services.AddSingleton<IConnectionMultiplexer>(sp =>
           ConnectionMultiplexer.Connect(
               configuration["Realtime:RedisConnectionString"]!));

       services.AddScoped<IRealtimeNotifier, Infrastructure.Realtime.RedisRealtimeNotifier>();
   }
   ```

## Testing

### Mock for Unit Tests

```csharp
var mockNotifier = new Mock<IRealtimeNotifier>();
var service = new PostService(db, mockNotifier.Object, logger);

await service.CreatePostAsync(...);

mockNotifier.Verify(x => x.NotifyPostCreatedAsync(
    It.IsAny<Post>(),
    It.IsAny<User>(),
    It.IsAny<Discussion>()),
    Times.Once);
```

### Disable in Tests

```json
{
  "Realtime": {
    "Provider": "None"  // Add null implementation if needed
  }
}
```

## Performance

### Current Implementation (SignalR Direct)
- Broadcast latency: <1ms (in-process)
- No serialization overhead
- Direct memory access

### HTTP Microservice
- Broadcast latency: ~5-10ms (network + HTTP)
- JSON serialization
- Fire-and-forget (doesn't block API requests)

### Trade-offs

| Feature | SignalR Direct | HTTP Microservice |
|---------|---------------|-------------------|
| Latency | Lowest | Low |
| Scalability | Limited | High |
| Deployment | Simple | Complex |
| Infrastructure | None | HTTP + optional Redis |
| Multi-instance | Requires Redis | Works out-of-box |

## Clean Architecture Compliance

✅ **Endpoints** - Only handle HTTP concerns
✅ **Application Services** - Call `IRealtimeNotifier` interface
✅ **Interface** - Lives in Application layer
✅ **Implementations** - Live in Infrastructure layer
✅ **Swappable** - Change via configuration, zero code changes

## Summary

- Interface: `IRealtimeNotifier` (Application layer)
- Implementations:
  - `SignalRRealtimeNotifier` (direct, default)
  - `HttpRealtimeNotifier` (microservice, optional)
- Fire-and-forget for performance
- Swappable via configuration
- Clean architecture maintained
- Easy to add Redis implementation later
