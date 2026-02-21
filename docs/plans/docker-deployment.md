# Docker Deployment Plan — Two Modes

## Context

Snakk needs Docker deployment in two flavors:
- **All-in-One**: Single Snakk container + PostgreSQL container (2 total) — for novice installers
- **Advanced**: One container per service + PostgreSQL (8 total) — for power users

Currently only .NET Aspire handles orchestration (dev only). No Docker files exist.

---

## Files to Create (8 new files, 0 modifications)

| # | File | Purpose |
|---|------|---------|
| 1 | `.dockerignore` (repo root) | Exclude bin/obj/node_modules from build context |
| 2 | `docker/.env.example` | Template with all configurable values |
| 3 | `docker/Dockerfile` | Multi-stage build (~180 lines) |
| 4 | `docker/supervisord.conf` | Process manager for all-in-one |
| 5 | `docker/entrypoint.sh` | Startup script for all-in-one |
| 6 | `docker/docker-compose.yml` | All-in-one mode (2 containers) |
| 7 | `docker/docker-compose.advanced.yml` | Advanced mode (8 containers) |

---

## Architecture

### All-in-One
```
Host:17000 → [snakk container]              [postgres container]
              Gateway    :17000 (0.0.0.0)       :5432
              Api        :17100 (localhost) ←────┘
              Realtime   :17101 (localhost)
              Web        :17110 (localhost)
              Auth       :17111 (localhost)
              AdminWeb   :17112 (localhost)
              Worker     (no port)
              supervisord manages all 7
              /app/storage (volume)
```
API naturally firewalled — binds `localhost` only inside the container.

### Advanced
```
Host:17000 → gateway:8080 ─┬─ web:8080
                            ├─ auth:8080
                            ├─ adminweb:8080
                            └─ realtime:8080
             api:8080 (internal only, no host port)
             worker (no port)
             postgres:5432
             [avatar-storage] shared by api, worker, web
```

---

## Dockerfile Strategy

Single file, multi-stage. Build all services together (shared NuGet restore), then split into per-service runtime images.

### Stages

```
base-sdk      mcr.microsoft.com/dotnet/sdk:10.0 + Node.js 22
              └── NuGet restore (all projects)
              └── npm ci --ignore-scripts (Snakk.Web)
              └── Manually copy htmx.min.js (skip PowerShell postinstall)

build         FROM base-sdk
              └── Copy all source
              └── dotnet publish 7 services + DbSeeder → /publish/*

runtime-base  mcr.microsoft.com/dotnet/aspnet:10.0
              └── Non-root user, /app/storage/avatars dirs

gateway       FROM runtime-base, COPY /publish/gateway, EXPOSE 8080
api           FROM runtime-base, COPY /publish/api, EXPOSE 8080
realtime      FROM runtime-base, COPY /publish/realtime, EXPOSE 8080
auth          FROM runtime-base, COPY /publish/auth, EXPOSE 8080
adminweb      FROM runtime-base, COPY /publish/adminweb, EXPOSE 8080
web           FROM runtime-base, COPY /publish/web, EXPOSE 8080
worker        FROM runtime-base, COPY /publish/worker (no EXPOSE)
dbseeder      FROM runtime-base, COPY /publish/dbseeder

allinone      FROM runtime-base + apt-get supervisor
              └── COPY all /publish/* dirs
              └── COPY supervisord.conf, entrypoint.sh
              └── COPY /publish/dbseeder (for init migration)
              └── EXPOSE 17000
```

### Build Commands
- All-in-one: `docker compose up` (targets `allinone` stage)
- Advanced: `docker compose -f docker-compose.advanced.yml up` (targets per-service stages)

### Special: Snakk.Web npm Build

The `.csproj` (`src/apps/Snakk.Web/Snakk.Web.csproj`) has MSBuild targets that run `npm run build:css` before build. The `package.json` `postinstall` script uses PowerShell (`Copy-Item`) which won't work on Linux. Solution:

```dockerfile
# In restore stage:
COPY src/apps/Snakk.Web/package.json src/apps/Snakk.Web/package-lock.json ./src/apps/Snakk.Web/
RUN cd src/apps/Snakk.Web && npm ci --ignore-scripts
RUN cp src/apps/Snakk.Web/node_modules/htmx.org/dist/htmx.min.js \
       src/apps/Snakk.Web/wwwroot/js/vendor/htmx.min.js
```

Then `dotnet publish` triggers the MSBuild `BuildTailwindCSS` target automatically.

---

## Database Migration

Use existing `Snakk.DbSeeder` (`src/tools/Snakk.DbSeeder/Program.cs`) — it already calls `MigrateAsync()` and seeds default data.

- **All-in-one**: `entrypoint.sh` runs DbSeeder before starting supervisord
- **Advanced**: `dbseeder` service runs with `restart: "no"` + `depends_on: postgres`; other services wait for dbseeder via `service_completed_successfully` condition

---

## Environment Variables

### `.env.example` — User-Facing Config

```env
# Required
POSTGRES_PASSWORD=changeme
JWT_SECRET_KEY=your-secret-key-min-32-characters
REALTIME_API_KEY=your-realtime-api-key
PUBLIC_URL=http://localhost:17000

# Ports
SNAKK_PORT=17000
POSTGRES_PORT=5432

# Optional: OAuth providers
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
GITHUB_CLIENT_ID=
GITHUB_CLIENT_SECRET=
DISCORD_CLIENT_ID=
DISCORD_CLIENT_SECRET=
```

### Internal Wiring (handled by compose files, not user-configured)

