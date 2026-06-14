# Observability ops — dos and don'ts

Pairs with `docs/OBSERVABILITY-DESIGN-2026-05-24.md` (what we built). This doc
covers how to **run** the stack safely. If you're about to ship the dev compose
to anything that isn't a developer laptop, read this first.

Status: living document. Update as new hardening decisions land.

---

## 1. Dev-vs-prod matrix

The dev compose deliberately relaxes a handful of constraints so the
`docker compose --profile monitoring up -d` flow gives a working stack
end-to-end on a laptop. Each row below is something that **must change
before the same stack runs anywhere except a single developer's machine**.

| Surface | Dev setting | Prod hardening required |
| --- | --- | --- |
| `otel-collector` container user | `user: "0:0"` (root) so filelog can read other containers' log files via shared named volumes | Drop `user: "0:0"`; pick one of: (a) align UIDs across producer containers and use a shared `observability` GID, (b) init-container chowns the log volumes to the collector's nonroot UID at boot, (c) every log source writes mode 0644 (Postgres already does — extend to Caddy + Valkey shippers). |
| `otel-collector` mount of `/var/run/docker.sock` | Read-only bind from host | Front the socket with `tecnativa/docker-socket-proxy` (or equivalent) configured with `CONTAINERS=1`, `IMAGES=0`, `EXEC=0`, `POST=0`, `INFO=0`, `VERSION=1` and put the collector + proxy on their own bridge network. Read-only on the socket DOES NOT restrict API surface — a process inside the container can still inspect env vars, list secrets, and read bind-mount host paths. |
| Postgres `log_file_mode` | `0644` (Postgres process owner is `postgres`; collector reads as root) | If collector runs nonroot, either chown to a shared GID or keep `0644` but tighten the parent directory. The mode 0644 itself is fine for redacted logs but only because we currently scrub statements before they hit Loki. |
| `node-exporter` mount of `/` | `:ro` only (no `rslave`) so Docker Desktop on WSL2 can start the container | Native-Linux operators wanting live mount-table tracking should override the bind to `/:/host/root:ro,rslave` via a compose overlay. Without `rslave` you still get a point-in-time snapshot of mountpoints at container start. |
| Prometheus `--enable-feature=otlp-write-receiver` | On (lets services / pipelines write OTLP directly to Prometheus) | Keep on if you accept that any process that can reach Prometheus on its admin port can write series. In prod restrict network reachability to the collector only — don't expose Prometheus to the app subnet. |
| Prometheus `--storage.tsdb.retention.size=1GB` | Bound to 1 GB so a dev laptop doesn't fill its disk | Pick a real retention policy: `--storage.tsdb.retention.time=15d` (matches design §6) is the floor; size cap optional. |
| Tempo `user: "0:0"` | Tempo chowns its data dir on first start, then keeps running as root | Tempo image's nonroot user works after first boot; switch to it after the volume is initialised. |
| Caddy `admin :2019` | Listens on the Docker network for the Prometheus scrape | Already restricted to the Docker network in dev. In prod, keep the admin endpoint off any public interface. Never expose 2019 on a public IP — the Caddy admin API can reconfigure routes. |
| `attributes/scrub` processor allowlist | Drops the five obvious header keys (`authorization`, `cookie`, `set-cookie`, `x-api-key`, `proxy-authorization`) plus URL query string before export | Every new instrumentation that emits spans MUST go through this processor. Don't bypass with a sibling pipeline. The allowlist is intentionally short — extend it whenever a new sensitive header pattern appears, never narrow it. |
| `pg_stat_statements.max` | Postgres default (5000) | Fine for prod too. Bound the Grafana dashboards by `LIMIT 50` so a query-storm doesn't blow up Prometheus cardinality. |
| Grafana admin password | `${GRAFANA_ADMIN_PASSWORD}` from `.env` (defaults to a known value in dev installs) | Generate per-deployment. Never commit the `.env` file. Rotate when an admin leaves. |
| MCP `.mcp.json` token scope | Project-scoped Viewer token in `.mcp.json` (gitignored) | Same in prod, plus periodic rotation. Document the rotation procedure (Grafana → service accounts → revoke + reissue). Never grant Editor scope to an MCP token. |

