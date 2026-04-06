# Performance & Memory Audit

Audit performed 2026-03-28. Covers the entire Snakk .NET solution — HTTP clients, database queries, caching, middleware, frontend delivery, and memory patterns.

---

## Critical — Fix First

### 1. Output Cache Attributes Are Ignored (Service Never Registered)

Multiple Razor Pages declare `[OutputCache]` but `AddOutputCache()` is never called in `Snakk.Web/Program.cs` and `UseOutputCache()` is missing from the pipeline. Every page re-renders on every request.

**Affected pages:**
- `Pages/Index.cshtml.cs` — `[OutputCache(PolicyName = "HomePage")]`
- `Pages/Hubs/Index.cshtml.cs` — `[OutputCache(PolicyName = "HomePage")]`
- `Pages/Hubs/Detail.cshtml.cs` — `[OutputCache(PolicyName = "Space")]`
- `Pages/Spaces/Detail.cshtml.cs` — `[OutputCache(PolicyName = "Space")]`
- `Pages/Communities/Index.cshtml.cs` — `[OutputCache(PolicyName = "CommunitiesList")]`

**Fix:** Register `AddOutputCache()` with policy definitions, add `UseOutputCache()` between `UseRouting()` and `MapRazorPages()`.

### 2. Compression Level Set to `Fastest` Instead of `Optimal`

Both `Snakk.Web/Program.cs` and `Snakk.Gateway/Program.cs` use `CompressionLevel.Fastest` for Brotli and Gzip. `Optimal` yields 20-40% smaller responses at minimal CPU cost for a content-heavy platform.

**Files:**
- `src/apps/Snakk.Web/Program.cs` (Brotli ~line 58, Gzip ~line 63)
- `src/services/Snakk.Gateway/Program.cs` (~lines 73-74)

### 3. Missing Database Indexes on Frequently Queried Columns

In `SnakkDbContext.cs`, several commonly filtered columns lack indexes:

| Column | Used In | Impact |
|--------|---------|--------|
| `User.Email` | Login, search, dedup | Full table scan on every login |
| `Post.CreatedByUserId` | Profile pages, user activity | Slow profile rendering |
| `Group.CommunityId` | Hierarchy queries | Slow permission resolution |
| `GroupMember.GroupId` | Group membership checks | Slow access checks |

**Fix:** Add indexes in `OnModelCreating` and create a migration.

---

## High Impact — Significant Gains

### 4. No Resilience Policies (Polly) on Any HttpClient

Zero retry or circuit-breaker policies. Every inter-service call fails immediately on transient errors.

**Affected registrations:**
- `Snakk.Api/ServiceCollectionExtensions.cs` — `AddHttpClient("RealtimeService")`, `AddHttpClient("ActivityBroadcaster")`
- `Snakk.Web/Program.cs` — `AddHttpClient("InternalApi")`
- `Snakk.Infrastructure/Services/WebhookService.cs` — generic `CreateClient()`

**Fix:** Add `Microsoft.Extensions.Http.Resilience` with retry + circuit breaker on all named clients.

### 5. ReportRepository Loads 9 Related Entities Unconditionally

`ReportRepository.GetByPublicIdAsync` chains 9 `.Include()` calls for every report lookup:

```
.Include(r => r.ReporterUser)
.Include(r => r.ReportedPost)
.Include(r => r.ReportedDiscussion)
.Include(r => r.ReportedUser)
.Include(r => r.Reason)
.Include(r => r.ResolvedByUser)
.Include(r => r.Space)
.Include(r => r.Hub)
.Include(r => r.Community)
```

**Fix:** Create a lightweight overload with `.Select()` projection, or split by caller needs.

### 6. SignalR Uses All Default Configuration

`Snakk.Realtime/Program.cs` calls `AddSignalR()` with no options. For a realtime platform, explicitly configure:
- `MaximumReceiveMessageSize`
- `ClientTimeoutInterval`
- `KeepAliveInterval`
- `StreamBufferCapacity`

### 7. Snakk.Api Kestrel Has No Limits Configured

`Snakk.Api/Program.cs` only sets HTTP protocol versions. Compare to Gateway which has MaxConcurrentConnections, HTTP/2 window sizes, and timeouts. The API service handles all gRPC traffic on pure defaults.

