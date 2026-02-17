# Snakk Realtime Architecture - Final Design

## Overview

Clean, stateless architecture with dedicated SignalR microservice. **No BFF proxy** for WebSockets.

## Architecture Diagram

```
┌─────────────┐
│   Browser   │
└──────┬──────┘
       │
       ├─ WebSocket ──────→ Snakk.Realtime:5300 (/realtime)
       │                          ↑
       │                          │ HTTP POST /api/broadcast
       └─ HTTP ──→ Snakk.Web:5001 (/bff/*)    (API key auth, fire-and-forget)
                         ↓                     ↑
                   Snakk.Api:5242 ─────────────┘
                   (Firewalled)
```

## Key Design Decisions

### 1. Direct WebSocket Connection ✅

**Browser → Snakk.Realtime** (no proxy through BFF)

**Why:**
- ✅ Keeps BFF stateless (HTTP only)
- ✅ Lower latency (no extra hop)
- ✅ Simpler architecture
- ✅ Easier to scale independently
- ✅ Fewer failure points

**Not a security issue:**
- SignalR hub is meant to be public
- Auth happens at application layer
- API key protects the HTTP broadcast endpoints

### 2. Fire-and-Forget Broadcasting

API posts events to Realtime service asynchronously:

```csharp
// In Application Service
_ = _realtimeNotifier.NotifyPostCreatedAsync(post, author, discussion);
```

- Zero latency impact on main request
- Failures are logged but don't break core functionality
- 5-second timeout on HTTP client

### 3. API Completely Firewalled

**Exposed to Internet:**
- Snakk.Web (BFF) - port 5001
- Snakk.Realtime - port 5300

**Firewalled (Internal Only):**
- Snakk.Api - port 5242

## Services