When in doubt: anything the dev compose does that requires `user: "0:0"`, `:ro` on a privileged socket, or a default password is a candidate for this table.

---

## 2. Operator onboarding

### What `docker compose --profile monitoring up -d` actually starts

| Container | Image | Role |
| --- | --- | --- |
| `postgres` | `postgres:17-alpine` | App DB. `logging_collector=on` writes slow queries to a shared volume the collector tails. |
| `valkey` | `valkey/valkey:8-alpine` | Cache. |
| `valkey-slowlog` | `valkey/valkey:8-alpine` | Sidecar that polls `SLOWLOG GET` every 30 s and writes JSON to a shared volume. |
| `snakk` | local build | All nine .NET services under one supervisord. |
| `redis-exporter` | `oliver006/redis_exporter:v1.67.0` | Prometheus exporter for Valkey/Redis. |
| `postgres-exporter` | `prometheuscommunity/postgres-exporter:v0.19.1` | Prometheus exporter for Postgres + the custom queries in `postgres-queries.yaml` (pg_stat_statements, lock waits, and a `pg_stat_checkpointer` query that surfaces the PG17 checkpoint counters the built-in collector no longer emits — feeds the Checkpoints panel). |
| `node-exporter` | `prom/node-exporter:v1.9.1` | Host CPU / memory / disk / network metrics. |
| `prometheus` | `prom/prometheus:v3.4.0` | Metrics storage. OTLP write-receiver enabled; exemplar storage enabled; remote_write receiver enabled. |
| `otel-collector` | `otel/opentelemetry-collector-contrib:0.115.1` | OTLP ingest, redaction, batching, fan-out to Tempo/Loki/Prometheus. Also runs the `docker_stats` receiver and the `filelog` receivers for Postgres / Valkey / Caddy. |
| `tempo` | `grafana/tempo:2.7.0` | Trace storage. |
| `loki` | `grafana/loki:3.4.1` | Log storage. |
| `pyroscope` | `grafana/pyroscope:2.0.2` | Continuous profiling. |
| `grafana` | `grafana/grafana:11.6.0` | UI. Provisioned datasources + 15 dashboards baked in. |

Optional extra profiles:

- `--profile loadtest` → adds the `k6` container for ad-hoc load testing.
- `--profile caddy` → containerised Caddy reverse proxy (off by default; the host-Caddy install is still the default in dev).

### One-curl-per-signal smoke test

Run each of these against a freshly-started stack and confirm a healthy response. Any failure narrows the diagnosis to a specific pipeline.

```bash
# 1. Stack is up.
docker compose ps

# 2. Metrics receiver alive (collector emitted target_info for itself).
curl -s 'http://127.0.0.1:9090/api/v1/query?query=target_info{service_name="otelcol-contrib"}' | jq '.data.result | length > 0'

# 3. App metrics flowing (services emit http_server_request_duration_seconds_count).
curl -s 'http://127.0.0.1:9090/api/v1/query?query=count(http_server_request_duration_seconds_count)' | jq -r '.data.result[0].value[1]'

# 4. Traces receiver alive (collector accepted at least one OTLP span).
curl -s 'http://127.0.0.1:9090/api/v1/query?query=otelcol_receiver_accepted_spans' | jq '.data.result | length > 0'

# 5. Logs flowing (Loki has at least one stream).
curl -s 'http://127.0.0.1:3100/loki/api/v1/labels' | jq '.data | length > 0'

# 6. Profiles flowing (Pyroscope responds and has the snakk apps).
curl -s 'http://127.0.0.1:4040/querier.v1.QuerierService/LabelValues' -H 'Content-Type: application/json' -d '{"name":"service_name"}' | jq '.names | length > 0'

# 7. Grafana reachable + datasources healthy.
curl -s -u admin:${GRAFANA_ADMIN_PASSWORD} 'http://127.0.0.1:3000/api/datasources' | jq -r '.[] | "\(.name)\t\(.uid)\t\(.type)"'
```

