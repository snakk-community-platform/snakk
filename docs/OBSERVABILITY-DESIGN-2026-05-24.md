# Observability design — OpenTelemetry on the LGTM stack

Status: **approved** (2026-05-24). Author: Forc0.

Sibling docs to update during implementation: `ARCHITECTURE.md`,
`ENVIRONMENT.md`, `docker/monitoring/README.md` (new).

## Locked decisions (see §8 for rationale)

| # | Decision | Choice |
| - | -------- | ------ |
| 1 | Metrics backend | Prometheus + Tempo + Loki. prometheus-net kept; OTel metrics ALSO go to Prometheus via Collector remote_write. Mimir migration deferred. |
| 2 | OTel Collector | Mandatory in the path (one container; centralises redaction/sampling/routing/BYO-backend). |
| 3 | Sampling | Tail-sampling in prod (keep all errors + traces with root-span >1 s + 10% baseline); 100% head in dev. |
| 4 | Pyroscope | **Phase 2** (ships with Tempo/Loki, not Phase 7). |
| 5 | Error tracking | Grafana logs only (`level=error` Loki queries + alerts). No Sentry/GlitchTip. |
| 6 | Retention | Traces 7 d, logs 7 d, metrics 15 d. |
| 7 | Phase 3 custom spans | `Snakk.Webhook.Deliver` + `Snakk.LinkMetadata.Fetch` only. (Discussion.LoadDetail, Auth.IssueJwt, Realtime.Broadcast → relegated to "future, opportunistic".) |
| 8 | Monitoring profile | Opt-in stays (`docker compose --profile monitoring up`). README + ENVIRONMENT.md prominently document. |
| 9 | Per-container metrics | Collector `dockerstats` receiver. No cAdvisor. |
| 10 | Caddy | Host-Caddy stays documented for production; containerised Caddy added to dev/local docker-compose so observability "just works" out of the box. |

---

## 1. Goals & non-goals

### Goals

- **Make slow requests obvious.** End-to-end latency breakdowns across the
  9-service mesh (Web → Gateway → Api → Auth/Realtime/Worker) so any developer
  can see where a 1.2 s page load actually went.
- **Make errors obvious.** Exceptions, failed gRPC calls, slow DB queries, and
  failed webhook deliveries should appear with full context (which request,
  which user-id-equivalent, which span) without grepping container logs.
- **Same observability surface in dev and prod.** A developer running
  `docker compose up` sees the same Grafana, the same dashboards, the same
  trace explorer that on-call sees in production. No "works on my machine"
  bifurcation.
- **Operator-portable.** A community deploying Snakk on their own VM gets
  observability by default (no SaaS account required) but can point exports
  at any OTLP-compatible backend (Grafana Cloud, Honeycomb, Datadog) via
  env vars.

### Non-goals (for this design)

- Replacing every existing Prometheus dashboard. Today's 6 dashboards
  (overview, application, system, grpc, postgres, valkey) keep working.
- A custom application performance management UI. Grafana is the UI.
- Front-end real-user monitoring (browser-side OTel SDK). Tracking it in
  "future work" — useful but a separate effort.
- Synthetic monitoring / uptime probes. Separate concern; out of scope.

---

## 2. Where we are today

### 2.1 Application telemetry

| Signal       | Status                                         | Source                                                                                        |
| ------------ | ---------------------------------------------- | --------------------------------------------------------------------------------------------- |
| **Logs**     | Serilog → stdout. JSON in prod, console in dev. No central store. | `src/aspire/Snakk.ServiceDefaults/Extensions.cs`                                              |
| **Metrics**  | `prometheus-net` exposes `/metrics`; Prometheus scrapes; Grafana renders. | `Snakk.Web/Program.cs:793` (`MapMetrics`), `docker/monitoring/prometheus.yml`                 |
| **Traces**   | **None.** No `ActivitySource`, no OTel SDK references in any csproj. | —                                                                                             |
| **Profiles** | None.                                          | —                                                                                             |
| **Dashboards** | 6 provisioned: overview, application, system, grpc, postgres, valkey | `docker/monitoring/grafana/dashboards/`                                                       |
| **Aspire**   | Orchestrates 9 services for local dev. ServiceDefaults does NOT include the standard `ConfigureOpenTelemetry()` template Microsoft ships. | `src/aspire/Snakk.AppHost/Program.cs`, `src/aspire/Snakk.ServiceDefaults/Extensions.cs`       |

### 2.2 Infrastructure telemetry

| Component             | Today                                                                  | Critical gaps                                                                              |
| --------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| **Postgres 17**       | `postgres-exporter` → Prometheus. Slow queries logged at >200 ms and lock waits logged to stdout (`postgresql.conf:log_min_duration_statement=200`, `log_lock_waits=on`). | **Slow-query log not shipped to Loki — it dies with the container.** No `pg_stat_statements`. No replica → no lag metrics. No backup health. No autovacuum visibility. |
| **Valkey 8**          | `redis-exporter` → Prometheus.                                         | No slow-log shipping. No client-side hit/miss metrics (app has no Redis instrumentation today). No keyspace metrics. |
| **Caddy** (reverse proxy) | **Runs on the host, not in docker.** JSON access log to `/var/log/caddy/snakk.log` (`docker/Caddyfile`). Built-in `/metrics` Prometheus endpoint **not enabled**. | No metrics, no centralised access logs, no cert-expiry alerts. Caddy is the front door — and we're blind to it. |
| **Host (Linux)**      | `node-exporter` → Prometheus (CPU/RAM/disk/net/FDs).                  | No `journald` shipping. No per-container resource attribution.                            |
| **Container runtime** | None.                                                                  | No cAdvisor — no per-container CPU/RAM/restarts/OOM-kills.                                |
| **Object storage**    | None.                                                                  | S3-compatible writes (R2/MinIO/AWS) un-instrumented; no telemetry on local-disk `snakk-storage` volume beyond raw disk. |
| **Background workers** | Same Serilog stdout.                                                  | No outbox-lag metric. No "last successful run" timestamps. Per audit, `AchievementCheckerWorker` is disabled and nobody notices. |
| **Observability stack itself** | None (Prometheus has its own `/metrics`, used nowhere). | No meta-monitoring — Prometheus down = silent failure; scrape misses unnoticed; Grafana errors invisible. |

