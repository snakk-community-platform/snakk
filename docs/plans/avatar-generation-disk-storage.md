# Avatar Generation with Disk Storage Implementation Plan

**Status:** Planning
**Created:** 2026-02-04
**Priority:** High (Security Fix)

## Problem Statement

Current avatar generation endpoint (`/avatars/{userId}/generated`) generates SVG on-demand without validating if the user exists. This creates a security vulnerability where attackers can:
- Bypass CDN caching by requesting random user IDs
- Exhaust server CPU resources by forcing SVG generation
- Hammer the database with validation queries (if lazy loading is used)

## Solution Overview

Pre-generate all avatar SVGs as static files on disk, serve via static file middleware, and let CDN cache them naturally. This approach provides:
- ✅ Zero runtime generation cost
- ✅ No database queries from avatar endpoints
- ✅ Perfect CDN integration
- ✅ Eliminates brute force attack vector
- ✅ Simple, maintainable architecture

---

## Phase 1: File Structure & Storage

### Directory Structure
```
/api-root/
  ├── avatars/
  │   ├── generated/
  │   │   ├── users/
  │   │   │   ├── u_abc123.svg
  │   │   │   ├── u_def456.svg
  │   │   │   └── ...
  │   │   ├── hubs/
  │   │   │   ├── h_xyz789.svg
  │   │   │   └── ...
  │   │   ├── spaces/
  │   │   │   ├── s_qrs456.svg
  │   │   │   └── ...
  │   │   └── communities/
  │   │       ├── c_mno789.svg
  │   │       └── ...
  │   └── uploaded/  (existing - user uploads)
  │       ├── u_abc123.jpg
  │       └── ...
```

### File Naming Convention
- User avatars: `users/{userId}.svg`
- Hub avatars: `hubs/{hubId}.svg`
- Space avatars: `spaces/{spaceId}.svg`
- Community avatars: `communities/{communityId}.svg`

### Configuration

**File:** `src/core/Snakk.Api/appsettings.json`

```json
{
  "AvatarSettings": {
    "GeneratedAvatarsPath": "avatars/generated",
    "UploadedAvatarsPath": "avatars/uploaded",
    "DefaultSize": 80,
    "GenerateOnStartup": true,
    "StartupGenerationTimeout": 300000
  }
}
```

---

## Phase 2: Avatar Generation Service

### New Interface: `IAvatarGenerationService`

**Location:** `src/core/Snakk.Application/Services/IAvatarGenerationService.cs`

**Methods:**
```csharp
public interface IAvatarGenerationService
{
    Task<string> GenerateUserAvatarAsync(string userId, int size = 80);
    Task<string> GenerateHubAvatarAsync(string hubId, int size = 80);
    Task<string> GenerateSpaceAvatarAsync(string spaceId, int size = 80);
    Task<string> GenerateCommunityAvatarAsync(string communityId, int size = 80);
    Task<bool> AvatarExistsAsync(string entityType, string entityId);
    Task DeleteAvatarAsync(string entityType, string entityId);
    Task<int> GenerateAllMissingAvatarsAsync();
}
```

### Implementation: `AvatarGenerationService`

**Location:** `src/core/Snakk.Infrastructure/Services/AvatarGenerationService.cs`

**Dependencies:**
- `IWebHostEnvironment` - to get content root path
- `IConfiguration` - for avatar settings
- `Snakk.Shared.Avatars.AvatarGenerator` - existing SVG generator
- `ILogger<AvatarGenerationService>`
- `IUserRepository`, `IHubRepository`, `ISpaceRepository`, `ICommunityRepository`

**Key Methods:**

#### GenerateUserAvatarAsync
```
Logic:
1. Build file path: {ContentRoot}/avatars/generated/users/{userId}.svg
2. Check if file already exists (skip if present)
3. Generate SVG using Snakk.Shared.Avatars.AvatarGenerator.Generate(userId, size)
4. Ensure directory exists (create if needed)
5. Write SVG string to file
6. Set file permissions to read-only (644)
7. Log success
8. Return file path
```