Or use the **Grafana MCP server** (`.mcp.json` in the repo root): the `list_datasources`, `query_prometheus`, `query_loki_logs`, `search_dashboards` tools cover the same surface programmatically. Faster than curl and the same source of truth as the dashboards.

### Switching backends (Grafana Cloud / Honeycomb / Datadog)

The collector receives OTLP from the .NET services on `otel-collector:4317`. Routing onward is exporter config. To point traces at Grafana Cloud Tempo and metrics at Mimir:

```yaml
# docker/monitoring/otel-collector-config.yaml
exporters:
  otlp/grafanacloud-tempo:
    endpoint: tempo-prod-04-prod-eu-west-2.grafana.net:443
    headers:
      authorization: "Basic ${env:GRAFANA_CLOUD_TEMPO_AUTH}"
  prometheusremotewrite/grafanacloud-mimir:
    endpoint: https://prometheus-prod-13-prod-eu-west-2.grafana.net/api/prom/push
    headers:
      authorization: "Basic ${env:GRAFANA_CLOUD_MIMIR_AUTH}"

service:
  pipelines:
    traces:
      exporters: [otlp/grafanacloud-tempo]  # replace otlp/tempo
    metrics:
      exporters: [prometheusremotewrite/grafanacloud-mimir]  # replace prometheusremotewrite
```

The `${env:...}` syntax reads `GRAFANA_CLOUD_TEMPO_AUTH` etc. from the `otel-collector` container's environment. Put the credentials in the operator's `.env` file, not in the YAML. The same pattern works for Honeycomb (`api.honeycomb.io:443` + `x-honeycomb-team` header) and Datadog (the collector has a first-class `datadog` exporter).

You do NOT need to change .NET code or rebuild containers — the SDK still emits to `http://otel-collector:4317` and the collector handles the rest.

### Rolling a fresh stack

When something gets into a wedged state, the fastest reset is:

```bash
docker compose --profile monitoring --profile loadtest down
docker compose --profile monitoring --profile loadtest up -d
```

`down` without `-v` keeps the named volumes (Prometheus / Tempo / Loki / Pyroscope / Postgres data, plus the shared log volumes). For a hard reset that also nukes data, see §5 below.

---

## 3. Common failure modes + fixes

### "I restarted the postgres-exporter / otel-collector and nothing happened"

`docker compose restart <service>` only restarts the existing container; it does NOT pick up changes to the compose file (new volume mounts, new env vars, new commands). After editing `docker-compose.yml`:

```bash
docker compose up -d --force-recreate <service>
```

Symptom: you added a volume mount or env var, the container restarted, but the path / variable isn't present inside. Cure: force-recreate.

### "filelog receiver shows 'permission denied'"

Cause: the file's UID/GID isn't readable by the collector. In dev we sidestep this with `user: "0:0"`; in prod do NOT keep that escape hatch. Three fixes (pick one):

- **Change the log source's mode.** Postgres uses `log_file_mode = 0644` in `postgresql.conf` for exactly this reason. The Caddy access-log line in the Caddyfile and the valkey-slowlog shipper script can be similarly relaxed.
- **Init container chowns at boot.** A short `chown -R <gid>:<gid> /var/log/...` job in an init container before the collector starts.
- **Shared GID across producer + collector.** Define an `observability` GID in the image, add the log-producing process and the collector to it, set the log file group to it.

The dev `user: "0:0"` is documented in `docker-compose.yml` with `*** PROD HARDENING REQUIRED ***` so it can't be silently shipped.

### "Postgres logging_collector stopped rotating after I deleted log files manually"

Postgres's logging_collector holds an open FD on the current log file. Deleting the file from outside Postgres leaves the FD pointing at an inode no one can read; new writes silently disappear. Recovery:

```bash
docker exec docker-postgres-1 psql -U snakk -d snakk -c "SELECT pg_rotate_logfile();"
```

Tells the logging_collector to close the old FD and open a new one. The collector's filelog receiver picks up the new file on its next scan.

**Don't delete logs from the host while Postgres is running.** Use SQL rotation, or stop Postgres first.

### "Pyroscope shows the old service name even though I renamed it"

