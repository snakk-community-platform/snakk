<p align="center" width="100%">
    <img src="https://github.com/snakk-community-platform/snakk/blob/main/media/github-logo.webp?raw=true"> 
</p>

A modern, performant community platform built with .NET 10 and ASP.NET Core. Snakk enables communities to create organized discussions through a hierarchical structure of communities, hubs, and spaces, with built-in moderation, real-time features, and multi-community support.

> **Pre-Release Software**
> Snakk is in active development and should be considered pre-release/alpha software. Core functionality works, but some features may be incomplete or subject to breaking changes.
> Preview: https://preview.snakk.community/

## Table of Contents

- [Features](#features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Installation](#installation)
  - [Docker (Recommended)](#docker-recommended)
  - [Development Setup](#development-setup)
- [First-Run Setup](#first-run-setup)
- [Frontend Builds](#frontend-builds)
- [OAuth Configuration](#oauth-configuration)
- [Development](#development)
- [Service Ports](#service-ports-development)
- [Contributing](#contributing)
- [License](#license)
- [Roadmap](#roadmap)
- [AI Assistance](#ai-assistance)
- [Author](#author)

## Features

### Core
- **Hierarchical Organization**: Communities > Hubs > Spaces > Discussions > Posts
- **Multi-Community Support**: Host multiple communities with custom domains on a single instance
- **Discussion Types**: Standard, Question (with accepted answers), Poll, Link (with oEmbed previews), Images/Gallery (6 layout options), Debate (position-based), Guide (auto-generated TOC), Journal (chronological updates), and IAMA (ask-me-anything)
- **Rich Text Editor**: Milkdown-based markdown editor with toolbar, image uploads, tables, and live preview
- **Reactions**: Post-level reactions with emoji support
- **OAuth Authentication**: Google, GitHub, and Discord login via dedicated auth service
- **Moderation Tools**: Hierarchical role-based permissions with audit logs, reports, and bans
- **Full-Text Search**: PostgreSQL trigram-based search across discussions and posts
- **Trending Content**: Algorithmic trending discussions, spaces, and contributors

### Content & Media
- **Image Galleries**: Upload multiple images with drag-to-reorder and 6 display layouts (masonry, grid, justified, carousel, hero, compare)
- **oEmbed Previews**: Automatic rich previews for 14 providers (YouTube, Vimeo, TikTok, Twitter/X, Bluesky, Reddit, Spotify, SoundCloud, Bandcamp, Twitch, Imgur, CodePen, Canva) with per-user embed privacy controls
- **Link Discussions**: Preview cards with Open Graph metadata, local image caching, and blur-up placeholders
- **Syntax Highlighting**: Lazy-loaded Prism.js with line numbers, language labels, and copy-to-clipboard for code blocks
- **Avatar Generation**: Deterministic SVG avatars (Marble, Beam, Pixel, Bauhaus styles) with custom upload support

### Performance
- **HybridCache**: In-memory + distributed caching with granular invalidation
- **Output Caching**: Page-level caching for public content
- **Database Optimization**: PostgreSQL with trigram indexes, projection queries, and denormalized counters
- **HTMX Navigation**: SPA-like page transitions with adaptive loading indicators
- **Image Optimization**: Server-side WebP conversion, thumbnail generation, and blur-up data URIs

### Real-Time
- **SignalR Hub**: Live activity feed, notifications, and presence via dedicated microservice
- **Read State Tracking**: Batched read state updates with unread indicators

### User Experience
- **Dark Mode**: System-preference aware with manual toggle
- **Infinite Scroll**: HTMX-powered endless scroll with pagination fallback
- **Smart Navigation**: Resume-reading, unread tracking, and draft persistence
- **Embed Privacy Controls**: Per-provider settings for external embedded content (stored client-side)
- **Spoiler Images**: NSFW/spoiler overlays with click-to-reveal and session persistence

## Technology Stack

### Backend (.NET 10)
- **ASP.NET Core** - Razor Pages (Web), Minimal APIs (API), Blazor Server (Admin)
- **gRPC** - Internal service-to-service communication
- **Entity Framework Core 10** - PostgreSQL with Npgsql
- **SignalR** - Real-time WebSocket communication (.NET 9)
- **YARP** - Reverse proxy gateway
- **Serilog** - Structured logging
- **.NET Aspire** - Service orchestration and observability
- **BCrypt** - Password hashing

### Frontend
- **Razor Pages** - Server-side rendering with HTMX for interactivity
- **Tailwind CSS v4** + **daisyUI v5** - Utility-first CSS with component library
- **SCSS** - Custom styles compiled with Dart Sass
- **TypeScript** - Client-side logic compiled with esbuild
- **Milkdown** - ProseMirror-based markdown editor
- **Prism.js** - Syntax highlighting (lazy-loaded)

### Admin Panel
- **Blazor Server** - Interactive server-side UI
- **MudBlazor** - Material Design component library
- **BlazorApexCharts** - Dashboard charts and analytics

### Architecture
- **Clean Architecture** - Domain / Application / Infrastructure layer separation
- **BFF Pattern** - Backend-for-Frontend; JavaScript only calls `/bff/*` endpoints
- **CQRS** - Command/Query separation with use case orchestrators
- **Domain-Driven Design** - Value objects, domain events, aggregate roots

## Project Structure

```
src/
├── aspire/
│   ├── Snakk.AppHost/              # .NET Aspire orchestrator
│   └── Snakk.ServiceDefaults/      # Shared service configuration
│
├── core/
│   ├── Snakk.Domain/               # Domain entities, events, value objects
│   ├── Snakk.Application/          # DTOs, service interfaces, use cases
│   ├── Snakk.Infrastructure/       # Service implementations
│   ├── Snakk.Infrastructure.Database/  # EF Core DbContext, migrations
│   ├── Snakk.Protos/               # Protobuf definitions for gRPC
│   └── Snakk.Shared/               # Enums, utilities
│
├── services/
│   ├── Snakk.Api/                  # Internal gRPC + REST API (port 17100)
│   ├── Snakk.Gateway/              # YARP reverse proxy (port 17000)
│   ├── Snakk.Realtime/             # SignalR hub (port 17101)
│   └── Snakk.Worker/               # Background job processor
│
├── apps/
│   ├── Snakk.Web/                  # Main platform — Razor Pages + HTMX (port 17110)
│   ├── Snakk.Auth/                 # Authentication service (port 17111)
│   ├── Snakk.Admin/                # Admin panel — Blazor Server + MudBlazor (port 17112)
│   └── Snakk.Setup/                # First-run setup wizard
│
├── tests/
│   ├── Snakk.Domain.Tests/
│   ├── Snakk.Application.Tests/
│   ├── Snakk.Shared.Tests/
│   ├── Snakk.Infrastructure.Tests/
│   ├── Snakk.Api.Tests/
│   ├── Snakk.Realtime.Tests/
│   └── Snakk.Web.Tests/
│
└── tools/
    ├── Snakk.DbSeeder/             # Database seeding tool
    └── Snakk.VBulletinImporter/    # vBulletin migration tool

docs/
├── ARCHITECTURE.md                 # Architecture documentation
├── REALTIME.MD                     # Real-time features
├── PERFORMANCE-AUDIT.md            # Performance audit notes
├── PROJECT-STRUCTURE.MD            # Detailed project structure
├── SECURITY-TODO.md                # Security checklist
└── GDRP.MD                         # GDPR compliance
```

## Installation

### Docker (Recommended)

The fastest way to deploy Snakk on a Linux server. The installer handles Docker, PostgreSQL, Caddy (HTTPS), memory tuning, and launches the browser-based setup wizard.

```bash
curl -fsSL https://get.snakk.community/install-docker.sh | sudo bash
```

**What it does:**
1. Installs prerequisites (Git, Docker, optionally Caddy for HTTPS)
2. Clones the repository to `/opt/snakk`
3. Detects system RAM and tunes PostgreSQL accordingly
4. Builds and starts containers
5. Prints the URL to complete setup in your browser

**Supported distros:** Ubuntu, Debian, Rocky Linux, AlmaLinux, RHEL

**After installation:**
```bash
cd /opt/snakk/docker
docker compose logs -f snakk     # View logs
docker compose restart            # Restart services
docker compose down               # Stop everything
docker compose up -d --build      # Rebuild after updates
```

### Development Setup

#### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/) (for frontend builds)
- [PostgreSQL 14+](https://www.postgresql.org/download/)

#### Running with .NET Aspire (Recommended)

The easiest way to run Snakk locally is with the Aspire AppHost, which orchestrates all services:

```bash
cd src/aspire/Snakk.AppHost
dotnet run
```

This starts all services with the correct ports and configuration. The Aspire dashboard provides observability into all running services.

#### Running Individual Services

If you prefer to run services manually:

```bash
# API (internal, port 17100)
dotnet run --project src/services/Snakk.Api

# Web platform (port 17110)
dotnet run --project src/apps/Snakk.Web

# Auth service (port 17111)
dotnet run --project src/apps/Snakk.Auth

# Realtime hub (port 17101)
dotnet run --project src/services/Snakk.Realtime
```

### First-Run Setup

On first launch, the setup wizard guides you through:
1. Database connection
2. Site configuration (domain, name)
3. Storage path
4. Admin account creation
5. Security keys (auto-generated)
6. OAuth provider configuration (optional)

### Frontend Builds

Frontend assets (CSS, JS) are pre-compiled and committed. To rebuild after changes:

```bash
# Snakk.Web
cd src/apps/Snakk.Web
npm install
npm run build          # Build all (TypeScript + CSS)

# Snakk.Auth
cd src/apps/Snakk.Auth
npm install
npm run build:css      # Build CSS
```

### OAuth Configuration

To enable social login, register OAuth applications:

- **Google**: [Google Cloud Console](https://console.cloud.google.com/)
- **GitHub**: [GitHub Developer Settings](https://github.com/settings/developers)
- **Discord**: [Discord Developer Portal](https://discord.com/developers/applications)

Configure via the setup wizard or in `conf/snakk-config.json` (under the storage path).

## Development

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

There are 2,500+ tests across 7 test projects covering domain logic, application use cases, infrastructure, API endpoints, realtime hub, and web layer.

### Database Migrations
```bash
dotnet ef migrations add MigrationName \
  --project src/core/Snakk.Infrastructure.Database/Snakk.Infrastructure.Database.csproj \
  --startup-project src/services/Snakk.Api/Snakk.Api.csproj

dotnet ef database update \
  --project src/core/Snakk.Infrastructure.Database/Snakk.Infrastructure.Database.csproj \
  --startup-project src/services/Snakk.Api/Snakk.Api.csproj
```

### Code Structure Guidelines

- **Domain Layer**: Pure business logic, no external dependencies
- **Application Layer**: Use cases, orchestration, DTOs, service interfaces
- **Infrastructure Layer**: Service implementations, database repositories
- **Web Layer**: Razor Pages, BFF endpoints, minimal logic

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed architecture documentation.

## Service Ports (Development)

| Service | Port | Purpose |
|---------|------|---------|
| Snakk.Gateway | 17000 | YARP reverse proxy |
| Snakk.Api | 17100 | Internal gRPC API |
| Snakk.Realtime | 17101 | SignalR WebSocket hub |
| Snakk.Web | 17110 | Main platform |
| Snakk.Auth | 17111 | Authentication service |
| Snakk.Admin | 17112 | Admin panel |

## Contributing

Contributions are welcome! Please read the contributing guidelines before submitting pull requests.

### Development Workflow
1. Create a feature branch from `main`
2. Make your changes with clear commit messages
3. Write tests for new functionality
4. Submit a pull request with description of changes

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Roadmap

- [ ] Password recovery flow
- [ ] Email notifications
- [ ] Mobile apps (iOS/Android)
- [ ] Plugin system
- [ ] Localization/i18n

## AI Assistance

This project was developed using LLM-based tooling as an implementation aid.
All architectural and design decisions originated from the author.

---

## Author

**Pål Rune Sørensen Tuv**
Senior Software Engineer / Systems Architect

- GitHub: [https://github.com/paaltuv](https://github.com/paaltuv)
- LinkedIn: [https://www.linkedin.com/in/pal-rune-sorensen-tuv-702412392/](https://www.linkedin.com/in/p%C3%A5l-rune-s%C3%B8rensen-tuv-702412392/)
