snakk – project-structure.md

SOLUTION OVERVIEW

The solution follows Clean Architecture with strict dependency rules.
The Core layer is fully isolated.
Infrastructure is split into multiple projects by concern.
The Api project is the only entry point.

High-level dependency direction:

Api
 └─ Infrastructure.*
     └─ Core

Core MUST NOT reference:
- ASP.NET
- Infrastructure
- Databases
- WebSockets
- SignalR
- OAuth providers
- HTML / UI concerns

Infrastructure MAY reference:
- Core
- Third-party libraries

Api MAY reference:
- Core
- Infrastructure.*

---

SOLUTION STRUCTURE

Snakk.sln
- src/
  - Snakk.Core/
  - Snakk.Api/
  - Snakk.Infrastructure.Common/
  - Snakk.Infrastructure.Database/
  - Snakk.Infrastructure.Realtime/
  - Snakk.Infrastructure.Identity/   (future)
- tests/
  - Snakk.Core.Tests/
  - Snakk.Api.Tests/

---

Snakk.Core

Purpose:
- Business rules
- Domain model
- Application use cases
- Interfaces (ports)

Projects:
- Snakk.Core (single project, internally layered)

Internal structure:

Snakk.Core
- Domain/
  - Entities/
    - Hub
    - Space
    - Discussion
    - Post
    - User (minimal identity abstraction)
  - ValueObjects/
    - HubId
    - SpaceId
    - DiscussionId
    - PostId
    - UserId
  - Enums/
  - DomainEvents/

- Application/
  - UseCases/
    - CreateDiscussion
    - CreatePost
    - EditPost
    - DeletePost
    - MarkDiscussionRead
  - Interfaces/
    - Repositories/
      - IHubRepository
      - ISpaceRepository
      - IDiscussionRepository
      - IPostRepository
    - Realtime/
      - IRealtimePublisher
    - Identity/
      - ICurrentUser
  - DTOs/
  - Results/
  - Validation/

Rules:
- No EF Core
- No SQL
- No HTTP
- No WebSockets
- No cookies
- No claims
- No JWT

---

Snakk.Infrastructure.Common

Purpose:
- Shared infrastructure utilities
- Cross-cutting concerns
- Helpers used by other infrastructure projects

Contents:
- ID generation (ULID, short public IDs)
- Time abstractions
- Hashing utilities
- Slug generation
- Random ID generation (opaque, non-enumerable)

Rules:
- Can reference Core
- No ASP.NET hosting
- No controllers
- No HTTP endpoints

---

Snakk.Infrastructure.Database

Purpose:
- Persistence implementation

Contents:
- EF Core DbContext (or Dapper later)
- Entity mappings
- Repository implementations
- Migrations

Structure:

Snakk.Infrastructure.Database
- Db/
  - SnakkDbContext
  - EntityConfigurations/
- Repositories/
  - HubRepository
  - SpaceRepository
  - DiscussionRepository
  - PostRepository
- Migrations/

Rules:
- Implements Core repository interfaces
- No business logic
- No HTTP
- No WebSockets

---

Snakk.Infrastructure.Realtime

Purpose:
- WebSocket / SignalR implementation
- Scaling-friendly realtime messaging

Contents:
- SignalR hubs OR raw WebSocket handlers
- Group subscription management
- Connection tracking
- Event broadcasting

Structure:

Snakk.Infrastructure.Realtime
- Abstractions/
  - RealtimeConnection
  - SubscriptionGroup
- SignalR/
  - DiscussionHub
- Publishers/
  - RealtimePublisher (implements IRealtimePublisher)

Rules:
- Implements Core realtime interfaces
- No HTML rendering
- No domain logic

---

Snakk.Infrastructure.Identity (future)

Purpose:
- Authentication and identity providers

Contents:
- OAuth integrations
- Token validation
- Claims mapping

Rules:
- Core sees only UserId
- Identity never leaks framework types into Core

---

Snakk.Api

Purpose:
- Application entry point
- HTTP + WebSocket boundary
- HTML rendering
- HTMX handling

Responsibilities:
- Minimal API endpoints
- Request routing
- Authentication middleware
- WebSocket upgrades
- Dependency injection wiring
- SSR + partial rendering

Structure:

Snakk.Api
- Program.cs
- Composition/
  - ServiceRegistration
  - InfrastructureWiring
- Endpoints/
  - HubsEndpoints
  - SpacesEndpoints
  - DiscussionsEndpoints
  - PostsEndpoints
- Realtime/
  - WebSocketEndpoint
- Views/
  - Layouts/
  - Hubs/
  - Spaces/
  - Discussions/
  - Partials/
- Static/
  - tailwind.css
  - daisyui.css

Rules:
- Api can reference everything
- No business logic lives here
- HTMX detection happens here only

---

FRONTEND ARCHITECTURE

- Server-rendered HTML
- Tailwind CSS + daisyUI
- HTMX for:
  - Navigation
  - Partial content replacement
  - Live DOM updates
- Minimal JS only for:
  - WebSocket connection bootstrap
  - Consent handling (GDPR)

No SPA
No client-side router
No hydration layer

---

SCALABILITY NOTES

- Realtime is isolated in its own project
- Database access isolated
- Api is stateless
- WebSocket connections tracked per connection, not per user
- Multiple browser windows supported naturally

---

DOCUMENTATION FILES (RECOMMENDED)

- ARCHITECTURE.md
- project-structure.md
- realtime.md
- moderation.md
- gdpr.md
