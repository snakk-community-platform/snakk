# Contributing to Snakk

Thanks for your interest in contributing. Snakk is a .NET 10 community platform built on ASP.NET Core, HTMX, Tailwind CSS, and Blazor. This guide covers how to get the project running locally and what to keep in mind when opening a pull request.

## Getting Started

### Prerequisites

- **.NET 10 SDK** (`dotnet --version` should report 10.x)
- **Node.js 22+** (for the frontend build in Snakk.Web, Snakk.Setup, and Snakk.Auth)
- **PostgreSQL 17** (runs inside the Aspire AppHost automatically via container)
- **Docker Desktop** (required by the Aspire AppHost for the Postgres container and observability stack)

### Clone and Restore

```bash
git clone https://github.com/snakk-community-platform/snakk.git
cd snakk/src
dotnet restore Snakk.slnx
```

### Frontend Build

The ASP.NET projects that have TypeScript/Sass need an `npm install` + build before the first run:

```bash
cd src/apps/Snakk.Web && npm install && npm run build
cd ../Snakk.Setup && npm install && npm run build:css
cd ../Snakk.Auth && npm install && npm run build:css
```

After that, the frontend is watched automatically while the Aspire AppHost is running — just edit `.ts` or `.scss` files in `Styles/` and `Scripts/` and the compiled output under `wwwroot/` will update.

**Do not edit compiled output directly.** The files in `wwwroot/css/dist/`, `wwwroot/js/dist/`, and `wwwroot/css/vendor/` are generated. Edit the sources in `Styles/` and `Scripts/` instead.

### Running the Platform

```bash
cd src/aspire/Snakk.AppHost
dotnet run
```

The Aspire dashboard opens in your browser and shows every service, its port, and live logs. First-run setup (create admin user, seed default community) happens at `https://localhost:17000/setup`.

### Dev Ports

| Service | Port | URL |
|---|---|---|
| Snakk.Gateway (YARP, public entry) | 17000 | https://localhost:17000 |
| Snakk.Api (internal, firewalled in prod) | 17100 | https://localhost:17100 |
| Snakk.Realtime (SignalR) | 17101 | https://localhost:17101 |
| Snakk.Web (Razor Pages + HTMX) | 17110 | https://localhost:17110 |
| Snakk.Auth (OAuth + login UI) | 17111 | https://localhost:17111 |
| Snakk.Admin (Blazor Server) | 17112 | https://localhost:17112 |

Everything except the gateway is reached through the gateway in normal dev flow. Hit `https://localhost:17000` and traffic is routed to the right service.

### Running Tests

Snakk uses TUnit (not xUnit). Tests are executable projects — run them via `dotnet run` from the test project directory:

```bash
cd src/tests/Snakk.Application.Tests
dotnet run
```

Same pattern for the other test projects: `Snakk.Domain.Tests`, `Snakk.Infrastructure.Tests`, `Snakk.Api.Tests`, `Snakk.Web.Tests`, `Snakk.Shared.Tests`, `Snakk.Realtime.Tests`.

You can also use `dotnet test` from the solution root — it will discover all test projects.

## Architecture

Snakk follows Clean Architecture. Before opening a PR, please skim the [architecture docs](docs/ARCHITECTURE.md) and the project-level guide in [CLAUDE.md](CLAUDE.md) — the latter documents conventions that apply to humans as well as AI assistants.

### Layer Boundaries

```
Domain          →  Entities, value objects, domain events (no external dependencies)
Application     →  DTOs, service interfaces, use cases (no infrastructure deps)
Infrastructure  →  Service implementations, EF Core, external integrations
Api             →  gRPC + minimal REST endpoints (HTTP concerns only, no business logic)
Web / Admin     →  Presentation (Razor Pages + HTMX / Blazor Server)
```

Business logic belongs in the Application or Domain layers. Endpoints should only translate HTTP into a use case call and back. Please don't reference Infrastructure types from Api endpoints.

### Backend-for-Frontend (BFF) Pattern

JavaScript in Snakk.Web must only call `/bff/*` endpoints — never `/api/*` directly. The internal API is firewalled in production and is only reachable from the ASP.NET projects. If you need a new piece of data on the frontend, add a BFF endpoint in `src/apps/Snakk.Web/Endpoints/BffApiEndpoints.cs` that proxies and validates the internal call.

**Direct `fetch()` to `/api/*` from browser code is not accepted in PRs.**

### Public IDs, Not Integer IDs

External-facing DTOs and gRPC messages should expose `PublicId` (GUID/ULID) for entity identification, never internal database integer `Id` values. This prevents enumeration attacks and keeps the public contract decoupled from the schema.

### Security

- **Never use `innerHTML` with user-generated content.** Use `textContent`, or `window.SnakkUtils.escapeHtml()` / `sanitizeHtml()` for rendered HTML coming from BFF endpoints or SignalR.
- **Never commit secrets.** `.env`, credentials, API keys, OAuth client secrets, etc. stay out of the repo.
- Content Security Policy is enforced — inline scripts and event handlers will be blocked.

### File Naming

All CSS, SCSS, JS, TS, and image assets use kebab-case (`auth-navbar.js`, `profile-avatar.png`). C# files use PascalCase. Razor `@section` scripts on HTMX-swapped pages should go inline in the page body (not in `@section Scripts`), otherwise they won't re-execute on navigation.

## Development Workflow

1. **Fork and branch.** Create a feature branch from `main` (e.g. `feat/my-feature` or `fix/my-bug`).
2. **Small, focused changes.** Prefer small, reviewable PRs over large mixed ones.
3. **Write tests.** New behavior should come with a test. Bug fixes should come with a regression test.
4. **Run the tests.** All existing tests must continue to pass.
5. **Keep the build clean.** Zero warnings is the baseline.
6. **Follow existing style.** C# conventions are documented in the memory files referenced by [CLAUDE.md](CLAUDE.md). In short: expression bodies where sensible, LINQ chains on separate lines, no redundant comments.
7. **Document non-obvious changes.** If you touched architecture, a config surface, or a behavior that's surprising, update the relevant section of the README or `docs/`.
8. **Open a pull request.** Fill in the PR template, including a test plan and screenshots for UI changes.

## Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/) prefixes: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`, `perf:`. Keep the subject line under 72 characters. Explain the "why" in the body when it's not obvious from the diff.

## Reporting Bugs and Requesting Features

Please use the GitHub issue templates:

- **Bug report** for something that's broken or behaving unexpectedly
- **Feature request** for new capabilities

Include reproduction steps for bugs, and a clear use case for features.

## Questions

For questions that aren't bug reports or feature requests, open a GitHub Discussion or reach out to the maintainer at [me@paaltuv.no](mailto:me@paaltuv.no).

## License

By contributing, you agree that your contributions will be licensed under the MIT License (see [LICENSE](LICENSE)).