### Snakk.Web (BFF)
- **Role**: Backend-for-Frontend, SSR
- **Exposes**:
  - HTTP endpoints (/bff/*) - proxied to API
  - Razor Pages (server-side rendering)
- **State**: Stateless (HTTP only)
- **Scales**: Horizontally (no shared state)

### Snakk.Realtime
- **Role**: SignalR hub for browser connections
- **Exposes**:
  - WebSocket hub (/realtime) - for browsers
  - HTTP broadcast API (/api/broadcast) - for internal services
- **State**: Stateful (WebSocket connections)
- **Scales**: Horizontally with Redis backplane

### Snakk.Api
- **Role**: Internal API (business logic)
- **Exposes**: Nothing (firewalled)
- **State**: Stateless
- **Scales**: Horizontally

## Configuration

### Browser (JavaScript)
```javascript
// In _Layout.cshtml
window.realtimeServiceUrl = 'http://localhost:5300/realtime';

// In realtime.js
const connection = new signalR.HubConnectionBuilder()
    .withUrl(window.realtimeServiceUrl)
    .build();
```

### Snakk.Web (appsettings.json)
```json
{
  "ApiBaseUrl": "http://localhost:5242",
  "RealtimeServiceUrl": "http://localhost:5300/realtime"
}
```

### Snakk.Api (appsettings.json)
```json
{
  "Realtime": {
    "BaseUrl": "http://localhost:5300",
    "ApiKey": "dev-secret-key-change-in-production"
  }
}
```

### Snakk.Realtime (appsettings.json)
```json
{
  "ApiKey": "dev-secret-key-change-in-production",
  "Cors": {
    "AllowedOrigins": "http://localhost:5001,https://localhost:7001"
  }
}
```

## Event Flow

### Post Created Example

```
1. User creates post in browser
   ↓
2. Browser → Snakk.Web /bff/posts (HTTP)
   ↓
3. Snakk.Web → Snakk.Api /api/posts (HTTP)
   ↓
4. Snakk.Api:
   - Saves post to database
   - Calls IRealtimeNotifier.NotifyPostCreatedAsync()
   ↓
5. HttpRealtimeNotifier → Snakk.Realtime /api/broadcast (HTTP POST)
   ↓
6. Snakk.Realtime → All connected browsers (WebSocket)
   ↓
7. Browser receives update, updates DOM
```

## Security

### Snakk.Realtime Security

**WebSocket Hub (/realtime):**
- ✅ Public (no auth required)
- ✅ CORS restricted
- ✅ Users can only subscribe to public groups
- ✅ Application-layer auth prevents unauthorized data access

**HTTP Broadcast API (/api/broadcast):**
- ✅ API Key required (X-Api-Key header)
- ✅ Only internal services (Snakk.Api) can call
- ✅ Validates API key on every request

### Snakk.Api Security

- ✅ Completely firewalled (not exposed to internet)
- ✅ Only accessible by Snakk.Web (BFF)
- ✅ JWT authentication on all endpoints

## Scaling

### Single Instance (Development)
No additional setup. All services run on localhost.

### Multi-Instance (Production)

**Snakk.Web:**
- Stateless, scale horizontally behind load balancer
- No shared state needed

**Snakk.Api:**
- Stateless, scale horizontally
- Share database connection string only

**Snakk.Realtime:**
- Add Redis backplane for scaling:
  ```csharp
  builder.Services.AddSignalR()
      .AddStackExchangeRedis(configuration["Redis:ConnectionString"]);
  ```
- All instances share Redis for SignalR groups
- Load balancer with sticky sessions (optional) or Redis backplane (required)

## Deployment

### Docker Compose Example

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: snakk
      POSTGRES_USER: snakk
      POSTGRES_PASSWORD: password

  api:
    build: ./src/core/Snakk.Api
    environment:
      ConnectionStrings__DbConnection: "Host=postgres;Database=snakk;..."
      Realtime__BaseUrl: "http://realtime:5300"
      Realtime__ApiKey: "${API_KEY}"
    networks:
      - internal  # Not exposed to internet

  realtime:
    build: ./src/services/Snakk.Realtime
    ports:
      - "5300:5300"
    environment:
      ApiKey: "${API_KEY}"
      Cors__AllowedOrigins: "https://snakk.app"

  web:
    build: ./src/clients/Snakk.Web
    ports:
      - "5001:5001"
    environment:
      ApiBaseUrl: "http://api:5242"
      RealtimeServiceUrl: "https://realtime.snakk.app/realtime"

networks:
  internal:  # Private network for API
```

## Performance

### Latency Comparison

**With BFF Proxy (Rejected):**
- Browser → BFF (proxy) → Realtime = 2 network hops
- ~20-50ms latency

**Direct Connection (Current):**
- Browser → Realtime = 1 network hop
- ~5-10ms latency

### Load Distribution

| Service | Role | Scalability | State |
|---------|------|-------------|-------|
| Snakk.Web | HTTP proxy + SSR | Horizontal | Stateless |
| Snakk.Api | Business logic | Horizontal | Stateless |
| Snakk.Realtime | WebSocket hub | Horizontal (Redis) | Stateful |

## Monitoring

### Key Metrics

**Snakk.Realtime:**
- Connected clients count
- Broadcast rate (messages/sec)
- Failed broadcast count
- Connection churn rate

**Snakk.Api:**
- Broadcast HTTP POST latency
- Failed broadcast count
- API response times

**Snakk.Web:**
- BFF request rate
- Error rates

## Trade-offs & Alternatives Considered

### ❌ Option A: BFF WebSocket Proxy
```
Browser → Snakk.Web (proxy) → Snakk.Realtime
```
- ❌ Makes BFF stateful
- ❌ Extra network hop
- ❌ More complex

### ❌ Option B: SignalR in API
```
Browser → Snakk.Api (SignalR + business logic)
```
- ❌ Exposes API to internet
- ❌ Violates firewall requirement
- ❌ Can't scale independently

### ✅ Option C: Direct Connection (Current)
```
Browser → Snakk.Realtime (dedicated service)
Snakk.Api → Snakk.Realtime (HTTP broadcast)
```
- ✅ Clean separation of concerns
- ✅ All services stateless except Realtime
- ✅ API stays firewalled
- ✅ Simple, performant

## Summary

This architecture achieves:
- ✅ **Stateless BFF** - HTTP only, no WebSocket proxying
- ✅ **Firewalled API** - Never exposed to browsers
- ✅ **Direct connections** - Lower latency, simpler
- ✅ **Fire-and-forget** - No performance impact on API
- ✅ **Scalable** - Each service scales independently
- ✅ **Secure** - Proper auth at each layer

Clean, simple, fast. 🚀