#### GenerateAllMissingAvatarsAsync
```
Logic:
1. Query all user IDs from IUserRepository
2. Query all hub IDs from IHubRepository
3. Query all space IDs from ISpaceRepository
4. Query all community IDs from ICommunityRepository
5. Use Parallel.ForEachAsync with concurrency limit (e.g., 10)
6. For each entity:
   a. Check if avatar file exists
   b. If not exists, generate and save
   c. Log progress every 1000 entities
7. Return total count of generated avatars

Optimization:
- Parallel processing for performance
- Concurrency limit to avoid disk I/O bottleneck
- Progress logging for observability
```

#### DeleteAvatarAsync
```
Logic:
1. Build file path based on entity type and ID
2. Check if file exists
3. If exists, delete file
4. Log deletion
```

### Service Registration

**Location:** `src/core/Snakk.Api/ServiceCollectionExtensions.cs`

```csharp
// Add to AddSnakkServices() method
services.AddScoped<IAvatarGenerationService, AvatarGenerationService>();
```

---

## Phase 3: Startup Avatar Generation Task

### Background Service: `AvatarGenerationHostedService`

**Location:** `src/core/Snakk.Infrastructure/BackgroundJobs/AvatarGenerationHostedService.cs`

**Purpose:** Generate all missing avatars on application startup

**Implementation:**

```csharp
public class AvatarGenerationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AvatarGenerationHostedService> _logger;
    private readonly IConfiguration _configuration;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Check if generation is enabled
        var enabled = _configuration.GetValue<bool>("AvatarSettings:GenerateOnStartup", true);
        if (!enabled)
        {
            _logger.LogInformation("Avatar generation on startup is disabled");
            return;
        }

        _logger.LogInformation("Starting avatar generation for existing entities...");
        var stopwatch = Stopwatch.StartNew();

        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAvatarGenerationService>();

        var count = await service.GenerateAllMissingAvatarsAsync();

        stopwatch.Stop();
        _logger.LogInformation("Generated {Count} avatars in {Duration}ms", count, stopwatch.ElapsedMilliseconds);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

**Service Registration:**

```csharp
services.AddHostedService<AvatarGenerationHostedService>();
```

**Execution Order:**
- Runs AFTER database seeding
- Runs BEFORE accepting HTTP requests
- Non-blocking if disabled in config

---

## Phase 4: Domain Event Handlers

### User Events

#### UserCreatedAvatarGenerationHandler

**Location:** `src/core/Snakk.Infrastructure/EventHandlers/Avatars/UserCreatedAvatarGenerationHandler.cs`

```csharp
public class UserCreatedAvatarGenerationHandler : IDomainEventHandler<UserCreatedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<UserCreatedAvatarGenerationHandler> _logger;

    public async Task HandleAsync(UserCreatedEvent @event)
    {
        try
        {
            await _avatarService.GenerateUserAvatarAsync(@event.UserId);
            _logger.LogInformation("Generated avatar for new user {UserId}", @event.UserId);
        }
        catch (Exception ex)
        {
            // Non-critical - log but don't throw
            _logger.LogError(ex, "Failed to generate avatar for user {UserId}", @event.UserId);
        }
    }
}
```

#### UserDeletedAvatarCleanupHandler

**Location:** `src/core/Snakk.Infrastructure/EventHandlers/Avatars/UserDeletedAvatarCleanupHandler.cs`

```csharp
public class UserDeletedAvatarCleanupHandler : IDomainEventHandler<UserDeletedEvent>
{
    private readonly IAvatarGenerationService _avatarService;
    private readonly ILogger<UserDeletedAvatarCleanupHandler> _logger;