### 8. No JSON Source Generators

56 files use `JsonSerializer` with no source generators. For high-throughput endpoints (BFF responses, webhook payloads), source generators eliminate reflection overhead and reduce allocations.

---

## Medium Impact — Worth Doing

### 9. BFF Endpoints Fully Buffer All Responses

`BffApiEndpoints.cs` — every endpoint deserializes, maps, then re-serializes. No streaming, no `IAsyncEnumerable`. Large notification lists are fully materialized in memory.

### 10. Bulk Delete Uses `RemoveRange()` Instead of `ExecuteDeleteAsync()`

`RefreshTokenRepository.cs` loads expired tokens into memory then calls `RemoveRange()`. `ExecuteDeleteAsync()` deletes server-side with zero entity tracking overhead.

### 11. No Explicit `CommandTimeout` on DbContext

`ServiceCollectionExtensions.cs` relies on Npgsql default (30s). Long-running moderation or report queries could time out. Add `.CommandTimeout(60)` to Npgsql options.

### 12. Discussion Detail Page Has Sequential API Calls That Could Be Parallelized

`Discussions/Detail.cshtml.cs` — `GetCurrentUser` and `GetDiscussionResultAsync` run sequentially before the `Task.WhenAll` block. These could be fired in parallel.

### 13. Gateway Rate Limiting is Commented Out

`Snakk.Gateway/Program.cs` — rate limiting code exists but is disabled. Only the inner API has rate limits. A malicious client can overwhelm the gateway without throttling.

### 14. RealtimeHub Static Dictionaries Could Grow Unbounded

`RealtimeHub.cs` uses two static `ConcurrentDictionary` instances (`ViewerCounts`, `ConnectionDiscussions`). Cleaned up on `OnDisconnectedAsync`, but force-closed connections (network drop, process kill) leak entries. Add a periodic cleanup background task.

### 15. Generic `AddHttpClient()` Has No Configuration

`ServiceCollectionExtensions.cs` and `Worker/Program.cs` register generic HttpClients for webhooks with no timeout, handler lifetime, or base address.

---

## Low Impact — Polish

### 16. File Storage Returns Unbounded MemoryStreams

`LocalFileStorage.ReadAsync` and `S3FileStorage.ReadAsync` load entire files into `MemoryStream` with no size validation. Media files are bounded by upload limits, but a guard would be safer.

### ~~17. No ETag Support on BFF Endpoints~~ — SKIPPED

Not applicable. BFF endpoints are only called on page load, not polled on intervals. ETags would add complexity for zero benefit.

### 18. Web-Tier Health Checks Are Trivial

Snakk.Web, Snakk.Auth, and Snakk.Admin map `/health` to `() => Results.Ok()` with no actual health verification. Consider checking gRPC channel connectivity.

### 19. Blocking `GetAwaiter().GetResult()` in gRPC Interceptor

`GrpcAuthInterceptor.cs` blocks a thread pool thread during token refresh. Known gRPC interceptor limitation (documented in code), but a thread pool pressure point under load.

---

## Already Done Well

- **DbContextPool** (128 pool size) with `NoTrackingWithIdentityResolution` globally
- **Split query behavior** prevents cartesian explosions
- **Compiled queries** for hot-path follow checks (`FollowDatabaseRepository.cs`)
- **Excellent `.Select()` projection usage** across ~93 queries — no lazy loading
- **Keyset pagination** with cursor encoding throughout
- **HybridCache** (stampede-safe) registered in API, Web, and Worker
- **PrefetchCacheService** with `Lazy<Task<T>>` request coalescing
- **Structured logging** everywhere — no string interpolation in log calls
- **Static file cache headers** — 1-year immutable on content-addressed media, smart avatar differentiation
- **Dual-cookie CSRF pattern** with SameSite=Strict mutation guard
- **Comprehensive security headers** (CSP, HSTS, X-Frame-Options, Permissions-Policy)
- **gRPC channels** with `EnableMultipleHttp2Connections` on all clients
- **Gateway Kestrel tuning** with proper HTTP/2 window sizes
- **`ExecuteUpdateAsync`** used for bulk updates (notifications, team revisions)
- **Proper `IHttpClientFactory` usage** — no manual `new HttpClient()` anywhere
- **Soft delete global query filters** on all key entities
