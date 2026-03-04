# Snakk Platform - Architecture Documentation

**Last Updated**: 2026-03-04
**Status**: Current

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

Snakk is a hierarchical community discussion platform built on .NET 10 with a microservices architecture, Clean Architecture principles, and gRPC for internal service communication.

### Services

| Service | Technology | Port | Access | Purpose |
|---------|-----------|------|--------|---------|
| **Snakk.Gateway** | .NET 10 + YARP | 17000 | Public | Reverse proxy, routes traffic to services |
| **Snakk.Api** | .NET 10 + gRPC/REST | 17100 | Internal (firewalled) | Business logic API |
| **Snakk.Realtime** | .NET 9 + SignalR | 17101 | Public | WebSocket hub for real-time updates |
| **Snakk.Web** | .NET 10 + Razor Pages | 17110 | Public | Main platform + BFF layer |
| **Snakk.Auth** | .NET 10 + Razor Pages | 17111 | Public | Authentication (login, register, OAuth) |
| **Snakk.AdminWeb** | .NET 10 + Blazor Server | 17112 | Internal | Admin dashboard |
| **Snakk.Setup** | .NET 10 + Razor Pages | — | Local | First-run setup wizard |
| **Snakk.Worker** | .NET 10 | — | Internal | Background job processor |

### Architecture Diagram

```
                     ┌──────────────────────┐
                     │    Browser Client     │
                     └──────────┬───────────┘
                                │
               ┌────────────────┼─────────────────┐
               │                │                  │
               ▼                ▼                  ▼
    ┌──────────────────┐  ┌───────────┐  ┌─────────────────┐
    │  Snakk.Gateway   │  │ Snakk.    │  │  Snakk.AdminWeb │
    │  (YARP Proxy)    │  │ Realtime  │  │   (Blazor)      │
    │  Port 17000      │  │ (SignalR) │  │  Port 17112     │
    └───────┬──────────┘  │ Port 17101│  └────────┬────────┘
            │             └─────┬─────┘           │
     ┌──────┼───────┐          │                  │
     │      │       │          │                  │
     ▼      ▼       ▼          │                  │
  ┌─────┐┌─────┐┌──────┐      │                  │
  │ Web ││Auth ││Setup │      │            Snakk.Sdk
  │17110││17111││      │      │                  │
  └──┬──┘└──┬──┘└──┬───┘      │                  │
     │      │      │           │                  │
     └──────┼──────┘           │                  │
            │ gRPC             │ gRPC             │ HTTP
            ▼                  ▼                  ▼
    ┌──────────────────────────────────────────────┐
    │               Snakk.Api                      │
    │         (Internal gRPC + REST API)           │
    │              Port 17100                      │
    │            🔒 Firewalled                     │
    └───────────────────┬──────────────────────────┘
                        │
                        ▼
               ┌────────────────┐
               │   PostgreSQL   │
               └────────────────┘
```

### Orchestration

**.NET Aspire** orchestrates all services during development:

```
src/aspire/
├── Snakk.AppHost/           # Service orchestrator (ports, config, dependencies)
└── Snakk.ServiceDefaults/   # Shared defaults (OpenTelemetry, health checks, resilience)
```

Run `dotnet run` in `Snakk.AppHost` to start everything with the Aspire dashboard for observability.

---

## Technology Stack

### Backend (.NET)
- **.NET 10** — Latest framework (Realtime uses .NET 9)
- **ASP.NET Core** — Minimal APIs, Razor Pages, Blazor Server
- **gRPC** — Internal service-to-service communication (Snakk.Protos)
- **Entity Framework Core 10** — ORM with PostgreSQL (Npgsql)
- **YARP** — Reverse proxy gateway
- **SignalR** — Real-time WebSocket communication
- **Serilog** — Structured logging
- **HybridCache** — In-memory + distributed caching
- **BCrypt.NET** — Password hashing
- **FluentValidation** — Request validation
- **ImageSharp** — Avatar generation