    public async Task HandleAsync(UserDeletedEvent @event)
    {
        try
        {
            await _avatarService.DeleteAvatarAsync("user", @event.UserId);
            _logger.LogInformation("Deleted avatar for user {UserId}", @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete avatar for user {UserId}", @event.UserId);
        }
    }
}
```

### Similar Handlers

Create similar handlers for:
- `HubCreatedAvatarGenerationHandler`
- `HubDeletedAvatarCleanupHandler`
- `SpaceCreatedAvatarGenerationHandler`
- `SpaceDeletedAvatarCleanupHandler`
- `CommunityCreatedAvatarGenerationHandler`
- `CommunityDeletedAvatarCleanupHandler`

### Service Registration

**Location:** `src/core/Snakk.Api/ServiceCollectionExtensions.cs`

```csharp
// Avatar generation event handlers
services.AddScoped<IDomainEventHandler<UserCreatedEvent>, UserCreatedAvatarGenerationHandler>();
services.AddScoped<IDomainEventHandler<UserDeletedEvent>, UserDeletedAvatarCleanupHandler>();
services.AddScoped<IDomainEventHandler<HubCreatedEvent>, HubCreatedAvatarGenerationHandler>();
services.AddScoped<IDomainEventHandler<HubDeletedEvent>, HubDeletedAvatarCleanupHandler>();
// ... etc
```

---

## Phase 5: API Endpoint Changes

### Update GetGeneratedAvatar

**File:** `src/core/Snakk.Api/Endpoints/AvatarEndpoints.cs` (line 136-145)

**Current Implementation:**
```csharp
private static IResult GetGeneratedAvatar(string userId, string? type, HttpContext httpContext)
{
    var typeOverride = Snakk.Shared.Avatars.AvatarGenerator.ParseType(type);
    var svg = Snakk.Shared.Avatars.AvatarGenerator.Generate(userId, 80, typeOverride);
    httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.Content(svg, "image/svg+xml");
}
```

**New Implementation:**
```csharp
private static IResult GetGeneratedAvatar(
    string userId,
    string? type,
    HttpContext httpContext,
    IWebHostEnvironment env)
{
    // Build file path
    var filePath = Path.Combine(
        env.ContentRootPath,
        "avatars",
        "generated",
        "users",
        $"{userId}.svg"
    );

    // Check if file exists
    if (!File.Exists(filePath))
    {
        return Results.NotFound(new { error = "Avatar not found" });
    }

    // Set aggressive cache headers
    httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Serve static file
    return Results.File(filePath, "image/svg+xml", enableRangeProcessing: true);
}
```

**Key Changes:**
- ❌ No on-the-fly generation
- ❌ No database queries
- ✅ Direct file serving
- ✅ 404 if file doesn't exist

### Update GetHubAvatar, GetSpaceAvatar, GetCommunityAvatar

Apply same pattern to:
- `GetHubAvatar` (line 147-158)
- `GetSpaceAvatar` (line 160-170)
- `GetCommunityAvatar` (line 172-182)

### Update GetAvatarAsync

**File:** `src/core/Snakk.Api/Endpoints/AvatarEndpoints.cs` (line 46-134)

**Changes:**
```csharp
// After checking for uploaded avatar (line 125)
// OLD: Generate on-the-fly
// NEW: Check for pre-generated file

// Fall back to generated avatar with optional type override
var generatedPath = Path.Combine(
    env.ContentRootPath,
    "avatars",
    "generated",
    "users",
    $"{cleanUserId}.svg"
);

if (File.Exists(generatedPath))
{
    httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.File(generatedPath, "image/svg+xml", enableRangeProcessing: true);
}

// If neither uploaded nor generated avatar exists, return 404
return Results.NotFound(new { error = "Avatar not found" });
```

---

## Phase 6: Static File Serving Configuration

### Option A: ASP.NET Core Static Files Middleware

**File:** `src/core/Snakk.Api/Program.cs`

**Add before `app.UseRouting()`:**

```csharp
// Serve generated avatars with aggressive caching
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "avatars", "generated")),
    RequestPath = "/avatars/generated",
    OnPrepareResponse = ctx =>
    {
        // 1 year cache for immutable generated avatars
        ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    }
});
```

**Benefits:**
- Built-in to ASP.NET Core
- No external dependencies
- Easy to configure

**Drawbacks:**
- Goes through ASP.NET pipeline (slight overhead)
- Less efficient than nginx

---

### Option B: Nginx Reverse Proxy (Production Recommendation)

**Nginx Configuration:**

```nginx
server {
    listen 80;
    server_name api.snakk.com;

    # Serve generated avatars directly (bypass ASP.NET)
    location /avatars/generated/ {
        alias /app/avatars/generated/;

        # Aggressive caching
        expires 1y;
        add_header Cache-Control "public, immutable";
        add_header X-Content-Type-Options "nosniff";

        # CORS headers (if needed)
        add_header Access-Control-Allow-Origin "*";

        # Return 404 for missing files
        try_files $uri =404;
    }

    # Proxy other requests to ASP.NET Core
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Benefits:**
- ✅ Maximum performance
- ✅ Bypasses ASP.NET Core entirely
- ✅ Industry standard for production
- ✅ Better caching control

**Drawbacks:**
- ✗ Requires nginx setup
- ✗ More complex deployment

---

## Phase 7: CDN Configuration

### CDN Setup (Cloudflare, CloudFront, Azure CDN)

**Cache Rules:**
```
Path pattern: /avatars/generated/*

Settings:
  - Cache TTL: 1 year (31536000 seconds)
  - Cache everything (including 404s)
  - Respect origin cache headers
  - Ignore query strings
  - Browser cache TTL: 1 year
  - Compression: Automatic (gzip/brotli)
```

**Cache Key:**
```
Include:
  - URL path only

Ignore:
  - Query strings
  - Cookies
  - Accept headers
  - User-Agent
```

**Purge Strategy:**
```
When to purge:
  - Never (files are immutable)
  - Exception: Avatar algorithm changes (full purge)
  - Individual purge: Only if user requests regeneration
```

### Optional: CDN Purge Endpoint

**New endpoint for admin use:**

```csharp
POST /api/admin/avatars/purge/{userId}
  - Requires admin authentication
  - Deletes local file
  - Calls CDN purge API
  - Regenerates avatar
  - Returns new avatar URL
```

---

## Phase 8: Migration Strategy

### For Existing Deployments

#### Pre-Deployment Checklist
- [ ] Backup database
- [ ] Estimate disk space needed (1.5 KB per user × user count)
- [ ] Ensure avatars directory exists with write permissions
- [ ] Configure AvatarSettings in appsettings.json
- [ ] Set up nginx configuration (if using)
- [ ] Configure CDN cache rules

#### Deployment Steps
1. Deploy new code to server
2. Application starts
3. `AvatarGenerationHostedService` automatically runs
4. Generates all missing avatars (may take 30-60 seconds for 100k users)
5. Application becomes ready for requests
6. Monitor logs for generation progress

#### Post-Deployment Verification
- [ ] Check logs for "Generated X avatars in Y ms"
- [ ] Verify files exist in avatars/generated/ directory
- [ ] Test avatar URL: GET /avatars/{sampleUserId}/generated
- [ ] Verify response has correct cache headers
- [ ] Check CDN cache hit rate (should be >95% after warmup)

#### Rollback Plan
- Old code still works (generates on-the-fly)
- No database schema changes
- No breaking changes
- Generated files remain on disk (no data loss)
- Simply redeploy previous version if issues occur

---

## Phase 9: Cleanup & Maintenance

### Background Cleanup Job (Optional)

**Purpose:** Remove orphaned avatar files for deleted entities

**Implementation:**

```csharp
public class OrphanedAvatarCleanupJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken); // Run daily

            try
            {
                // Scan avatars/generated/users/ directory
                var files = Directory.GetFiles(avatarPath, "*.svg");
                var userIds = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

                // Query database for existing users
                var existingUsers = await _userRepository.GetAllPublicIdsAsync();

                // Find orphaned files
                var orphaned = userIds.Except(existingUsers).ToList();

                // Delete orphaned files
                foreach (var userId in orphaned)
                {
                    File.Delete(Path.Combine(avatarPath, $"{userId}.svg"));
                }

                _logger.LogInformation("Cleaned up {Count} orphaned avatar files", orphaned.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned avatar cleanup");
            }
        }
    }
}
```

**Trigger:** Daily at 2 AM via background service or scheduled job (Hangfire/Quartz.NET)

---

## Phase 10: Testing Plan

### Unit Tests

**Test: AvatarGenerationService**

```csharp
[Fact]
public async Task GenerateUserAvatarAsync_CreatesFile()
{
    // Arrange
    var service = CreateService();
    var userId = "u_test123";

    // Act
    var path = await service.GenerateUserAvatarAsync(userId);

    // Assert
    Assert.True(File.Exists(path));
    var content = await File.ReadAllTextAsync(path);
    Assert.Contains("<svg", content);
}

[Fact]
public async Task GenerateUserAvatarAsync_SkipsExistingFile()
{
    // Arrange
    var service = CreateService();
    var userId = "u_test456";
    await service.GenerateUserAvatarAsync(userId);
    var firstModified = File.GetLastWriteTime(GetPath(userId));

    // Act
    await service.GenerateUserAvatarAsync(userId);
    var secondModified = File.GetLastWriteTime(GetPath(userId));

    // Assert
    Assert.Equal(firstModified, secondModified); // File not regenerated
}

[Fact]
public async Task DeleteAvatarAsync_RemovesFile()
{
    // Arrange
    var service = CreateService();
    var userId = "u_test789";
    await service.GenerateUserAvatarAsync(userId);

    // Act
    await service.DeleteAvatarAsync("user", userId);

    // Assert
    Assert.False(File.Exists(GetPath(userId)));
}
```

**Test: Event Handlers**

```csharp
[Fact]
public async Task UserCreatedHandler_GeneratesAvatar()
{
    // Arrange
    var handler = CreateHandler();
    var @event = new UserCreatedEvent("u_newuser123");

    // Act
    await handler.HandleAsync(@event);

    // Assert
    Assert.True(File.Exists(GetPath("u_newuser123")));
}
```

### Integration Tests

**Test: Startup Generation**

```csharp
[Fact]
public async Task StartupGeneration_CreatesAllAvatars()
{
    // Arrange
    SeedDatabase(100); // Create 100 test users

    // Act
    var host = CreateHostBuilder().Build();
    await host.StartAsync();
    await Task.Delay(5000); // Wait for background task

    // Assert
    var files = Directory.GetFiles(avatarPath, "*.svg");
    Assert.Equal(100, files.Length);
}
```

**Test: Avatar Endpoint**

```csharp
[Fact]
public async Task GetGeneratedAvatar_Returns200ForExistingAvatar()
{
    // Arrange
    var client = CreateClient();
    await GenerateAvatar("u_exists");

    // Act
    var response = await client.GetAsync("/avatars/u_exists/generated");

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("image/svg+xml", response.Content.Headers.ContentType.MediaType);
    Assert.Contains("max-age=31536000", response.Headers.CacheControl.ToString());
}

[Fact]
public async Task GetGeneratedAvatar_Returns404ForNonExistent()
{
    // Arrange
    var client = CreateClient();

    // Act
    var response = await client.GetAsync("/avatars/u_notexist/generated");

    // Assert
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

### Performance Tests

**Test: Generation Speed**

```csharp
[Fact]
public async Task GenerateAllAvatars_Completes_UnderTimeLimit()
{
    // Arrange
    SeedDatabase(10_000); // 10k users
    var service = CreateService();

    // Act
    var stopwatch = Stopwatch.StartNew();
    var count = await service.GenerateAllMissingAvatarsAsync();
    stopwatch.Stop();

    // Assert
    Assert.Equal(10_000, count);
    Assert.True(stopwatch.ElapsedMilliseconds < 15_000); // Under 15 seconds
}
```

**Test: Static File Serving Performance**

```
Load test:
  - Tool: Apache Bench or wrk
  - Endpoint: GET /avatars/{userId}/generated
  - Concurrent requests: 100
  - Duration: 60 seconds
  - Expected: >10,000 req/s with nginx
  - Expected: <5ms avg response time
```

---

## Phase 11: Monitoring & Observability

### Metrics to Track

**Avatar Generation:**
- `avatar.generation.total` - Counter: Total avatars generated
- `avatar.generation.duration_ms` - Histogram: Generation time per avatar
- `avatar.generation.startup_duration_ms` - Gauge: Startup generation time
- `avatar.generation.errors` - Counter: Failed generations

**Avatar Serving:**
- `avatar.requests.total` - Counter: Total avatar requests
- `avatar.requests.cache_hit` - Counter: Cache hits (CDN)
- `avatar.requests.cache_miss` - Counter: Cache misses
- `avatar.requests.404` - Counter: Not found requests

**Storage:**
- `avatar.storage.total_files` - Gauge: Total avatar files on disk
- `avatar.storage.disk_usage_mb` - Gauge: Disk space used by avatars

### Logging

**Key log events:**
```
- INFO: "Starting avatar generation for existing entities..."
- INFO: "Generated {count} avatars in {duration}ms"
- INFO: "Generated avatar for new user {userId}"
- WARN: "Failed to generate avatar for user {userId}: {error}"
- ERROR: "Avatar generation startup failed: {error}"
- INFO: "Deleted avatar for user {userId}"
- INFO: "Cleaned up {count} orphaned avatar files"
```

### Health Checks

**Add health check endpoint:**

```csharp
GET /health/avatars

Checks:
  1. Avatars directory exists
  2. Write permissions on directory
  3. Sample avatar file exists and is readable

Returns:
  - 200 OK if all checks pass
  - 503 Service Unavailable if any check fails
```

---

## Phase 12: Deployment Checklist

### Pre-Deployment

- [ ] Backup database
- [ ] Verify disk space available (estimate: 1.5 KB × user count)
- [ ] Configure `AvatarSettings` in appsettings.json
- [ ] Set up nginx configuration (if using Option B)
- [ ] Configure CDN cache rules
- [ ] Create avatars/generated directory with correct permissions
- [ ] Run tests in staging environment

### Deployment

- [ ] Deploy new code to server
- [ ] Verify application starts successfully
- [ ] Monitor startup logs for avatar generation progress
- [ ] Wait for "Generated X avatars" log message
- [ ] Verify avatars directory contains files: `ls -l avatars/generated/users/ | wc -l`

### Post-Deployment

- [ ] Test sample avatar URL: `curl -I /avatars/{sampleUserId}/generated`
- [ ] Verify cache headers in response
- [ ] Check CDN cache status
- [ ] Monitor 404 rate (should be near zero)
- [ ] Create test user and verify avatar generates
- [ ] Delete test user and verify avatar is cleaned up
- [ ] Run load test to verify performance
- [ ] Monitor application metrics and logs

### Rollback Procedure

If critical issues occur:
1. Redeploy previous version
2. Old code generates avatars on-the-fly (backwards compatible)
3. Generated files remain on disk (no data loss)
4. Investigate issue and fix
5. Redeploy with fix

---

## Expected Outcomes

### Performance Improvements

| Metric | Before (On-the-fly) | After (Pre-generated) | Improvement |
|--------|---------------------|----------------------|-------------|
| Response time | 50-100ms | <5ms (nginx) | 10-20x faster |
| CPU usage | High (per request) | Zero (after startup) | ~100% reduction |
| Database queries | 1 per request (with validation) | 0 | 100% reduction |
| CDN hit rate | Variable | >95% | Consistent |
| Throughput | 1,000 req/s | 100,000 req/s | 100x increase |

### Resource Usage

**Disk Storage:**
- 100k users: ~150 MB
- 1M users: ~1.5 GB
- 10M users: ~15 GB

**Memory:**
- No Redis needed (saves 50-150 MB)
- Minimal increase in API memory footprint

**Startup Time:**
- Initial: +30-60 seconds for 100k users
- Subsequent: No delay (files already exist)

### Security

- ✅ Brute force attacks: No impact (missing files return 404)
- ✅ CDN bypass attacks: No impact (static files, no generation)
- ✅ Database load attacks: Eliminated (no DB queries)
- ✅ DoS attacks: Mitigated (rate limiting + CDN)

---

## Implementation Timeline

| Phase | Estimated Time | Dependencies |
|-------|---------------|--------------|
| Phase 2: Service Implementation | 4 hours | None |
| Phase 3: Startup Task | 2 hours | Phase 2 |
| Phase 4: Event Handlers | 3 hours | Phase 2 |
| Phase 5: Endpoint Updates | 2 hours | Phase 2 |
| Phase 6: Static File Config | 1 hour | Phase 5 |
| Phase 7: CDN Config | 1 hour | Phase 6 |
| Phase 10: Testing | 4 hours | All phases |
| Phase 11: Monitoring | 2 hours | Phase 5 |
| **Total** | **~19 hours** | |

---

## Success Criteria

- [ ] All existing users have generated avatar files
- [ ] New user registration automatically generates avatar
- [ ] User deletion automatically removes avatar
- [ ] Avatar endpoints return 404 for non-existent users
- [ ] CDN cache hit rate > 95%
- [ ] Response time < 5ms (nginx) or < 20ms (ASP.NET)
- [ ] Zero database queries from avatar endpoints
- [ ] All tests passing
- [ ] No increase in error rate
- [ ] Application startup completes successfully

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Startup generation fails | Low | High | Timeout + fallback to on-the-fly |
| Disk space exhaustion | Low | Medium | Monitor disk usage, alerts |
| Orphaned files accumulate | Medium | Low | Cleanup job runs daily |
| Event handler failure | Medium | Low | Non-critical, log error, don't throw |
| CDN misconfiguration | Low | Medium | Test in staging first |
| File permission issues | Medium | Medium | Set permissions in deployment script |

---

## Future Enhancements

1. **Avatar Customization**
   - Allow users to choose avatar style
   - Regenerate avatar when style changes
   - Multiple style options per user

2. **Avatar Versioning**
   - Filename: `{userId}_v{version}.svg`
   - Allows algorithm updates without purging CDN

3. **WebP/AVIF Support**
   - Generate raster formats for better compression
   - Serve based on Accept header

4. **Object Storage (S3/Azure Blob)**
   - Move from local disk to cloud storage
   - Better for multi-instance deployments

5. **Lazy Regeneration**
   - Mark avatars for regeneration without deleting
   - Regenerate on next request

---

## References

- Current avatar generation: `src/core/Snakk.Shared/Avatars/AvatarGenerator.cs`
- Current endpoint: `src/core/Snakk.Api/Endpoints/AvatarEndpoints.cs` (line 136-145)
- Domain events: `src/core/Snakk.Api/ServiceCollectionExtensions.cs` (line 231-236)
- Background jobs: `src/core/Snakk.Infrastructure/BackgroundJobs/`

---

**Plan Status:** Ready for implementation
**Next Step:** Begin Phase 2 (Service Implementation)