Pyroscope caches service-name labels client-side. After renaming a service in `supervisord.conf` (e.g., `PYROSCOPE_APPLICATION_NAME` casing change), the dashboard variable may still show the old value until the cache ages out. Force refresh: hard-refresh the Grafana page, OR query `LabelValues` directly via the Pyroscope HTTP API:

```bash
curl -s http://127.0.0.1:4040/querier.v1.QuerierService/LabelValues \
  -H 'Content-Type: application/json' -d '{"name":"service_name"}'
```

### "Collector dropped spans" / "send_failed counter rising"

Open **Snakk OTel Pipeline Health** dashboard. The "Exporter send-failed rate (CRITICAL)" panel is the canonical signal. The two common causes:

1. **Downstream is down.** Check `docker compose ps` — if Tempo/Loki/Prometheus is restarting, the collector exporter retries get rejected. Fix the downstream.
2. **Queue backed up.** The "Exporter queue depth by signal" panel shows queue_size approaching queue_capacity. The downstream is too slow vs ingress. Mitigations: temporarily reduce sampling rate, OR scale the downstream, OR accept drop (the memory_limiter processor will eventually back-pressure ingest if it's safer to drop than OOM).

### "Trace ID shows in structured metadata but Grafana Explore doesn't link to Tempo"

Cause: derived-field config in `loki.yml` was using the line-body regex matcher (`matcherType` not set, default = "regex" against `__line__`). Our OTel SDK emits trace_id as Loki *structured metadata*, not in the log body, so that regex never matches. **Fix** (already applied in the current branch): use `matcherType: label` against the `trace_id` and `TraceId` field names. If a new log source still doesn't show the link, check that the structured metadata field is named exactly `trace_id` or `TraceId`.

### "Grafana variable dropdown is empty after I added a new dashboard"

Grafana provisions dashboards from the directory on container start. After dropping a new `.json` into `docker/monitoring/grafana/dashboards/`, run `docker compose restart grafana`. The new dashboard appears within a few seconds.

### "snakk-* metrics suddenly disappeared from Prometheus"

There are TWO HTTP-metrics pipelines (intentional during transition, per design §3.2):

- **prometheus-net legacy:** scraped from `snakk:17000/internal/metrics/*` by Prometheus. Emits `http_request_duration_seconds_*` with `service`/`code` labels.
- **OTel:** pushed via OTLP through the collector. Emits `http_server_request_duration_seconds_*` with `service_name`/`http_response_status_code`/`http_route` labels.

If one pipeline goes dark, the OTel Pipeline Health dashboard's "Spans / Metric points / Logs per sec" panels distinguish which path failed. Phase 6 retires the prometheus-net pipeline; until then, dashboards may use *either* metric set.

### ".NET runtime metrics (GC, heap) only appear under `dotnet_*` not `process_runtime_dotnet_*`"

The OTel `OpenTelemetry.Instrumentation.Runtime` package is wired in `Snakk.ServiceDefaults.ConfigureOpenTelemetry()` but does not currently emit metrics under the OTel-semconv names — only under the prometheus-net-style `dotnet_*` names. This blocks Phase 6 (prometheus-net retirement). Don't rewrite the Overview GC panels to OTel names yet; they'd go dark.

---

## 4. What NOT to do

### Don't add request bodies to spans

The `attributes/scrub` processor strips five header keys and the URL query string, but it cannot redact arbitrary request bodies. Adding `body` as a span attribute leaks anything POSTed: form fields, JSON payloads, file contents. Never do `activity?.SetTag("http.request.body", ...)`.

### Don't add user IDs (or any other unbounded identifier) as Prometheus labels

Cardinality death. A user-ID label on a per-request metric creates one new time series per active user; Prometheus's RAM and disk grow linearly, queries get slower, eventually scrapes timeout. User context belongs in **traces** (where each user-id is one attribute on one span, not a label dimension across millions of points) and in **logs** (structured metadata is per-line, not aggregated).

The current Snakk codebase doesn't do this. Don't be the first.

### Don't bypass the `attributes/scrub` processor

Every new instrumentation that adds a custom span MUST go through the processor pipeline. If you find yourself thinking "I'll just add a sibling exporter that goes direct to Tempo for this one debug case," stop. Either:

- Add the new attribute to the scrub allowlist if it's safe, OR
- Don't emit it.

The scrub list is the single source of truth for "what we ship outside this process."

### Don't run the dev compose in prod

The dev compose makes nine specific trade-offs that aren't safe outside a single developer's machine (see §1). The `--profile monitoring` flag is opt-in *because* the design assumes a separate prod overlay does the hardening work. Until that overlay exists (`docker-compose.production.yml` exists but doesn't yet override the dev compromises), don't `up -d` the dev compose anywhere that has a public IP or shared credentials.

### Don't `git add -A` near `.mcp.json`

`.mcp.json` is gitignored and contains the Grafana Viewer token. The gitignore entry is at the repo root. Confirm before committing.

### Don't delete named volumes assuming "the data isn't important"

`prometheus-data`, `loki-data`, `tempo-data`, `pyroscope-data`, `pgdata`, `postgres-logs`, `valkey-slowlog`, `caddy-logs`, `snakk-storage` all persist real state. The app data (`pgdata`, `snakk-storage`) is obviously load-bearing. The observability data (everything else) is what makes post-mortems possible. If you nuke `prometheus-data` mid-incident, you lose the metric history needed to write a post-mortem.

Hard reset is fine when it's intentional (see §5). Reflexive `docker compose down -v` because something looks off is not.

### Don't change `pg_stat_statements.max` upward without bounding the dashboard scrape

`postgres-queries.yaml` already `LIMIT 50`s the scrape — that keeps Prometheus cardinality bounded regardless of how many statements Postgres is tracking. If you remove the LIMIT or raise it, account for the cardinality math: 5000 statements × 8 metrics × hourly histogram buckets is a lot of series.

### Don't put secrets in `OTEL_RESOURCE_ATTRIBUTES`

Resource attributes are emitted on every signal (traces, metrics, logs). They're queryable and indexed. Anything you put here is forever in your observability backend. Use it for `deployment.environment`, `service.version`, `service.instance.id` — not for tokens, customer identifiers, or anything you might want to forget.

---

## 5. Retention reset procedure (hard reset of one backend)

Sometimes the right move is to nuke a specific observability volume without taking the app down. The pattern: stop the consumer of the volume, remove the volume, restart.

```bash
# Drop trace history (Tempo). Doesn't touch app DB or other backends.
docker compose stop tempo
docker volume rm docker_tempo-data
docker compose up -d tempo

# Drop log history (Loki).
docker compose stop loki
docker volume rm docker_loki-data
docker compose up -d loki

# Drop metric history (Prometheus). Includes every dashboard's source data.
docker compose stop prometheus
docker volume rm docker_prometheus-data
docker compose up -d prometheus

# Drop profile history (Pyroscope).
docker compose stop pyroscope
docker volume rm docker_pyroscope-data
docker compose up -d pyroscope
```

Each is independent. Do NOT pass `down -v` — that drops all volumes including `pgdata`.

The shared log-shipping volumes (`postgres-logs`, `valkey-slowlog`, `caddy-logs`) don't need separate resetting; they're tailed live and contain only the last few minutes of buffer.

---

## 6. Shedding telemetry load — the `Observability` feature flags

Each telemetry signal is an independent kill switch, bound from the
`Observability` config section and read once at process start by
`AddSnakkObservability` (`src/aspire/Snakk.ServiceDefaults/Extensions.cs`).
Use them to shed observability cost in prod under pressure (incident, capacity
crunch, small box) **without a code change** — flip an env var, restart the
service.

Every flag is env-overridable with the standard ASP.NET double-underscore
convention, e.g.:

```
Observability__Tracing__Enabled=false
Observability__Tracing__SamplingRatio=0.1
Observability__Profiling__Enabled=false
```

| Flag | Default | What turning it OFF does | Restart? |
| --- | --- | --- | --- |
| `Observability:Enabled` | `true` | Master switch. NO OTel pipeline (traces/metrics/logs) and no profiler are registered — instrumentation is *not wired*, so per-request Activity creation, the EF Core interceptor, and scope capture cost nothing. Health checks still work. | Yes |
| `Observability:Tracing:Enabled` | `true` | Skips the tracing pipeline entirely (no spans created or exported). | Yes |
| `Observability:Tracing:SamplingRatio` | `1.0` | Head-samples at the given probability in non-Dev environments via `ParentBased(TraceIdRatioBased(ratio))`, *before* the Collector's tail sampler. Lower it (e.g. `0.1`) to shed trace volume at the source. Dev always samples 100%. | Yes |
| `Observability:Metrics:Enabled` | `true` | Skips the metrics pipeline (ASP.NET/HttpClient/Runtime/Process/SignalR meters). | Yes |
| `Observability:OtlpLogs:Enabled` | `true` | Skips the OTel logging provider (`IncludeScopes`/`IncludeFormattedMessage` capture). Serilog console logging is unaffected. | Yes |
| `Observability:Profiling:Enabled` | `true` | Doesn't load the Pyroscope continuous profiler even if `PYROSCOPE_SERVER_ADDRESS` is set. **This is the single biggest per-process cost — opt it OUT in prod (`false`) until you actually need a profile.** | Yes |
| `Observability:Rum:Enabled` | `true` | `Snakk.Web` doesn't map `POST /bff/rum` AND `_Layout` omits the `web-vitals` `<script>`, so browsers register no observers and fire no beacon (rather than firing one to get a 404). | Yes |

**Defaults preserve prior behaviour exactly.** A service with no `Observability`
section behaves as it did before the flags existed (everything on, full
sampling). The flags are an *additional* gate on top of the existing env-var
gates — OTLP export still also requires `OTEL_EXPORTER_OTLP_ENDPOINT`, profiling
still also requires `PYROSCOPE_SERVER_ADDRESS`. A flag set to `false` wins.

**Recommended prod posture:** ship with everything on at a conservative
`SamplingRatio` (e.g. `0.1`) **except** `Profiling:Enabled=false` (opt-in). The
flags exist to shed load fast, not to run dark.

**Restart semantics:** OTel pipelines and the Pyroscope native agent read config
only at startup, so every flag takes effect on restart, not live. If live
tracing-volume control becomes necessary, the one worth the complexity is a
custom sampler reading `IOptionsMonitor` — everything else stays restart-gated.

**Aggregate cost (measured 2026-05-30, all signals on vs all off, dev box,
10k-VU browse):** ~8% throughput and 3–8× tail latency at high load, dominated
by 100%-sampled tracing + continuous profiling in the Dev config. The
per-signal A/B (rebase `experiment/observability-off`, k6 each flag
independently) is the open follow-up — drop the per-flag numbers into this
table as they land.

---

## 7. Cross-references

External docs we link to rather than restate:

- **OpenTelemetry Collector:** https://opentelemetry.io/docs/collector/
  - `attributes` processor: https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/attributesprocessor
  - `docker_stats` receiver: https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/receiver/dockerstatsreceiver
  - `filelog` receiver: https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/receiver/filelogreceiver
- **Tempo:** https://grafana.com/docs/tempo/latest/
- **Loki:** https://grafana.com/docs/loki/latest/
- **Pyroscope:** https://grafana.com/docs/pyroscope/latest/
- **node-exporter:** https://github.com/prometheus/node_exporter
- **postgres-exporter custom queries:** https://github.com/prometheus-community/postgres_exporter#extending-queries-with-extendqueriespath
- **pg_stat_statements:** https://www.postgresql.org/docs/17/pgstatstatements.html
- **k6 OTLP output:** https://k6.io/docs/results-output/real-time/opentelemetry/
- **Grafana derived fields (Loki):** https://grafana.com/docs/grafana/latest/datasources/loki/configure-loki-data-source/#derived-fields

Internal:

- `docs/OBSERVABILITY-DESIGN-2026-05-24.md` — locked decisions, threat model, pipeline diagram.
- `tests/k6/README.md` — load testing scenarios + how to interpret the k6 dashboard.