| Service | Key Env Vars | Source File |
|---------|-------------|------------|
| Api | `ConnectionStrings__DbConnection`, `Realtime__BaseUrl`, `Realtime__ApiKey`, `WebClientUrl`, `FileStorage__BasePath` | `src/services/Snakk.Api/Program.cs` |
| Worker | `ConnectionStrings__DbConnection`, `Realtime__BaseUrl`, `Realtime__ApiKey`, `FileStorage__BasePath` | `src/services/Snakk.Worker/Program.cs` |
| Realtime | `ApiKey`, `Cors__AllowedOrigins` | `src/services/Snakk.Realtime/Program.cs` |
| Web | `ApiBaseUrl`, `RealtimeServiceUrl`, `FileStorage__BasePath` | `src/apps/Snakk.Web/Program.cs` |
| Auth | `ApiBaseUrl`, OAuth keys | `src/apps/Snakk.Auth/Program.cs` |
| AdminWeb | `SnakkApi__BaseUrl` | `src/apps/Snakk.AdminWeb/Program.cs` |
| Gateway | `ReverseProxy__Clusters__*__Destinations__*__Address` (4 values) | `src/services/Snakk.Gateway/appsettings.json` |

All services needing JWT: `Jwt__SecretKey`, `Jwt__Issuer`, `Jwt__Audience`

---

## Supervisord Config (All-in-One)

Priority ordering for dependency start order:

| Priority | Service | Binds To | Why |
|----------|---------|----------|-----|
| 100 | Realtime | localhost:17101 | No deps |
| 200 | Api | localhost:17100 | Needs Realtime |
| 300 | Web | localhost:17110 | Needs Api |
| 300 | Auth | localhost:17111 | Needs Api |
| 300 | AdminWeb | localhost:17112 | Needs Api |
| 350 | Worker | (no port) | Needs Api + Realtime |
| 400 | Gateway | 0.0.0.0:17000 | Needs all backends |

All processes log to stdout/stderr (Docker captures). Supervisor runs `nodaemon=true`.

**Key detail**: Api binds to `localhost:17100` (not `0.0.0.0`) — firewalled by default, unreachable from outside the container.

---

## Entrypoint Script (All-in-One)

```
1. mkdir -p /app/storage/avatars/{generated,uploaded}
2. Wait for PostgreSQL TCP on $DB_HOST:$DB_PORT
3. Run DbSeeder (migrations + seed data)
4. exec supervisord (foreground, PID 1)
```

---

## Docker Compose: All-in-One (`docker/docker-compose.yml`)

Two services only:

1. **postgres** — `postgres:17-alpine`, healthcheck, `pgdata` volume
2. **snakk** — builds `allinone` target, depends on postgres healthy, maps `$SNAKK_PORT:17000`, mounts `avatar-storage` volume, receives all env vars from `.env`

### Environment variables passed to the all-in-one container:
- DB connection string (composed from POSTGRES_* vars)
- JWT secrets (from .env)
- Realtime API key (from .env)
- CORS origins (from PUBLIC_URL)
- OAuth provider keys (from .env, optional)
- Internal service URLs are hardcoded localhost (set in supervisord.conf)

---

## Docker Compose: Advanced (`docker/docker-compose.advanced.yml`)

Nine services:

1. **postgres** — same as above
2. **dbseeder** — runs once, `restart: "no"`, depends on postgres
3. **realtime** — `expose: 8080`, no deps
4. **api** — `expose: 8080`, depends on postgres + realtime
5. **worker** — no port, depends on postgres + realtime
6. **web** — `expose: 8080`, depends on api
7. **auth** — `expose: 8080`, depends on api
8. **adminweb** — `expose: 8080`, depends on api
9. **gateway** — `ports: $SNAKK_PORT:8080`, depends on all backends

- Only gateway has a host port mapping
- api has `expose` but no `ports` — accessible only on Docker network
- `avatar-storage` volume shared by api, worker, web
- Each service targets its own Dockerfile stage via `target:`

---

## Technical Notes

### Path Bases
- Realtime and Auth use `UsePathBase()` only in Development — correct for Docker (Production env)
- Gateway strips `/auth` and `/realtime` prefixes via YARP `PathRemovePrefix` transforms
- No code changes needed

### CORS
- Realtime `appsettings.json` uses semicolons but code splits on commas — Docker env var overrides with commas work correctly
- `Cors__AllowedOrigins` set from `PUBLIC_URL`

### HTTPS
- Internal communication uses HTTP (no TLS between containers)
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` handles proxy headers
- Services already configure `ForwardedHeaders.All` with cleared KnownNetworks/KnownProxies
- TLS should be terminated at a load balancer or reverse proxy in front of the Gateway

### Shared Storage Volume
Three services share `/app/storage` for avatars:
- Api writes (generates default avatars via SixLabors.ImageSharp)
- Worker writes (AvatarGenerationHostedService)
- Web reads (serves at `/avatars/*` endpoint)

### NSwag SDK Generation
Generated files (`openapi.json` and `SnakkApiClient.cs`) are committed to git. The build works from static files — no API needed at build time.

---

## Verification

### All-in-One
```bash
cd docker && cp .env.example .env   # edit secrets
docker compose up -d
docker compose logs -f snakk        # watch 7 services + DbSeeder
curl http://localhost:17000          # homepage
curl http://localhost:17000/admin    # → /auth/login redirect
```

### Advanced
```bash
cd docker && cp .env.example .env   # edit secrets
docker compose -f docker-compose.advanced.yml up -d
docker compose -f docker-compose.advanced.yml ps  # 9 containers (7 + dbseeder + postgres)
curl http://localhost:17000          # homepage
curl http://localhost:8080           # FAIL (API not exposed) ✓
```

### Shared Storage
```bash
# All-in-one
docker exec snakk-app ls /app/storage/avatars/

# Advanced
docker exec snakk-api ls /app/storage/avatars/
docker exec snakk-web ls /app/storage/avatars/
```