### 2.3 Alert rules (`docker/monitoring/alerts.yml`)

7 rules today: `PostgresConnectionsHigh`, `DiskSpaceLow`, `GrpcErrorRateHigh`,
`GrpcChannelNotReady`, `ValkeyMemoryHigh`, `TokenRefreshFailures`,
`HttpLatencyHigh`. No log-based alerts, no alerts on the observability
stack itself, no SLO-style alerts (just static thresholds).

### 2.4 Operational footguns

- **Monitoring is opt-in (`--profile monitoring`).** Most operators
  running `docker compose up` get zero observability. This design needs
  an explicit position (see §8 decision 8).
- **`Snakk.Infrastructure.Services.MetricsService` is not a metrics
  service.** It writes business counters to a Postgres table (achievement
  system). Rename in a follow-up; out of scope here.

### 2.5 The actual gaps, ranked

1. **Distributed tracing across 9 services** — biggest blind spot, biggest
   ROI per hour of work. Auto-instrumentation gives it almost for free.
2. **Centralised logs with trace correlation** — currently every service's
   logs die in its container; no way to join logs to the failing trace.
3. **Postgres / Valkey slow-log shipping** — the data already exists in
   stdout, we just don't keep it. One log shipper unlocks query forensics.
4. **Caddy is unobserved** — surprisingly invisible. Our front door has no
   metrics or shipped logs.
5. **Per-container metrics** — no answer to "is the worker memory-leaking?"
   without SSHing in and running `docker stats`.
6. **Meta-monitoring** — small change, but losing observability silently
   is the worst possible failure mode.

---

## 3. Architecture

### 3.1 Target topology (dev and prod, same shape)

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Snakk services (×9)                          │
│   Web · Api · Auth · Realtime · Worker · Setup · PublicApi ·         │
│                          Admin · Gateway                             │
│  OTel SDK in ServiceDefaults: AspNetCore + HttpClient + Grpc.Net +   │
│  EFCore + Npgsql + StackExchange.Redis + custom Meters/Activities    │
└───────────────────────────────────┬──────────────────────────────────┘
                                    │ OTLP/gRPC (4317)
                                    ▼
                ┌──────────────────────────────────────┐
                │            OTel Collector            │
                │  Receivers: OTLP, filelog,           │
                │             dockerstats, hostmetrics │
                │  Processors: attribute scrubbing,    │
                │              tail sampling (prod)    │
                │  Exporters:  Tempo, Loki, Prom RW    │
                └──┬──────────┬──────────┬─────────────┘
       traces ────┘   logs ──┘ metrics ─┘
              ▼          ▼          ▼
        ┌─────────┐ ┌────────┐ ┌────────────┐
        │  Tempo  │ │  Loki  │ │ Prometheus │ ◀── postgres-exporter
        └─────────┘ └────────┘ └────────────┘     redis-exporter
              ▲          ▲          ▲             node-exporter
              │          │          │             cAdvisor (new)
              └──────────┴──────────┴──────────── Caddy /metrics (new)
                         │                        Collector self-metrics
                         ▼                        (meta-monitoring)
                    ┌─────────┐
                    │ Grafana │  (existing; add Tempo + Loki datasources)
                    └─────────┘

