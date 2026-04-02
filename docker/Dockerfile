# =============================================================================
# Snakk All-in-One Docker Image
# Multi-stage build: .NET SDK + Node.js → aspnet runtime + supervisord
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Build everything
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install Node.js 22 (needed for TypeScript + Tailwind CSS + Sass in Snakk.Web)
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# --- NuGet restore (cached layer — only invalidated when csproj files change) ---
COPY docker/Snakk.Docker.slnx Snakk.Docker.slnx
COPY src/aspire/Snakk.ServiceDefaults/Snakk.ServiceDefaults.csproj aspire/Snakk.ServiceDefaults/
COPY src/core/Snakk.Domain/Snakk.Domain.csproj core/Snakk.Domain/
COPY src/core/Snakk.Application/Snakk.Application.csproj core/Snakk.Application/
COPY src/core/Snakk.Infrastructure/Snakk.Infrastructure.csproj core/Snakk.Infrastructure/
COPY src/core/Snakk.Infrastructure.Database/Snakk.Infrastructure.Database.csproj core/Snakk.Infrastructure.Database/
COPY src/core/Snakk.Protos/Snakk.Protos.csproj core/Snakk.Protos/
COPY src/core/Snakk.Shared/Snakk.Shared.csproj core/Snakk.Shared/
COPY src/services/Snakk.Api/Snakk.Api.csproj services/Snakk.Api/
COPY src/services/Snakk.Gateway/Snakk.Gateway.csproj services/Snakk.Gateway/
COPY src/services/Snakk.Realtime/Snakk.Realtime.csproj services/Snakk.Realtime/
COPY src/services/Snakk.Worker/Snakk.Worker.csproj services/Snakk.Worker/
COPY src/apps/Snakk.Setup/Snakk.Setup.csproj apps/Snakk.Setup/
COPY src/apps/Snakk.Web/Snakk.Web.csproj apps/Snakk.Web/
COPY src/apps/Snakk.Auth/Snakk.Auth.csproj apps/Snakk.Auth/
COPY src/apps/Snakk.Admin/Snakk.Admin.csproj apps/Snakk.Admin/
COPY src/tools/Snakk.DbSeeder/Snakk.DbSeeder.csproj tools/Snakk.DbSeeder/
RUN dotnet restore Snakk.Docker.slnx

# --- npm install (cached layer — only invalidated when package files change) ---
COPY src/apps/Snakk.Web/package.json src/apps/Snakk.Web/package-lock.json apps/Snakk.Web/
WORKDIR /src/apps/Snakk.Web
RUN npm ci --ignore-scripts
WORKDIR /src

COPY src/apps/Snakk.Setup/package.json src/apps/Snakk.Setup/package-lock.json apps/Snakk.Setup/
WORKDIR /src/apps/Snakk.Setup
RUN npm ci --ignore-scripts
WORKDIR /src

COPY src/apps/Snakk.Auth/package.json src/apps/Snakk.Auth/package-lock.json apps/Snakk.Auth/
WORKDIR /src/apps/Snakk.Auth
RUN npm ci --ignore-scripts
WORKDIR /src

# --- Copy all source code ---
COPY src/ ./

# Manual htmx copy (the postinstall script uses PowerShell which doesn't work on Linux)
RUN cp apps/Snakk.Web/node_modules/htmx.org/dist/htmx.min.js apps/Snakk.Web/wwwroot/js/vendor/htmx.min.js

# Build TypeScript + Tailwind CSS + Sass (not auto-triggered by MSBuild, must run explicitly)
RUN cd apps/Snakk.Web && npm run build

# Build Snakk.Setup CSS (Tailwind + daisyUI)
RUN cd apps/Snakk.Setup && npm run build:css

# Build Snakk.Auth CSS (Sass)
RUN cd apps/Snakk.Auth && npm run build:css

# --- Build the entire solution (resolves shared dependencies once) ---
RUN dotnet build Snakk.Docker.slnx -c Release --no-restore -p:SkipBuildCSS=true -p:SkipFrontendBuild=true

# --- Publish Blazor admin (full publish — needs restore for _framework static web assets) ---
RUN dotnet publish apps/Snakk.Admin/Snakk.Admin.csproj -c Release -o /publish/admin

# --- Publish remaining projects in parallel (no compilation, just copy) ---
RUN dotnet publish services/Snakk.Gateway/Snakk.Gateway.csproj -c Release -o /publish/gateway --no-build --no-restore & \
    dotnet publish services/Snakk.Api/Snakk.Api.csproj -c Release -o /publish/api --no-build --no-restore & \
    dotnet publish services/Snakk.Realtime/Snakk.Realtime.csproj -c Release -o /publish/realtime --no-build --no-restore & \
    dotnet publish services/Snakk.Worker/Snakk.Worker.csproj -c Release -o /publish/worker --no-build --no-restore & \
    dotnet publish apps/Snakk.Auth/Snakk.Auth.csproj -c Release -o /publish/auth --no-build --no-restore & \
    dotnet publish apps/Snakk.Setup/Snakk.Setup.csproj -c Release -o /publish/setup --no-build --no-restore & \
    dotnet publish tools/Snakk.DbSeeder/Snakk.DbSeeder.csproj -c Release -o /publish/dbseeder --no-build --no-restore & \
    dotnet publish apps/Snakk.Web/Snakk.Web.csproj -c Release -o /publish/web --no-build --no-restore & \
    wait

# ---------------------------------------------------------------------------
# Stage 2: Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install supervisord and curl (for health checks)
RUN apt-get update && \
    apt-get install -y --no-install-recommends supervisor curl && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy all published services
COPY --from=build /publish/gateway ./gateway/
COPY --from=build /publish/api ./api/
COPY --from=build /publish/realtime ./realtime/
COPY --from=build /publish/worker ./worker/
COPY --from=build /publish/web ./web/
COPY --from=build /publish/setup ./setup/
COPY --from=build /publish/auth ./auth/
COPY --from=build /publish/admin ./admin/
COPY --from=build /publish/dbseeder ./dbseeder/

# Copy Docker config files
COPY docker/supervisord.conf /etc/supervisor/conf.d/snakk.conf
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Create storage + runtime directories and non-root user
RUN mkdir -p /app/storage/avatars/generated /app/storage/avatars/uploaded /app/run && \
    groupadd -r snakk && useradd -r -g snakk -d /app snakk && \
    chown -R snakk:snakk /app /etc/supervisor/conf.d

EXPOSE 17000
VOLUME ["/app/storage"]

USER snakk
ENTRYPOINT ["/app/entrypoint.sh"]