### Frontend (Snakk.Web)
- **Razor Pages** — Server-side rendering (SSR)
- **HTMX** — HTML-first interactive enhancements (SPA-like navigation)
- **Tailwind CSS v4** — Utility-first CSS framework
- **daisyUI v5** — Tailwind component library
- **SCSS** — Custom styles compiled with Dart Sass
- **TypeScript** — Client-side logic compiled with tsc + esbuild minification
- **Vanilla JavaScript ES6+** — IIFE modules, event delegation
- **SignalR Client** — WebSocket connection for real-time updates

### Frontend (Snakk.Auth)
- **Razor Pages** — Server-side rendering
- **SCSS** — Custom styles compiled with Dart Sass (no Tailwind, no daisyUI)

### Admin Panel (Snakk.AdminWeb)
- **Blazor Server** — .NET UI framework
- **Microsoft Fluent UI** — Component library
- **Cookie Authentication** — Admin session management
- **Snakk.Sdk** — Auto-generated API client (NSwag from OpenAPI spec)

### Database & Storage
- **PostgreSQL** — Primary database with trigram indexes for full-text search
- **Local File Storage** — Avatar storage with XxHash32 sharding (256 folders)

### Authentication & Security
- **JWT Bearer Tokens** — API authentication
- **Cookie Authentication** — Web and admin panel sessions
- **OAuth 2.0** — Google, GitHub, Discord
- **Rate Limiting** — Per-endpoint limits

---

## Architectural Patterns

### 1. Clean Architecture (C# Backend)

**Rules:**
- API layer: HTTP/gRPC concerns ONLY (no business logic)
- Application layer: DTOs, service interfaces, use cases
- Infrastructure layer: Service implementations
- Domain layer: Business logic (NO external dependencies)
- **FORBIDDEN**: API layer referencing Infrastructure types