Sources that feed the Collector directly (not via app code):

  Container logs ────► OTel Collector filelog/dockerlog receiver ──► Loki
  Postgres slow log ─► filelog receiver tailing /var/log/postgresql ─► Loki
  Valkey slow log ──► sidecar script polls `SLOWLOG GET` ───────────► Loki
  Caddy access log ─► filelog receiver tailing /var/log/caddy/*.log ─► Loki
  Docker stats ─────► dockerstats receiver ─────────────────────────► Prom
  Host metrics ─────► hostmetrics receiver (or keep node-exporter) ─► Prom
```

### 3.2 Two emission paths during transition

While we migrate, services emit metrics **twice** for a window:

- prometheus-net continues to serve `/metrics` (untouched; existing
  dashboards keep working).
- OTel `Meter` instruments alongside, exported via OTLP → Collector →
  Prometheus remote_write. New custom business metrics use OTel only.

This avoids touching the 6 working dashboards in PR 1 and lets us delete
prometheus-net at our own pace.

### 3.3 Why this topology

- **One Collector** centralizes redaction/sampling/routing. Services stay
  simple — they only know "send OTLP to this endpoint." Swapping backends
  is a Collector-config change, not a redeploy.
- **OTLP/gRPC** is the standard wire format. Every backend (Tempo, Loki,
  Prometheus, Grafana Cloud, Honeycomb, Datadog, Splunk) speaks it.
- **Grafana + Tempo + Loki + Prometheus** is the operator-friendly default:
  self-hostable, single UI, free, no vendor lock. The PromQL/LogQL/TraceQL
  trio works in the same query bar in Grafana 11.
- **Same in dev and prod** means devs hit the same shape they'll debug
  later. Aspire dev dashboard remains available too (sidecar, not
  replacement — see §4).

---

## 4. Dev experience

Two modes, both supported.

### 4.1 "I'm hacking on a feature" — Aspire dashboard

`dotnet run` against `Snakk.AppHost`. ServiceDefaults sets
`OTEL_EXPORTER_OTLP_ENDPOINT` to the Aspire dashboard (Aspire passes this
env var automatically when the dashboard is enabled).

- Traces, logs, metrics in the Aspire dashboard's built-in viewer.
- Fastest feedback loop, no docker required.
- Drawback: no historical store; restart loses data.

### 4.2 "I'm debugging a real-feeling scenario" — docker LGTM

`docker compose up`. The same stack a small operator runs in production.
Services emit OTLP to the Collector container; data lands in Tempo / Loki /
Prometheus; Grafana is at `:3000`.

- Persistent volumes; can restart services and inspect what happened.
- Same dashboards / explorers a production operator uses.
- Higher footprint (Tempo + Loki + Collector add ~300 MB RAM).

Devs choose per task. Aspire dashboard for hot-loop iteration; docker LGTM
when reproducing prod issues or building observability features.

### 4.3 Concrete dev URLs (after this design lands)

| Service          | Dev URL                      | Notes                                          |
| ---------------- | ---------------------------- | ---------------------------------------------- |
| Grafana          | `http://localhost:17030/grafana/` (gateway-routed, existing) | Already provisioned. New: Tempo + Loki datasources |
| Aspire dashboard | `http://localhost:18888`     | Only when `dotnet run` against AppHost          |
| OTel Collector   | `http://localhost:4317` (OTLP/gRPC), `:4318` (OTLP/HTTP) | Internal; services point here                  |
| Tempo            | not exposed; queried via Grafana | container-internal                             |
| Loki             | not exposed; queried via Grafana | container-internal                             |
| Prometheus       | existing                     | unchanged                                      |

---

## 5. Production experience

### 5.1 Self-hosted (default for community operators)

Same docker-compose. Tempo, Loki, OTel Collector are part of the base
stack. Operators get full observability without any SaaS account. They
get the same Grafana UI we develop against.

**Resource budget (rough, single-node small instance):**

- Tempo: ~150 MB RAM, ~5 GB/day storage for a small community (heavy
  trace compression; 7-day retention).
- Loki: ~150 MB RAM, depends on log volume. Default retention 7 days.
- OTel Collector: ~100 MB RAM. CPU bursts with tail sampling.
- Total added: ~400 MB RAM + ~10–20 GB disk for week of telemetry.

**Retention defaults:** 7 days for traces and logs, 15 days for metrics.
Operators can extend via env vars / collector config.

### 5.2 Bring-your-own backend (advanced operators)

Single env var: `OTEL_EXPORTER_OTLP_ENDPOINT`. Set it to
`https://otlp-gateway-prod-eu-west-2.grafana.net/otlp` (Grafana Cloud) or
any other OTLP endpoint with `OTEL_EXPORTER_OTLP_HEADERS=Authorization=...`
and the services push directly. The local Collector becomes optional in
this mode (operator can run a Collector locally if they want
redaction/sampling, or skip and push raw).

### 5.3 Sampling

- **Dev:** 100% sample. Always.
- **Prod default:** tail sampling in the Collector.
  - Keep 100% of traces with any error span.
  - Keep 100% of traces with root-span duration > 1 s.
  - Sample 10% of remaining "normal" traces.
- **Rationale:** head sampling is cheap but you lose the 1-in-10 slow
  request — exactly the one you want. Tail sampling holds traces in the
  Collector for ~5 s before deciding, then drops the boring ones. Modest
  Collector RAM cost; high value.
- **Operator override:** `OTEL_TRACES_SAMPLER_ARG` env var on the Collector.

### 5.4 PII & GDPR

This codebase already has GDPR-stub issues (per `SECURITY-AUDIT-2026-05-14.md`).
We don't want to make them worse:

- **Block list (Collector config, `attributes` processor):** strip
  `http.request.header.authorization`, `http.request.header.cookie`,
  `http.response.header.set-cookie`, `db.statement` SQL parameters, any
  attribute matching regex `(?i)(password|secret|token|otp|api[_-]?key)`.
- **User IDs in traces:** allowed, but as ULIDs (already opaque). Never log
  emails / phone numbers in spans.
- **User-data export (GDPR DSAR):** when the broken export is fixed, scope
  to include trace IDs the user generated in the last N days (best-effort;
  retention drops them after 7 days anyway).
- **Right to be forgotten:** rely on the 7-day retention as the deletion
  mechanism. Tempo/Loki support time-range deletes if a stronger guarantee
  is needed.

### 5.5 Trace identity & context propagation

- **Resource attributes** (set once per service): `service.name`,
  `service.version` (from assembly), `service.instance.id` (hostname +
  container ID), `deployment.environment`.
- **Propagation:** W3C `traceparent` everywhere.
  - HTTP / HttpClient: auto, OTel default.
  - gRPC (Grpc.Net): auto.
  - SignalR: needs explicit propagation. Server sends `traceparent` in the
    hub method invocation context; client side picks it up. **Action item:**
    add a small SignalR `IHubFilter` for this.
  - Background workers consuming domain events: capture the trace context
    in the outbox row at write time; resume it when the worker dequeues.
    **Action item:** add `traceparent` column to event/outbox tables and
    plumb through `ActivityContext`.
  - Browser → server: server emits `Server-Timing` and (optionally)
    `traceparent`; full browser-side spans are future work.

---

## 6. What we instrument

### 6.1 Auto-instrumentations (zero code, pulled from OTel contrib packages)

| Package                                        | What it gives                                                                |
| ---------------------------------------------- | ---------------------------------------------------------------------------- |
| `OpenTelemetry.Instrumentation.AspNetCore`     | HTTP server spans, request metrics                                           |
| `OpenTelemetry.Instrumentation.Http`           | HttpClient spans                                                             |
| `OpenTelemetry.Instrumentation.GrpcNetClient`  | gRPC client spans, propagates traceparent                                    |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | EF query spans with command text (gated)                                |
| `Npgsql.OpenTelemetry`                         | Lower-level Postgres connection spans                                        |
| `OpenTelemetry.Instrumentation.StackExchangeRedis` | Cache hit/miss spans                                                     |
| `OpenTelemetry.Instrumentation.Runtime`        | .NET runtime metrics (GC, threadpool, exceptions)                            |
| `OpenTelemetry.Instrumentation.Process`        | Process CPU/memory                                                           |

### 6.2 Custom instrumentation (the high-value spots)

Business spans worth adding manually:

- **`Snakk.Discussion.LoadDetail`** — wraps the full discussion-detail load
  (the page the bug we just fixed lives on). Children: gRPC to Api,
  permission check, post pagination, OG-image fetches.
- **`Snakk.Auth.IssueJwt`** — wraps token issuance; counts by reason
  (login / refresh / sudo). Useful for the security audit work.
- **`Snakk.Webhook.Deliver`** — wraps webhook delivery attempts; attributes
  for webhook ID, attempt number, final status. Counters for
  success/failure/retry-scheduled.
- **`Snakk.LinkMetadata.Fetch`** — wraps the link-preview pipeline (the
  one PR #42 just hardened). Useful for tracking SSRF blocks and slow
  external fetches.
- **`Snakk.Realtime.Broadcast`** — SignalR fan-out spans, attributes for
  hub name, recipient count.

Custom Meters worth adding (alongside or replacing prometheus-net counters):

- `snakk.posts.created` — counter, labels: space ID is too high-cardinality;
  use space type (community/hub) only.
- `snakk.auth.login_attempts` — counter, labels: result
  (success / wrong_password / locked / 2fa_failed).
- `snakk.cache.hits` / `snakk.cache.misses` — counters per cache name.
- `snakk.webhooks.delivery.duration` — histogram.

### 6.3 Infrastructure telemetry sources

Application instrumentation only tells half the story. These sources cover
the layers underneath the .NET services.

| Component       | What we collect                                                        | How                                                                              | Phase |
| --------------- | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------- | ----- |
| **Postgres**    | Connection counts, replication lag, transaction rate, deadlocks (existing) | Keep `postgres-exporter` → Prometheus                                            | —     |
|                 | Per-query stats: calls, mean/max time, rows, shared-block hits          | Enable `pg_stat_statements` extension in `postgresql.conf:shared_preload_libraries`; postgres-exporter reads automatically | 2.5   |
|                 | Slow queries (>200 ms, already logged) + lock waits (already logged)    | Collector `filelog` receiver tails the Postgres container log → Loki             | 2     |
|                 | Backup health (last successful base backup, WAL size)                   | Custom probe script → textfile collector OR push-gateway. Skip if no backups configured. | 5     |
| **Valkey**      | Hits/misses/evictions/memory/persistence (existing)                    | Keep `redis-exporter` → Prometheus                                              | —     |
|                 | Slow log entries                                                       | Tiny sidecar: poll `SLOWLOG GET 100 / RESET` every 30 s, write JSON to stdout, Collector ships to Loki | 2.5   |
|                 | Client-side hit/miss/duration from each .NET service                   | `OpenTelemetry.Instrumentation.StackExchangeRedis` (Phase 1 — already in §6.1)   | 1     |
| **Caddy**       | Request rate, latency, status codes, cert expiry                       | Enable Caddy's built-in Prometheus endpoint in `Caddyfile` (`servers { metrics }`); scrape | 2.5   |
|                 | Access logs (already JSON to file)                                     | Collector `filelog` receiver tails `/var/log/caddy/*.log`; parsed JSON → Loki    | 2.5   |
| **Container runtime** | Per-container CPU, RAM, network, FS, restart count, OOM-kill flag | `cAdvisor` container (Google's standard) OR Collector `dockerstats` receiver. Prefer `dockerstats` (one less container, OTel-native). | 2.5   |
| **Host (Linux)** | CPU/RAM/disk/net/FDs (existing)                                       | Keep `node-exporter` (works fine; deeper than `hostmetrics` for some collectors) | —     |
|                 | systemd / journald                                                     | Collector `journald` receiver → Loki (only if Caddy or other systemd services need it) | 2.5   |
| **Object storage** | S3 SDK client spans (PUT, GET, DELETE, list)                         | Manual: wrap `IFileStorage` calls in an `ActivitySource`. Skip provider-side metrics (CloudWatch/R2 analytics live in the provider). | 3     |
|                 | Local-disk `snakk-storage` volume — size, inode usage                  | Covered by `node-exporter` filesystem collector if mount is visible              | —     |
| **OTel Collector itself** | Pipeline throughput, dropped spans/metrics/logs, queue depth, exporter errors | Collector exposes its own `/metrics`; scrape from Prometheus                  | 2     |
| **Tempo / Loki / Prometheus / Grafana** | Ingest rate, query latency, storage usage, errors      | Each ships own `/metrics`; scrape (very small additional config)                | 5     |
| **Background workers** | Outbox lag (events queued vs processed), per-job last-run timestamp, retry counts | Custom Meters in `Snakk.Worker` (`snakk.outbox.lag_seconds`, `snakk.worker.last_run_unix`) | 3     |

### 6.4 Cross-cutting: what we DON'T instrument

- Per-user-ID labels on metrics (cardinality explosion; user IDs go in
  trace attributes instead).
- Detailed SQL parameters in EF spans (PII risk; we get the operation
  name + table from `db.operation` and `db.sql.table` which is enough
  for performance work).
- Request body / response body capture (PII + cost; rely on logs at
  level-error for failures).

---

## 7. Phased delivery

Each phase is a single PR; each PR is independently mergeable.

### Phase 1 — ServiceDefaults OTel + Aspire dashboard (S, ~2 hrs)

- Add `ConfigureOpenTelemetry()` to `Snakk.ServiceDefaults` matching the
  Aspire template, plus the instrumentation packages from §6.1.
- Wire it into all 9 services via the existing `AddSnakkDefaults()` call.
- Add `MapDefaultEndpoints()` for `/health/live` and `/health/ready`
  (currently missing — Aspire normally ships this; ours doesn't).
- Default OTLP endpoint reads `OTEL_EXPORTER_OTLP_ENDPOINT`; falls back
  to the Aspire-dashboard endpoint in dev.
- **Outcome:** running `dotnet run` against AppHost shows traces, logs,
  metrics in the Aspire dashboard. No docker changes; no production
  effect yet (no OTLP receiver in compose).

### Phase 2 — docker-compose Collector + Tempo + Loki + **Pyroscope** (M, ~1 day)

- New docker-compose services: `otel-collector`, `tempo`, `loki`,
  `pyroscope` (per locked decision #4 — profiling ships with traces/logs,
  not deferred).
- Collector config: receivers (OTLP gRPC+HTTP), processors (attribute
  scrubbing, batch), exporters (Tempo, Loki, Prometheus remote_write).
- Pyroscope: `Grafana.Pyroscope.Profiler` agent in ServiceDefaults, push
  endpoint configured.
- Grafana datasources provisioned for Tempo + Loki + Pyroscope (file in
  `docker/monitoring/grafana/provisioning/datasources/`).
- Two starter Grafana dashboards:
  - **"Snakk Traces"** — RED panels per service (rate, errors, duration)
    + trace explorer.
  - **"Snakk Profiles"** — CPU + alloc flame graphs linked from trace spans.
- Document new resource budget and ports in `docs/ENVIRONMENT.md`.
- All gated by `--profile monitoring` (per locked decision #8).
- **Outcome:** `docker compose --profile monitoring up` and devs see
  traces, centralised logs, and flame graphs in Grafana for the first time.

### Phase 2.5 — Infrastructure telemetry (S–M, ~half day)

Ship the under-the-app layers into the same pipeline. Independent of Phase 3
(app custom spans) — can land in either order.

- Collector `filelog` receivers for: Postgres container log (slow queries +
  lock waits, already produced), Caddy access log (already JSON), worker
  service logs (already structured). Multi-line JSON parsing in the
  Collector; ship to Loki with labels `{service, log_type}`.
- Tiny `valkey-slowlog` sidecar container — 20-line shell script polling
  `SLOWLOG GET 100 / SLOWLOG RESET` every 30 s; stdout consumed by
  Collector's `dockerlog` receiver.
- Enable Caddy's built-in Prometheus endpoint (`servers { metrics }` in
  the Caddyfile snippet) + add Prometheus scrape job.
- Enable Postgres `pg_stat_statements` extension (add to
  `shared_preload_libraries` in `postgresql.conf`; needs a Postgres
  restart). Postgres-exporter picks it up automatically.
- Collector `dockerstats` receiver for per-container CPU/RAM/restarts (one
  config block; no extra container needed if we use OTel-native; cAdvisor
  is the alternative if dockerstats receiver proves too thin).
- Add dashboards: **"Snakk Infra"** (Caddy + Postgres query top-N + Valkey
  slow log + container resource usage) and **"Snakk Logs"** (a Loki
  Explore template with pre-set queries for "errors last 1h", "5xx
  responses last 1h", "Postgres slow queries last 1h", "SSRF blocks
  last 24h").
- **Outcome:** the whole stack — from Caddy through Postgres to the
  workers — is visible in Grafana. Centralised logs with trace correlation
  for app-emitted spans. Slow queries no longer die with the container.

### Phase 3 — Custom Meters & ActivitySources for high-value flows (S–M)

Scope-narrowed per locked decision #7 — two custom ActivitySources, both
tied to in-flight security/audit work where visibility has immediate
operational value. Auto-instrumentation from Phase 1 still covers
Discussion-detail and Auth flows at the HTTP/gRPC/EF span level; we just
don't wrap them in a named business span.

- `Snakk.Webhook.Deliver` — webhook delivery attempts. Attributes:
  webhook ID, attempt number, final status, retry-scheduled boolean.
  Counters: success / failure / retry-scheduled / dead-lettered.
  Pairs with audit finding that webhooks may be silently dead (CR-30).
- `Snakk.LinkMetadata.Fetch` — link-preview pipeline. Attributes: URL
  scheme, content-type, blocked-reason (when SSRF guard fires).
  Counters: success / ssrf-blocked / parse-failed / timeout. Lets us
  watch the §HI-54/HI-55 fix from PR #42 working in real time.
- Custom counters from §6.2 (login outcomes, post creation, cache
  hits/misses, webhook delivery duration histogram).
- Dashboard: **"Snakk Business"** — login outcomes, post creation rate,
  webhook delivery health, link-preview throughput + SSRF block rate.
- **Outcome:** dashboards show the things audit/operations care about,
  not just generic HTTP/gRPC metrics.

**Deferred (opportunistic, not committed):** `Snakk.Discussion.LoadDetail`,
`Snakk.Auth.IssueJwt`, `Snakk.Realtime.Broadcast`. Add when a specific
investigation needs them.

### Phase 4 — Trace propagation gaps: SignalR + outbox (S–M)

- `IHubFilter` carrying traceparent across SignalR boundaries.
- `traceparent` column added to outbox/event tables; resumed in the
  worker.
- **Outcome:** end-to-end traces span the realtime + async event paths,
  not just sync request paths.

### Phase 5 — Sampling, redaction, prod hardening (S)

- Tail-sampling policy in the Collector config.
- Redaction rules (PII block list) verified with a fixture trace.
- Production override (`docker-compose.production.yml`) adds the
  monitoring stack with sensible volume mounts and resource limits.
- Document operator overrides for shipping to Grafana Cloud /
  Honeycomb / Datadog.
- **Outcome:** prod-safe defaults.

### Phase 6 — Migration off prometheus-net (S)

- Stop registering `prometheus-net` middleware in Web + Api.
- Remove `/metrics` endpoint registration.
- Update Prometheus scrape config: drop `snakk-web` / `snakk-api`
  jobs; metrics now arrive via Collector remote_write.
- Update existing 6 dashboards to use OTel-named metrics (find/replace
  on metric names; mostly mechanical).
- **Outcome:** single metric pipeline; one fewer abstraction.

### Phase 7 — (was Pyroscope; moved into Phase 2 per locked decision #4)

### Phase 8 (optional, future) — Browser-side RUM

- `@opentelemetry/sdk-trace-web` in `Snakk.Web/Scripts/`. Browser spans
  for page loads, htmx swaps, SPA-style flows. Connects to existing
  server-side traces via injected `traceparent`.

---

## 7a. Meta-monitoring (observability for the observability stack)

The worst failure mode is silent loss of observability: Prometheus crashed,
nobody noticed, an outage happens during the blind window. Specific
guarantees we want:

- **"Is the Collector alive and ingesting?"** — alert if
  `otelcol_receiver_accepted_spans` rate drops to zero for >5 min during
  business hours, or if `otelcol_exporter_send_failed_*` rises.
- **"Is Prometheus scraping everything?"** — `up == 0` alert per scrape
  target; also alert if scrape duration > scrape interval (will start
  dropping samples).
- **"Is Loki keeping up?"** — `loki_distributor_lines_received_total`
  rate vs `loki_ingester_chunks_flushed_total`; ingestion-lag alert.
- **"Is Tempo accepting traces?"** — `tempo_distributor_spans_received_total`
  flat = something upstream broke.
- **"Is Grafana reachable?"** — Caddy-level probe (`/grafana/api/health`
  returns 200) or an external uptime check. Grafana isn't on the path of
  serving traffic; if it's down only operators notice — but they need to
  know.
- **Out-of-band fallback.** If the whole monitoring stack is gone, you
  need *one* alert that fires from somewhere else. Options:
  - A 1-line cron on the host that curls Prometheus's `/-/healthy` every
    minute and emails / Discord-webhooks on failure.
  - An external probe (UptimeRobot / your own pinger / Grafana Cloud
    free tier set up just for this).
  - Recommended: external probe. The cron-on-same-host is dead if the
    host is.

Phase 5 wires the in-stack meta-alerts. The external probe is an
operator-deployment concern documented in `ENVIRONMENT.md`, not in code.

## 7b. Alert taxonomy

The 7 existing rules already cover some app and infra. Below is the
target inventory — what each layer should alert on, by severity. Use as
a checklist when authoring new rules; not all of these need to land in
Phase 5 (mark which are MVP vs nice-to-have).

| Layer | Critical (page now)                                                     | Warning (next business day)                                              |
| ----- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| **App — request path** | HTTP 5xx rate >1% for 5 min · P95 latency >2 s for 5 min · all instances of a service unreachable | P99 latency regression >50% vs 7d average · 5xx rate >0.1% for 30 min     |
| **App — auth** | Token refresh failure rate >10/s (existing) · login failure spike (suggests credential stuffing) | 2FA enable/disable rate >10× baseline · OAuth callback failures >5/min   |
| **App — workers** | Outbox lag >5 min · worker process not running                          | Outbox lag >1 min · webhook delivery success rate <90% for 1 h           |
| **gRPC** | Channel not Ready >1 min (existing) · error rate >5% for 5 min          | Error rate >1% for 5 min (existing)                                      |
| **Postgres** | Connections >95% of max (currently 80% at warning) · disk full <10% · replication lag >30s (when there's a replica) | Connections >80% (existing) · slow queries >1/s sustained · deadlocks    |
| **Valkey** | Memory >95% of maxmemory · evictions spiking · primary unreachable     | Memory >85% (existing) · slow log entries >1/min                          |
| **Caddy** | 5xx rate >1% · cert expires in <7 days · process down                  | 4xx rate spike · cert expires in <30 days                                |
| **Host** | Disk <10% free (existing critical at 20%) · load average >2× core count for 10 min · OOM-kill in last 1 min | Disk <20% free · file descriptors >80% · swap activity                   |
| **Containers** | Restart loop (>3 restarts in 10 min) · OOM-killed                       | Memory >90% of limit for 10 min · CPU throttled >50% of time             |
| **Object storage** | S3 client error rate >5% for 5 min                                     | Storage usage >80% of operator's quota (where known)                    |
| **Observability stack** | Collector exporter send-failed >0 for 5 min · Prometheus down · external uptime probe failing | Loki ingest lag >30 s · Tempo dropped spans >0 · scrape duration close to interval |
| **Security** (overlap with audit) | SSRF block rate >baseline×10 in 5 min · admin password rotation never happened (per CR-17) | Rate-limit-trigger rate spike · brute-force pattern from one IP        |

Most "warning" rules are best authored as **SLO-burn-rate alerts** rather
than static thresholds once the system has 30 days of baseline data. Phase
5 introduces static thresholds (fast to ship); SLOs are a follow-up.

## 8. Decisions to confirm

These are calls I'm making in this draft. Push back on any.

1. **Backend default: self-hosted Tempo + Loki + (existing) Prometheus in
   docker.** Alternative: drop in Mimir to replace Prometheus and emit
   metrics exclusively via OTel. I default to "keep Prometheus, add Tempo
   + Loki" because the 6 existing dashboards already work. Mimir migration
   becomes optional.

2. **OTel Collector in the pipeline (vs services pushing direct to
   backends).** Adds one hop and one container but centralizes
   redaction/sampling/routing. I default to having it; the alternative
   ("direct push") is fine for very small deploys but doesn't scale to
   the BYO-backend operator story.

3. **Tail sampling in prod, head sampling 100% in dev.** Alternative is
   head sampling everywhere with a configurable rate. I default to tail
   sampling because it's the only way to keep all errors without keeping
   everything. Costs ~50 MB Collector RAM.

4. **Continuous profiling (Pyroscope) is Phase 7, optional.** Could be
   Phase 2 if you want it sooner.

5. **Error tracking via Grafana logs (filter by `level=error`).** Not
   adopting Sentry or a separate error-tracking SaaS. The audit work
   would benefit from grouped/triaged errors; Grafana works but Sentry's
   UX is nicer. If you want Sentry, that's a separate decision.

6. **Trace retention 7 days, logs 7 days, metrics 15 days.** Trades
   storage for forensic depth. Reasonable defaults for a community
   platform; tweak per operator.

7. **MUST-have spans in Phase 3: the 5 listed in §6.2.** Open to adding /
   dropping.

8. **Monitoring stack always-on by default (drop the `--profile monitoring`
   gate).** Today's docker-compose hides Prometheus/Grafana/exporters
   behind `--profile monitoring`, so a community running `docker compose up`
   gets zero observability and won't know until something breaks.
   Recommendation: make the monitoring stack the default, and add a
   `--profile minimal` for the rare operator who explicitly opts out
   (resource-constrained VPS). Alternative: keep opt-in, document
   prominently in README, accept that most deploys are unmonitored.

9. **Per-container metrics: Collector `dockerstats` receiver vs cAdvisor
   container.** dockerstats is OTel-native (no extra container, same
   config file). cAdvisor is more battle-tested with deeper metrics.
   Defaulting to dockerstats; switch to cAdvisor if dockerstats proves
   thin in practice.

10. **Caddy as a docker service.** Today the Caddyfile is "copy to host
    `/etc/caddy/Caddyfile`" — Caddy runs outside docker. Moving Caddy
    into docker-compose simplifies log/metric collection (Collector
    `dockerlog` + Caddy `/metrics` both via container network) and makes
    the stack self-contained. Tradeoff: existing operators have host-Caddy
    configs they'd need to migrate. **Recommendation:** support both —
    keep host-Caddy as the documented production pattern (cert renewal
    easier as systemd unit) but add a containerised Caddy for the
    docker-compose default so observability "just works" in
    `docker compose up`.

---

## 9. Open questions

- **Who runs the dev Collector — Aspire or docker?** Right now I'm
  proposing both modes (Aspire dashboard for the hot loop, docker for
  realistic scenarios). Could simplify to docker-only and let Aspire pass
  OTLP_ENDPOINT through to the container, getting one path. Pro: one
  truth. Con: docker startup time hurts hot-loop iteration.

- **Should the Collector live in `Snakk.Docker.slnx` or as a sibling to
  the existing monitoring services in `docker/monitoring/`?** I'd
  suggest the latter for consistency.

- **JSON vs Protobuf OTLP.** Default gRPC (Protobuf) is fastest. HTTP/JSON
  is friendlier for debugging. Use gRPC; expose HTTP only if needed.

- **Aspire 9 vs 10 dashboard OTLP support.** Need to verify the version
  here exposes a stable OTLP endpoint that ServiceDefaults can target by
  default. (I'll confirm before Phase 1.)

---

## 10. Out of scope (call out so they don't sneak in)

- Synthetic uptime monitoring (Grafana Synthetic Monitoring or Pingdom).
- APM-style code-level profiling (covered by Phase 7 Pyroscope if we go
  there).
- Log-based alerting beyond the existing `alerts.yml` Prometheus rules
  (Loki alerting is possible but not in this design).
- Mobile / MAUI app telemetry (the `snakk-app` repo) — separate effort.
- Audit-log unification. `AuthAuditLogger` writes to ILogger only per
  the security audit; fixing it to write to the AuditLog table is a
  security-audit deliverable, not an observability one.

---

## 11. Implementation notes / files that will change

(Concrete pointers so a follow-up implementer can find them.)

| Phase | File(s)                                                                                       | Change                                                                |
| ----- | --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| 1     | `src/aspire/Snakk.ServiceDefaults/Snakk.ServiceDefaults.csproj`                                | Add `OpenTelemetry.*` package refs                                    |
| 1     | `src/aspire/Snakk.ServiceDefaults/Extensions.cs`                                              | Add `ConfigureOpenTelemetry()`; bridge Serilog → OTel logs            |
| 1     | All 9 service `Program.cs`                                                                    | Call `.ConfigureOpenTelemetry()` from `AddSnakkDefaults()` (one site) |
| 1     | each `appsettings.json`                                                                       | OTEL_* defaults                                                        |
| 1     | `src/aspire/Snakk.ServiceDefaults/Extensions.cs`                                              | `MapDefaultEndpoints()` for `/health/live` + `/health/ready`           |
| 2     | `docker/docker-compose.yml`                                                                   | Add `otel-collector`, `tempo`, `loki` services                         |
| 2     | `docker/monitoring/otel-collector-config.yaml` (new)                                          | Receivers / processors / exporters                                     |
| 2     | `docker/monitoring/tempo.yaml` (new), `docker/monitoring/loki.yaml` (new)                     | Storage config                                                        |
| 2     | `docker/monitoring/grafana/provisioning/datasources/tempo.yml` (new), `.../loki.yml` (new)    | Datasource registration                                               |
| 2     | `docker/monitoring/grafana/dashboards/snakk-traces.json` (new)                                | Starter trace dashboard                                                |
| 2     | `docker/monitoring/grafana/dashboards/snakk-profiles.json` (new)                              | Pyroscope flame-graph dashboard (per locked decision #4)              |
| 2     | `docker/docker-compose.yml`                                                                   | Add `pyroscope` service to `monitoring` profile                       |
| 2     | `src/aspire/Snakk.ServiceDefaults/Snakk.ServiceDefaults.csproj`                               | Add `Grafana.Pyroscope.Profiler` package                              |
| 2     | `src/aspire/Snakk.ServiceDefaults/Extensions.cs`                                              | Wire Pyroscope agent in `ConfigureOpenTelemetry()`                    |
| 2     | `docker/docker-compose.yml`                                                                   | Add containerised Caddy to dev (per locked decision #10)              |
| 2     | `docker/Caddyfile.dev` (new)                                                                  | Dev Caddyfile; shares upstream config with prod Caddyfile             |
| 2     | `docs/ENVIRONMENT.md`                                                                         | New ports, env vars, resource budget                                  |
| 2.5   | `docker/postgresql.conf`                                                                      | Add `pg_stat_statements` to `shared_preload_libraries`                |
| 2.5   | `docker/Caddyfile`                                                                            | Enable built-in `servers { metrics }` block                            |
| 2.5   | `docker/docker-compose.yml`                                                                   | `valkey-slowlog` sidecar (20 lines), Collector receivers for filelog + dockerstats |
| 2.5   | `docker/monitoring/otel-collector-config.yaml`                                                | Add filelog/dockerstats receivers + Loki exporter labels              |
| 2.5   | `docker/monitoring/grafana/dashboards/snakk-infra.json` (new), `snakk-logs.json` (new)        | Caddy + Postgres top-N + Valkey slow + container resources; Logs Explore preset |
| 2.5   | `docker/monitoring/prometheus.yml`                                                            | Add scrape jobs for Caddy + Collector self-metrics                    |
| 5     | `docker/monitoring/alerts.yml`                                                                | Meta-monitoring rules (§7a) + alert taxonomy MVP rules (§7b)          |
| 3     | `Snakk.Web/Pages/Discussions/Detail.cshtml.cs`, `Snakk.Auth/.../AuthGrpcService.cs`, etc.     | Manual `ActivitySource` spans for §6.2 flows                          |
| 4     | `Snakk.Realtime/Hubs/`                                                                        | `IHubFilter` for traceparent                                          |
| 4     | DB migration for outbox `traceparent` column; worker plumbing                                 | —                                                                     |
| 5     | `docker/monitoring/otel-collector-config.yaml`                                                | Tail-sampling policy; PII block list                                  |
| 6     | `Snakk.Web/Program.cs:793`, `Snakk.Api/Program.cs`                                            | Remove `MapMetrics`; remove prometheus-net package refs                |
| 6     | `docker/monitoring/prometheus.yml`                                                            | Drop snakk-web / snakk-api scrape jobs                                |

---

## 12. Glossary

- **LGTM** — Loki (logs) + Grafana (UI) + Tempo (traces) + Mimir (metrics).
  Grafana Labs' open-source observability stack. We use a "GTL + Prometheus"
  variant initially.
- **OTLP** — OpenTelemetry Protocol. Standard wire format for all signals.
  gRPC default, HTTP/Protobuf alternative.
- **RED metrics** — Rate, Errors, Duration. Per-service summary panels.
- **Tail sampling** — sampling decision made after the trace finishes, so
  errors and slow traces can be kept while normal traffic is sampled down.
- **Head sampling** — decision at trace start. Cheap but blind to outcomes.
- **W3C traceparent** — the standard HTTP header for trace context
  propagation. Looks like `00-{trace_id}-{span_id}-{flags}`.
- **MUST/SHOULD/MAY** (§2) — RFC 2119 conventions for what's required vs
  optional in this design.
