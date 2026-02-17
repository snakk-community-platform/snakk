# Snakk.Realtime - SignalR Microservice

Dedicated SignalR service for real-time browser updates. Keeps the main API firewalled.

## Architecture

```
Browser → WebSocket → Snakk.Realtime (this service)
                            ↑
                            │ HTTP POST
                    Snakk.Api (firewalled)
```

## Features

- **SignalR Hub** at `/realtime` - Browser WebSocket connections
- **HTTP Broadcast API** at `/api/broadcast` - For Snakk.Api to push events
- **API Key Auth** - Protects broadcast endpoints
- **Group Subscriptions** - Discussion, space, hub, user-specific updates

## Running

```bash
cd src/services/Snakk.Realtime
dotnet run
```

Runs on `http://localhost:5300` by default.

## Configuration

**appsettings.json:**
```json
{
  "ApiKey": "your-secure-key-here",
  "Cors": {
    "AllowedOrigins": "http://localhost:5001,https://localhost:7001"
  }
}
```

**Environment Variables (Production):**
```bash
ASPNETCORE_URLS="http://0.0.0.0:5300"
ApiKey="production-secure-key"
Cors__AllowedOrigins="https://snakk.app"
```

## API Endpoints

### POST /api/broadcast
Broadcast realtime event to browsers.

**Headers:**
- `X-Api-Key`: Your API key

**Body:**
```json
{
  "EventType": "post-created",
  "TargetGroup": "discussion:abc123",
  "TargetId": "posts-container",
  "HtmlContent": "<div>...</div>",
  "SwapStrategy": "beforeend"
}
```

### POST /api/broadcast/activity
Broadcast admin activity event.

**Headers:**
- `X-Api-Key`: Your API key

**Body:**
```json
{
  "ActivityType": "post-created",
  "TargetGroup": "admin-activity",
  "Data": { ... }
}
```

## SignalR Hub Methods

### Client → Server

- `SubscribeToGlobal()` - Subscribe to global updates
- `SubscribeToDiscussion(discussionId)` - Subscribe to discussion updates
- `UnsubscribeFromDiscussion(discussionId)` - Unsubscribe from discussion
- `SubscribeToSpace(hubSlug, spaceSlug)` - Subscribe to space updates
- `UnsubscribeFromSpace(hubSlug, spaceSlug)` - Unsubscribe from space
- `SubscribeToHub(hubSlug)` - Subscribe to hub updates
- `UnsubscribeFromHub(hubSlug)` - Unsubscribe from hub
- `SubscribeToUserNotifications(userId)` - Subscribe to user notifications

### Server → Client

- `ReceiveUpdate(message)` - Realtime update event
- `ReceiveActivity(activity)` - Admin activity event

## Security

- ✅ API Key required for all `/api/*` endpoints
- ✅ CORS restricted to known origins
- ✅ No authentication required for WebSocket (handled by Snakk.Web BFF)
- ✅ SignalR hub is public (auth happens at application layer)

## Scaling

### Single Instance (Default)
No additional setup needed. All connections in memory.

### Multi-Instance (Redis Backplane)
Add Redis backplane for load balancing across multiple instances:

```csharp
// In Program.cs
builder.Services.AddSignalR()
    .AddStackExchangeRedis(configuration["Redis:ConnectionString"]!);
```

```json
// appsettings.json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Monitoring

### Health Check
```bash
curl http://localhost:5300/health
```

### Metrics (Future)
- Connected clients count
- Broadcast rate
- Message throughput