**Layer Dependencies:**
```
┌─────────────────────────────┐
│ Snakk.Api (Endpoints/gRPC)  │  ← HTTP/gRPC handlers only
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

### 2. Backend-for-Frontend (BFF) Pattern

**Rules:**
- JavaScript calls `/bff/*` endpoints ONLY
- BFF (Snakk.Web) proxies requests to internal API via gRPC
- SignalR connects directly to Snakk.Realtime (not proxied)
- **FORBIDDEN**: Direct `fetch()` to `/api/*` from browser
- **FORBIDDEN**: Exposing API URLs to frontend

**Request Flow:**
```
Browser
  ├── HTTP GET /bff/posts?page=1
  │     ↓
  │   Snakk.Web (BFF)
  │     ↓ gRPC GetDiscussions()
  │   Snakk.Api
  │     ↓
  │   Response (protobuf)
  │     ↓
  │   Transform to JSON
  │     ↓
  │   Return to browser
  │
  └── WebSocket connect → Snakk.Realtime:17101
                            (Direct connection, no proxy)
```

### 3. gRPC Internal Communication

All BFF-to-API communication uses gRPC with protobuf contracts defined in `Snakk.Protos`:

```
src/core/Snakk.Protos/Protos/
├── auth.proto           # Authentication (login, register, current user)
├── community.proto      # Communities
├── discussion.proto     # Discussions
├── follow.proto         # Follow relationships
├── hub.proto            # Hubs
├── moderation.proto     # Moderation (reports, bans, roles)
├── notification.proto   # Notifications
├── post.proto           # Posts
├── reaction.proto       # Reactions
├── read_state.proto     # Read state tracking
├── search.proto         # Search
├── space.proto          # Spaces
├── trending.proto       # Trending content
└── user.proto           # User profiles
```

### 4. Event-Driven Architecture

**Domain Events:**
- Events raised by domain entities: `PostCreatedEvent`, `DiscussionCreatedEvent`, `ReactionAddedEvent`
- Handled by Infrastructure layer event handlers
- Triggers: Database updates, cache invalidation, real-time broadcasts

**Event Flow:**
```
1. User submits post in browser
2. BFF endpoint calls API via gRPC
3. API UseCase creates Post domain entity
4. Post entity raises PostCreatedEvent
5. Domain event dispatcher invokes handlers
6. Handler calls IRealtimeNotifier.NotifyAsync()
7. Notifier POSTs to Snakk.Realtime broadcast endpoint
8. Realtime hub broadcasts to connected clients
9. Browser receives update via SignalR
10. JavaScript updates DOM
```

### 5. Use Case Pattern

Each business operation is encapsulated in a use case class:

```csharp
public class PostUseCase
{
    private readonly IPostRepository _postRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public async Task<Result<Post>> CreatePostAsync(CreatePostCommand command)
    {
        // 1. Validate
        // 2. Create domain entity
        var post = Post.Create(...);

        // 3. Save to repository
        await _postRepository.AddAsync(post);

        // 4. Dispatch domain events
        await _eventDispatcher.DispatchAsync(post.DomainEvents);

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
│   └── Repositories/                # Repository interfaces
│
├── Snakk.Application/               # Application layer (DTOs, use cases)
│   ├── DTOs/                        # Data transfer objects
│   ├── UseCases/                    # Business orchestration
│   ├── Services/                    # Service interfaces
│   └── Validators/                  # FluentValidation validators
│
├── Snakk.Infrastructure/            # Service implementations
│   ├── Database/                    # Database repositories
│   ├── EventHandlers/               # Domain event handlers
│   ├── Events/                      # Event dispatcher
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
├── Snakk.Protos/                    # Protobuf definitions
│   └── Protos/                      # .proto files for gRPC contracts
│
├── Snakk.Shared/                    # Shared utilities
│   ├── Enums/                       # UserRoleTypeEnum, NotificationType
│   └── Extensions/                  # String extensions, etc.
│
└── Snakk.Sdk/                       # Auto-generated client SDK
    ├── openapi.json                 # OpenAPI spec from Snakk.Api
    ├── nswag.json                   # NSwag generation config
    └── Generated/SnakkApiClient.cs  # Generated HTTP client
```

### Services

```
src/services/
├── Snakk.Api/                       # Internal gRPC + REST API
│   ├── GrpcServices/                # gRPC service implementations
│   ├── Endpoints/                   # REST endpoint groups
│   ├── Services/                    # CurrentUserService, JwtTokenService
│   ├── Authorization/               # Authorization handlers
│   └── Program.cs
│
├── Snakk.Gateway/                   # YARP reverse proxy
│   └── Program.cs                   # Route configuration
│
├── Snakk.Realtime/                  # SignalR hub
│   ├── Hubs/                        # SignalR hubs
│   ├── Controllers/                 # Broadcast API controller
│   └── Program.cs
│
└── Snakk.Worker/                    # Background jobs
    └── Program.cs
```

### Applications

```
src/apps/
├── Snakk.Web/                       # Main platform (Razor Pages + BFF)
│   ├── Pages/                       # Razor Pages
│   ├── Endpoints/                   # BFF API endpoints (/bff/*)
│   ├── Services/                    # SnakkApiClient (gRPC), CurrentUserService
│   ├── TagHelpers/                  # Custom tag helpers
│   ├── Scripts/                     # TypeScript source files
│   │   ├── core/                    # auth.ts, theme.ts, utils.ts
│   │   ├── components/              # auth-navbar.ts, site.ts
│   │   ├── services/                # realtime.ts, cache-manager.ts
│   │   └── pages/                   # discussion-detail.ts, profile.ts
│   ├── Styles/                      # SCSS source files
│   │   ├── base/                    # variables, typography, layout
│   │   ├── components/              # navbar, forms, cards, buttons
│   │   ├── features/                # posts, discussions, sidebar
│   │   ├── utilities/               # scrollbar, transitions, mobile
│   │   ├── themes/                  # dark mode
│   │   ├── site.scss                # Main SCSS entry point
│   │   └── input.css                # Tailwind CSS entry point
│   └── wwwroot/
│       ├── js/dist/                 # Compiled TypeScript output
│       ├── js/vendor/               # htmx.min.js, signalr
│       ├── css/dist/                # Compiled SCSS output
│       └── css/vendor/              # Compiled Tailwind CSS
│
├── Snakk.Auth/                      # Authentication service
│   ├── Pages/                       # Login, Register
│   ├── Endpoints/                   # OAuth callback handlers
│   ├── Styles/                      # SCSS source (auth.scss)
│   └── wwwroot/css/                 # Compiled auth.css
│
├── Snakk.AdminWeb/                  # Admin panel (Blazor Server)
│   ├── Pages/                       # Razor Pages (Dashboard, Auth)
│   ├── Components/                  # Blazor components (Fluent UI)
│   ├── Services/                    # AdminApiClientService
│   └── wwwroot/
│
└── Snakk.Setup/                     # First-run setup wizard
    ├── Pages/                       # Setup steps (DB, SiteConfig, Storage, etc.)
    ├── Services/                    # SetupService, SetupState
    ├── Styles/                      # Tailwind CSS entry point
    └── wwwroot/css/                 # Compiled setup.css
```

### Tests

```
src/tests/
├── Snakk.Domain.Tests/              # Domain entity and value object tests
├── Snakk.Application.Tests/         # Use case and DTO tests
├── Snakk.Shared.Tests/              # Shared utility tests
├── Snakk.Infrastructure.Tests/      # Repository and service tests
├── Snakk.Api.Tests/                 # API endpoint integration tests
├── Snakk.Realtime.Tests/            # SignalR hub tests
└── Snakk.Web.Tests/                 # Web layer tests
```

### Tools

```
src/tools/
├── Snakk.DbSeeder/                  # Database seeding tool
└── Snakk.VBulletinImporter/         # vBulletin migration tool
```

---

## Code Conventions

### C# Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| **Domain Entity** | PascalCase (no suffix) | `User`, `Post`, `Discussion` |
| **Database Entity** | PascalCase + "DatabaseEntity" | `UserDatabaseEntity`, `PostDatabaseEntity` |
| **DTO** | Descriptive PascalCase | `CreatePostDto`, `LoginRequest` |
| **Service Interface** | I + PascalCase + "Service" | `IUserService`, `IPostService` |
| **Service Implementation** | PascalCase + "Service" | `UserService`, `PostService` |
| **Use Case** | PascalCase + "UseCase" | `UserUseCase`, `PostUseCase` |
| **Value Object** | PascalCase (entity + "Id") | `UserId`, `PostId`, `DiscussionId` |
| **Endpoint Class** | PascalCase + "Endpoints" | `AuthEndpoints`, `PostEndpoints` |
| **gRPC Service** | PascalCase + "GrpcService" | `AuthGrpcService`, `PostGrpcService` |

### File Naming

- **C# files**: PascalCase (`SnakkUrlHelper.cs`)
- **CSS, SCSS, JS, TS, images**: kebab-case (`auth-navbar.ts`, `cache-manager.js`)

### Entity Property Naming

- `FollowDatabaseEntity`: Use `FollowedUser`, `Space`, `Discussion` (NOT TargetUser)
- `ReactionDatabaseEntity`: Use `Type`, only has `Post` property (NOT ReactionType)
- `UserDatabaseEntity`: Use `DisplayName` (NOT Username)

### EF Core Rules

- DbContext defaults: `NoTrackingWithIdentityResolution` + `SplitQuery`
- Write paths must use `.AsTracking()`
- Always use `.Select()` projections — never load full entities when a subset suffices
- Always specify `.WithMany(u => u.Collection)` explicitly in relationships
- Never use `.ToLower()` in LINQ — use `EF.Functions.ILike()` for case-insensitive search
- Use denormalized count columns (`d.PostCount`) instead of `.Count()` on navigation properties
- Default page size: 20, clamped with `Math.Clamp(pageSize, 1, maxAllowed)`

### JavaScript Conventions

**Module Pattern (IIFE):**
```javascript
(function() {
    'use strict';

    function setToken(token) { /* ... */ }
    function getToken() { /* ... */ }

    window.snakkAuth = { setToken, getToken };
})();
```

**Security Rules:**
- Use `window.SnakkUtils.escapeHtml()` for user content in HTML
- Use `window.SnakkUtils.sanitizeHtml()` for server-rendered HTML (markdown, etc.)
- Use `window.SnakkUtils.sanitizeUrl()` for URLs from API data
- Use `textContent` instead of `innerHTML` for plain text
- NO `innerHTML` with unsanitized user-generated content
- NO `eval()` or `new Function()`

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

---

## Security

### Authentication Flow

**JWT Bearer Tokens (API):**
```
1. User visits /auth/login (Snakk.Auth)
2. Auth service validates credentials via gRPC to Snakk.Api
3. API generates JWT + refresh token
4. Auth service sets HTTP-only cookies
5. Snakk.Web reads cookies for BFF requests
6. BFF forwards JWT in gRPC metadata to API
```

**Cookie Authentication (Admin Panel):**
```
1. Admin logs in via Snakk.AdminWeb
2. Blazor validates credentials via Snakk.Sdk HTTP client
3. Sets authentication cookie
4. Cookie automatically sent with each request
```

### Authorization

**Hierarchical Role-Based Access Control:**
```
GlobalAdmin  → Full access to everything
CommunityAdmin → Community + all child hubs/spaces/discussions
HubMod       → Hub + all child spaces/discussions
SpaceMod     → Space + child discussions only
```

Permissions "bubble down" but NOT up. A SpaceMod cannot access parent hub settings.

**Usage:**
```csharp
.RequireGlobalAdmin()                          // Global admin only
.RequireCommunityAdmin("communityId")          // Community admin or higher
.RequireHubModerator("hubId")                  // Hub mod or higher
.RequireSpaceModerator("spaceId")              // Space mod or higher
```

Implementation uses `PermissionService.UserHasPermissionAsync` with automatic parent resolution and a 5-minute per-user permission cache.

### Security Best Practices

- **No internal IDs in APIs**: Use `PublicId` (GUID) for all external-facing contracts, never database integer IDs
- **Rate Limiting**: Per-endpoint limits on auth, posts, and API calls (static assets exempted)
- **HTTPS Only**: Enforced in production
- **XSS Prevention**: `escapeHtml()`, `sanitizeHtml()`, `sanitizeUrl()` utilities
- **BFF Firewall**: Internal API not accessible from browser
- **No CDN dependencies**: All CSS and JS served locally

---

## Static File Serving & Storage

### wwwroot Serving

Snakk.Web serves static files from wwwroot:
- `/css/dist/*` — Compiled SCSS output
- `/css/vendor/*` — Compiled Tailwind CSS + daisyUI
- `/js/dist/*` — Compiled TypeScript output
- `/js/vendor/*` — HTMX, SignalR client

**Cache Policy:** 1 year in production (`public,max-age=31536000`), no caching in development.

### Avatar Storage

User-generated avatars use XxHash32 sharding across 256 folders:

- Generated avatars: `/avatars/generated/{type}/{shard}/{id}.svg` (cached 1 year, immutable)
- Uploaded avatars: `/avatars/uploaded/{type}/{shard}/{id}-r{revision}.svg` (cached 1 hour)

Storage path is configurable via `FileStorage:BasePath` in appsettings.json.

---

## Deployment

### Docker (Recommended)

One-command install on Linux:
```bash
curl -fsSL https://raw.githubusercontent.com/snakk-community-platform/snakk-installer/main/docker/install.sh | sudo bash
```

The installer handles Docker, PostgreSQL, Caddy (HTTPS), RAM-based memory tuning, and launches the browser-based setup wizard. See the [README](../README.md#installation) for details.

### Development

Use .NET Aspire for local development:
```bash
cd src/aspire/Snakk.AppHost
dotnet run
```

### SDK Regeneration

When API endpoints change, regenerate the SDK:
```bash
# 1. Start Snakk.Api
dotnet run --project src/services/Snakk.Api

# 2. Fetch updated OpenAPI spec
curl -k https://localhost:17100/openapi/v1.json -o src/core/Snakk.Sdk/openapi.json

# 3. Rebuild SDK
dotnet build src/core/Snakk.Sdk
```

Both `openapi.json` and `Generated/SnakkApiClient.cs` are committed for reproducible builds without needing the API running.

---

## Additional Resources

- **Moderation System**: [docs/MODERATION.MD](MODERATION.MD)
- **Real-Time Features**: [docs/REALTIME.MD](REALTIME.MD)
- **Hierarchical Permissions**: [docs/HierarchicalPermissions.md](HierarchicalPermissions.md)
- **Client Caching**: [docs/client-caching-guide.md](client-caching-guide.md)
- **GDPR Compliance**: [docs/GDRP.MD](GDRP.MD)
