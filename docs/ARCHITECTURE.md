# Snakk Platform - Architecture Documentation

**Last Updated**: 2026-02-17
**Version**: Current Production State
**Status**: ✅ ACTIVE

---

## Table of Contents
1. [Solution Overview](#solution-overview)
2. [Technology Stack](#technology-stack)
3. [Architectural Patterns](#architectural-patterns)
4. [Project Structure](#project-structure)
5. [Code Conventions](#code-conventions)
6. [Security](#security)
7. [Deployment](#deployment)

---

## Solution Overview

Snakk is a hierarchical community discussion platform built on .NET 10 with a microservices architecture and strict adherence to Clean Architecture principles.

### Core Services

| Service | Technology | Port | Access | Purpose |
|---------|-----------|------|--------|---------|
| **Snakk.Api** | .NET 10 + ASP.NET | 17100 | Internal (firewalled) | Business logic REST API |
| **Snakk.Web** | .NET 10 + Razor Pages | 17200 | Public | Main platform + BFF layer |
| **Snakk.Realtime** | .NET 10 + SignalR | 17101 | Public | WebSocket hub for real-time updates |
| **Snakk.AdminWeb** | .NET 10 + Blazor | 17201 | Internal | Admin dashboard |

### Architecture Diagram

```
                    ┌─────────────────────┐
                    │   Browser Client    │
                    └──────────┬──────────┘
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│  Snakk.Web      │  │ Snakk.Realtime  │  │ Snakk.AdminWeb  │
│  (BFF + SSR)    │  │   (SignalR)     │  │    (Blazor)     │
│  Port 17200     │  │   Port 17101    │  │   Port 17201    │
└────────┬────────┘  └────────┬────────┘  └────────┬────────┘
         │                    │                    │
         │ HTTP               │ HTTP               │ HTTP + Sdk
         │                    │ (broadcast)        │
         │                    │                    │
         └────────────────────┼────────────────────┘
                              ▼
                    ┌─────────────────────┐
                    │    Snakk.Api        │
                    │   (Internal API)    │
                    │    Port 17100       │
                    │   🔒 Firewalled     │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌──────────────────┐
                    │   PostgreSQL     │
                    │    Database      │
                    └──────────────────┘
```

---

## Technology Stack

### Backend (.NET)
- **.NET 10** - Latest LTS framework
- **ASP.NET Core** - Minimal APIs for endpoints
- **Entity Framework Core 10** - ORM with PostgreSQL
- **SignalR** - Real-time communication (dedicated service)
- **BCrypt.NET** - Password hashing
- **FluentValidation** - Request validation
- **ImageSharp** - Avatar generation

### Frontend (Snakk.Web)
- **Razor Pages** - Server-side rendering (SSR)
- **HTMX** - HTML-first interactive enhancements
- **Tailwind CSS** - Utility-first CSS framework
- **daisyUI** - Tailwind component library
- **Vanilla JavaScript ES6+** - IIFE modules, event delegation
- **SignalR Client** - WebSocket connection

### Admin Panel (Snakk.AdminWeb)
- **Blazor Server** - .NET UI framework
- **Microsoft Fluent UI** - Component library
- **Cookie Authentication** - Admin session management

### Database & Storage
- **PostgreSQL** - Primary database
- **Local File Storage** - Avatar storage with XxHash32 sharding (256 folders)
  - Configurable via `FileStorage:BasePath` in appsettings.json
  - Served from `/avatars` endpoint
  - CDN-ready structure (can be migrated to S3/Azure Blob Storage)

### Authentication & Security
- **JWT Bearer Tokens** - API authentication
- **Cookie Authentication** - Admin panel
- **OAuth 2.0** - Google, GitHub, Discord (configured)
- **Two-Factor Authentication** - TOTP-based

---

## Architectural Patterns

### 1. Clean Architecture (C# Backend)

**MANDATORY RULES:**
- ✅ API layer: HTTP concerns ONLY (no business logic)
- ✅ Application layer: DTOs, service interfaces, use cases
- ✅ Infrastructure layer: Service implementations
- ✅ Domain layer: Business logic (NO external dependencies)
- ❌ **FORBIDDEN**: API layer referencing Infrastructure types

**Layer Dependencies:**
```
┌─────────────────────────────┐
│ Snakk.Api (Endpoints)       │  ← Controllers/Endpoints only
├─────────────────────────────┤
│ Snakk.Application           │  ← DTOs, interfaces, use cases
│ (No Infrastructure deps)    │
├─────────────────────────────┤
│ Snakk.Infrastructure        │  ← Service implementations
├─────────────────────────────┤
│ Snakk.Infrastructure.       │  ← Database entities, DbContext
│ Database                    │
├─────────────────────────────┤
│ Snakk.Domain                │  ← Pure business logic
│ (No external dependencies)  │
└─────────────────────────────┘
```

**Example Violation (WRONG):**
```csharp
// ❌ BAD: API endpoint directly injecting DbContext
public static void MapPostEndpoints(this IEndpointRouteBuilder app)
{
    app.MapPost("/posts", async (SnakkDbContext context, ...) =>
    {
        // This violates Clean Architecture!
    });
}
```

**Correct Approach:**
```csharp
// ✅ GOOD: API endpoint using Application layer service
public static void MapPostEndpoints(this IEndpointRouteBuilder app)
{
    app.MapPost("/posts", async (IPostService postService, ...) =>
    {
        // Service interface from Application layer
        var result = await postService.CreatePostAsync(...);
    });
}
```

### 2. Backend-for-Frontend (BFF) Pattern

**MANDATORY RULES:**
- ✅ JavaScript calls `/bff/*` endpoints ONLY
- ✅ BFF proxies requests to internal API
- ✅ SignalR connects directly to Snakk.Realtime (not proxied)
- ❌ **FORBIDDEN**: Direct `fetch()` to `/api/*` from browser
- ❌ **FORBIDDEN**: Exposing API URLs to frontend

**Request Flow:**
```
Browser
  ├── HTTP GET /bff/posts?page=1
  │     ↓
  │   Snakk.Web (BFF)
  │     ↓ HTTP GET /api/posts?page=1
  │   Snakk.Api
  │     ↓
  │   Response (DTO)
  │     ↓
  │   Transform if needed
  │     ↓
  │   Return to browser
  │
  └── WebSocket connect → Snakk.Realtime:5300
                            (Direct connection, no proxy)
```

**Rationale:**
- **Security**: API is firewalled, only accessible to BFF
- **Validation**: BFF can sanitize/validate requests
- **Flexibility**: Can change internal API without breaking frontend
- **Auth Conversion**: BFF handles JWT/cookie conversion

**Why SignalR is NOT proxied:**
- WebSocket connections are stateful (BFF should be stateless)
- Lower latency (1 hop vs 2 hops)
- Easier to scale independently
- SignalR has app-level authentication

### 3. Event-Driven Architecture

**Domain Events:**
- Events raised by domain entities: `PostCreatedEvent`, `DiscussionCreatedEvent`, `ReactionAddedEvent`
- Handled by Infrastructure layer event handlers
- Triggers: Database updates, cache invalidation, real-time broadcasts

**Event Flow:**
```
1. User submits post in browser
2. BFF endpoint calls API
3. API UseCase creates Post domain entity
4. Post entity raises PostCreatedEvent
5. Domain event dispatcher invokes handlers
6. Handler calls IRealtimeNotifier.NotifyAsync()
7. Notifier POSTs to Snakk.Realtime:/api/broadcast
8. Realtime hub broadcasts to connected clients
9. Browser receives update via SignalR
10. JavaScript updates DOM
```

### 4. Use Case Pattern

Each business operation is encapsulated in a use case class:

```csharp
public class PostUseCase
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public async Task<Result<Post>> CreatePostAsync(CreatePostCommand command)
    {
        // 1. Validate
        // 2. Create domain entity
        var post = Post.Create(...);

        // 3. Save to repository
        await _postRepository.AddAsync(post);

        // 4. Dispatch domain events
        await _eventDispatcher.DispatchAsync(post.DomainEvents);

        // 5. Broadcast real-time update
        await _realtimeNotifier.NotifyPostCreatedAsync(post);

        return Result.Success(post);
    }
}
```

---

## Project Structure

### Core Libraries

```
src/core/
├── Snakk.Domain/                    # Pure business logic
│   ├── Entities/                    # Domain entities (User, Post, Discussion)
│   ├── ValueObjects/                # UserId, PostId, DiscussionId
│   ├── Events/                      # Domain events
│   ├── Repositories/                # Repository interfaces
│   └── Exceptions.cs                # Domain exceptions
│
├── Snakk.Application/               # Application layer (DTOs, use cases)
│   ├── DTOs/                        # Data transfer objects
│   ├── UseCases/                    # Business orchestration
│   ├── Services/                    # Service interfaces
│   └── Validators/                  # FluentValidation validators
│
├── Snakk.Infrastructure/            # Service implementations
│   ├── Adapters/                    # External service adapters
│   ├── Database/                    # Database repositories
│   ├── EventHandlers/               # Domain event handlers
│   ├── Events/                      # Event dispatcher
│   ├── Hubs/                        # SignalR hub implementations
│   ├── Mappers/                     # DTO/Entity mappers
│   ├── Realtime/                    # Real-time notification services
│   └── Services/                    # IPostService, IUserService implementations
│
├── Snakk.Infrastructure.Database/   # Database layer
│   ├── Entities/                    # Database entities (*DatabaseEntity)
│   ├── Migrations/                  # EF Core migrations
│   ├── SnakkDbContext.cs            # DbContext
│   └── Configurations/              # Fluent API configurations
│
├── Snakk.Shared/                    # Shared utilities
│   ├── Enums/                       # UserRoleTypeEnum, NotificationType
│   └── Extensions/                  # String extensions, etc.
│
└── Snakk.Sdk/                       # Client SDK
    └── SnakkApiClient.cs            # HTTP client for admin panel
```

### Services

```
src/services/
├── Snakk.Api/                       # Internal REST API
│   ├── Endpoints/                   # Endpoint groups (AuthEndpoints, PostEndpoints)
│   ├── Services/                    # CurrentUserService, JwtTokenService
│   ├── Authorization/               # Authorization handlers
│   ├── Middleware/                  # Custom middleware
│   └── Program.cs                   # Application entry point
│
└── Snakk.Realtime/                  # SignalR hub
    ├── Hubs/                        # SignalR hubs
    ├── Controllers/                 # Broadcast API controller
    └── Program.cs                   # Service entry point
```

### Applications

```
src/apps/
├── Snakk.Web/                       # Main platform (Razor Pages + BFF)
│   ├── Pages/                       # Razor Pages (Index, Auth, Profile)
│   ├── Endpoints/                   # BFF API endpoints
│   ├── Services/                    # SnakkApiClient, CurrentUserService
│   ├── Middleware/                  # URL rewrite, error handling
│   ├── Scripts/                     # TypeScript source files
│   │   ├── core/                    # auth.ts, theme.ts, utils.ts
│   │   ├── components/              # auth-navbar.ts, site.ts
│   │   ├── services/                # realtime.ts, cache-manager.ts
│   │   ├── pages/                   # discussion-detail.ts, profile.ts
│   │   └── enums/                   # user-role-type.ts
│   └── wwwroot/
│       ├── js/
│       │   ├── dist/                # Compiled TypeScript → JavaScript
│       │   │   ├── core/
│       │   │   ├── components/
│       │   │   ├── services/
│       │   │   ├── pages/
│       │   │   └── enums/
│       │   └── vendor/              # htmx.min.js, signalr.min.js
│       └── css/
│           ├── dist/                # Compiled Tailwind CSS
│           └── vendor/              # Third-party CSS
│
└── Snakk.AdminWeb/                  # Admin panel (Blazor Server)
    ├── Pages/                       # Razor Pages (Dashboard, Auth)
    ├── Components/                  # Blazor components
    │   ├── Layout/                  # MainLayout, Sidebar
    │   └── Pages/                   # User management, Content management
    ├── Services/                    # AdminApiClientService
    ├── Middleware/                  # AdminAuthenticationMiddleware
    └── wwwroot/
        ├── js/core/                 # auth-check.js, utils.js, actions.js
        └── css/                     # admin.css, site.css
```

---

## Code Conventions

### C# Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| **Domain Entity** | PascalCase (no suffix) | `User`, `Post`, `Discussion` |
| **Database Entity** | PascalCase + "DatabaseEntity" | `UserDatabaseEntity`, `PostDatabaseEntity` |
| **DTO** | PascalCase + "Dto"/"Request"/"Response" | `CreatePostDto`, `LoginRequest`, `UserProfileResponse` |
| **Service Interface** | I + PascalCase + "Service" | `IUserService`, `IPostService` |
| **Service Implementation** | PascalCase + "Service" | `UserService`, `PostService` |
| **Use Case** | PascalCase + "UseCase" | `UserUseCase`, `PostUseCase` |
| **Value Object** | PascalCase (entity name + "Id") | `UserId`, `PostId`, `DiscussionId` |
| **Endpoint Class** | PascalCase + "Endpoints" | `AuthEndpoints`, `PostEndpoints` |
| **Repository Interface** | I + Entity + "Repository" | `IUserRepository`, `IPostRepository` |

**Entity Property Naming (CRITICAL):**
- `FollowDatabaseEntity`: Use `FollowedUser`, `Space`, `Discussion` (NOT TargetUser, TargetCommunity)
- `ReactionDatabaseEntity`: Use `Type`, only has `Post` property (NOT ReactionType)
- `UserDatabaseEntity`: Use `DisplayName` (NOT Username)
- `RefreshTokenDatabaseEntity`: NO `LastUsedAt` property

**EF Core Relationship Configuration:**
```csharp
// ✅ ALWAYS specify the collection explicitly
modelBuilder.Entity<UserRoleDatabaseEntity>()
    .HasOne(ur => ur.User)
    .WithMany(u => u.Roles)  // Explicit collection!
    .HasForeignKey(ur => ur.UserId);

// ❌ NEVER use empty WithMany() - creates shadow FK
modelBuilder.Entity<UserRoleDatabaseEntity>()
    .HasOne(ur => ur.User)
    .WithMany()  // Creates "UserDatabaseEntityId" shadow FK!
    .HasForeignKey(ur => ur.UserId);
```

### JavaScript Conventions

**Module Pattern (IIFE):**
```javascript
// auth.js
(function() {
    'use strict';

    function setToken(token) { /* ... */ }
    function getToken() { /* ... */ }

    // Export to window
    window.snakkAuth = {
        setToken,
        getToken,
        clearToken,
        isAuthenticated,
        getAuthHeaders
    };
})();
```

**Event Delegation:**
```javascript
// Modern event delegation pattern
document.addEventListener('click', (e) => {
    const deleteBtn = e.target.closest('[data-action="delete"]');
    if (deleteBtn) {
        handleDelete(deleteBtn.dataset.id);
    }
});
```

**Custom Events:**
```javascript
// Dispatch custom events
document.dispatchEvent(new CustomEvent('snakk:auth:token-set', {
    detail: { userId, displayName }
}));

// Listen for custom events
document.addEventListener('snakk:auth:token-set', (e) => {
    console.log('User logged in:', e.detail.userId);
});
```

**Security Rules:**
- ✅ Use `window.SnakkUtils.escapeHtml()` for user content
- ✅ Use `textContent` instead of `innerHTML` for text
- ❌ NO `innerHTML` with user-generated content
- ❌ NO `eval()` or `new Function()`
- ❌ NO inline `onclick` handlers

### API Response Formats

**Standard Paginated Response:**
```json
{
  "items": [...],
  "total": 150,
  "page": 1,
  "pageSize": 20
}
```

**Failed Login Attempts:**
```json
{
  "failures": [...],
  "suspiciousIps": [...],
  "total": 50,
  "page": 1,
  "pageSize": 20
}
```

**Success Response:**
```json
{
  "data": { ... }
}
```

**Error Response:**
```json
{
  "error": "Error message",
  "code": "ERROR_CODE",
  "details": { ... }
}
```

---

## Security

### Authentication Flow

**JWT Bearer Tokens (API):**
```
1. User logs in via /bff/auth/login
2. BFF calls /api/auth/login
3. API validates credentials
4. API generates JWT + refresh token
5. BFF returns tokens to browser
6. JavaScript stores JWT in localStorage
7. Subsequent API calls include JWT in Authorization header
```

**Cookie Authentication (Admin Panel):**
```
1. Admin logs in via /admin/auth/login
2. Blazor validates credentials via Sdk.SnakkApiClient
3. Sets authentication cookie
4. Cookie automatically sent with each request
```

### Authorization

**Role-Based Access Control (RBAC):**
- `GlobalAdmin` - Platform-wide access
- `CommunityAdmin` - Community-level access
- `CommunityMod` - Community moderation
- `HubMod` - Hub moderation
- `SpaceMod` - Space moderation

**Hierarchical Permissions:**
```
GlobalAdmin → Full access to everything
CommunityAdmin → Community + all child hubs/spaces/discussions
HubMod → Hub + all child spaces/discussions
SpaceMod → Space + child discussions only
```

**Usage:**
```csharp
app.MapDelete("/api/communities/{id}", DeleteCommunityAsync)
    .RequireGlobalAdmin();  // Only GlobalAdmin

app.MapPost("/api/hubs/{hubId}/posts", CreatePostAsync)
    .RequireHubModerator("hubId");  // HubMod or higher
```

### Security Best Practices

**API Security:**
- CORS: Whitelist only Snakk.Web and Snakk.AdminWeb origins
- Rate Limiting: Per-endpoint limits (auth, posts, API calls)
- HTTPS Only: Enforce TLS 1.2+
- JWT Validation: Signature, expiration, issuer, audience

**Frontend Security:**
- XSS Prevention: Escape all user content with `escapeHtml()`
- CSRF Protection: SameSite cookies + anti-forgery tokens
- Content Security Policy: Restrict inline scripts, external resources
- Input Validation: Server-side validation on all inputs

---

## Static File Serving & Storage

### wwwroot Serving

Snakk.Web serves static files from wwwroot at the root path (not prefixed):
- `/css/dist/*` → Compiled Tailwind CSS
- `/css/vendor/*` → Third-party CSS libraries
- `/js/dist/*` → Compiled TypeScript output
- `/js/vendor/*` → Third-party JavaScript (HTMX, SignalR)
- `/robots.txt`, `/favicon.ico` → Root-level files

**Cache Policy:**
- Production: 1 year cache (`public,max-age=31536000`)
- Development: No caching

### Avatar Storage

User-generated avatars are served from a configurable storage path.

**Configuration (`appsettings.json`):**
```json
"FileStorage": {
  "BasePath": "c:\\Snakk\\storage",
  "PublicUrlBase": "/avatars"
}
```

**URL Structure:**
- Generated avatars: `/avatars/generated/{type}/{shard}/{id}.svg`
  - Example: `/avatars/generated/users/4a/01H2K3M4N5P6Q7R8S9T0A1B2.svg`
  - Cached: 1 year with `immutable` directive (deterministic SVGs, never change)

- Uploaded avatars: `/avatars/uploaded/{type}/{shard}/{id}-r{revision}.svg`
  - Example: `/avatars/uploaded/users/4a/01H2K3M4N5P6Q7R8S9T0A1B2-r5.svg`
  - Cached: 1 hour (can be updated by users)

**Sharding Strategy:**
- Uses XxHash32 to distribute avatars across 256 folders (00-ff)
- Prevents filesystem bottlenecks with large file counts
- Example: User ID `01H2K3M4N5P6Q7R8S9T0A1B2` hashes to shard `4a`

**Helper Usage:**
```csharp
// C# - Uses AvatarHelper from Snakk.Shared
var url = AvatarHelper.GetAvatarUrl(userId, AvatarEntityType.User, revision: 0);
// Returns: "/avatars/generated/users/4a/01H2K3M4N5P6Q7R8S9T0A1B2.svg"

// C# - Via SnakkUrlHelper (wraps AvatarHelper)
var url = SnakkUrlHelper.UserAvatar(userId, revision: 0);

// TypeScript - In site.ts
getAvatarUrl('users', userId);
// Returns: "/avatars/generated/users/01H2K3M4N5P6Q7R8S9T0A1B2.svg"
```

**Physical Storage:**
```
C:\Snakk\storage\
└── avatars\
    ├── generated\
    │   ├── users\
    │   │   ├── 00\
    │   │   ├── 01\
    │   │   ├── ...
    │   │   └── ff\
    │   ├── communities\
    │   ├── hubs\
    │   └── spaces\
    └── uploaded\
        └── users\
            ├── 00\
            └── ...
```

**Why `/avatars` endpoint?**
- Clear separation from wwwroot static assets
- Configurable storage location (can be moved to CDN/S3)
- Independent cache policies (generated vs uploaded)
- Security: Can add authorization middleware for private avatars

---

## Deployment

### Environment Variables

**Snakk.Api:**
```env
ConnectionStrings__DefaultConnection=Host=localhost;Database=snakk;Username=snakk;Password=***
Jwt__SecretKey=***
Jwt__Issuer=Snakk
Jwt__Audience=Snakk
Jwt__ExpirationMinutes=15
OAuth__Google__ClientId=***
OAuth__Google__ClientSecret=***
OAuth__GitHub__ClientId=***
OAuth__GitHub__ClientSecret=***
OAuth__Discord__ClientId=***
OAuth__Discord__ClientSecret=***
FileStorage__BasePath=c:\Snakk\storage
FileStorage__PublicUrlBase=/avatars
RealtimeServiceUrl=http://localhost:17101
```

**Snakk.Web:**
```env
ApiBaseUrl=https://localhost:17100
RealtimeServiceUrl=https://localhost:17101
FileStorage__BasePath=c:\Snakk\storage
FileStorage__PublicUrlBase=/avatars
```

**Snakk.Realtime:**
```env
ApiBaseUrl=http://localhost:17100
AllowedOrigins=https://localhost:17200,https://localhost:17201
```

### Docker Compose (Production Example)

```yaml
version: '3.8'
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: snakk
      POSTGRES_USER: snakk
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data

  api:
    image: snakk-api:latest
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=snakk;Username=snakk;Password=${DB_PASSWORD}
      - Jwt__SecretKey=${JWT_SECRET}
      - FileStorage__BasePath=/app/storage
      - FileStorage__PublicUrlBase=/avatars
      - RealtimeServiceUrl=http://realtime:17101
    volumes:
      - avatar-storage:/app/storage
    depends_on:
      - postgres

  web:
    image: snakk-web:latest
    environment:
      - ApiBaseUrl=http://api:17100
      - RealtimeServiceUrl=http://realtime:17101
      - FileStorage__BasePath=/app/storage
      - FileStorage__PublicUrlBase=/avatars
    ports:
      - "17200:17200"
    volumes:
      - avatar-storage:/app/storage
    depends_on:
      - api

  realtime:
    image: snakk-realtime:latest
    environment:
      - ApiBaseUrl=http://api:17100
      - AllowedOrigins=https://yourdomain.com
    ports:
      - "17101:17101"
    depends_on:
      - api

  admin:
    image: snakk-admin:latest
    environment:
      - ApiBaseUrl=http://api:17100
    ports:
      - "17201:17201"
    depends_on:
      - api

volumes:
  postgres_data:
  avatar-storage:
```

---

## Common Pitfalls & Solutions

### Pitfall 1: Clean Architecture Violation
**Problem:** Endpoint directly injects `SnakkDbContext`
**Solution:** Create service interface in Application layer, implementation in Infrastructure layer

### Pitfall 2: BFF Pattern Violation
**Problem:** JavaScript calls `/api/*` directly
**Solution:** Always call `/bff/*` endpoints, let BFF proxy to API

### Pitfall 3: EF Core Shadow Foreign Keys
**Problem:** Using `.WithMany()` without specifying collection
**Solution:** Always use `.WithMany(u => u.Collection)` explicitly

### Pitfall 4: XSS Vulnerabilities
**Problem:** Using `innerHTML` with user content
**Solution:** Use `textContent` or `window.SnakkUtils.escapeHtml()`

### Pitfall 5: Missing Role Checks
**Problem:** Admin endpoints accessible to regular users
**Solution:** Use `.RequireGlobalAdmin()` or appropriate permission check

---

## Additional Resources

- **Clean Architecture Refactoring Guide**: See plan file in `C:\Users\me\.claude\plans\steady-percolating-yeti.md`
- **BFF Pattern Guide**: See `docs/BFF_REFACTORING_GUIDE.md`
- **Hierarchical Permissions**: See `docs/HierarchicalPermissions.md`
- **Client Caching**: See `docs/client-caching-guide.md`

---

**Document Status**: ✅ Current and Accurate
**Obsoletes**: `docs/plans/admin-panel-architecture.md`, `docs/plans/nextjs-admin-implementation-guide.md`
**Maintained By**: Development Team
**Last Reviewed**: 2026-02-17
