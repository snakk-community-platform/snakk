# k6 load tests

OSS load testing against the Snakk dev stack. Tests run inside a `k6`
container on the docker network, target the gateway, and emit metrics to
the existing OTel Collector so test runs show up alongside app traces in
Grafana.

## Prereqs

The full observability stack is required for tests to be useful:

```bash
cd docker
docker compose --profile monitoring up -d
# (optional) start the containerised dev Caddy too:
docker compose --profile monitoring --profile caddy up -d
```

Seed demo data into the DB if you haven't:
```bash
# from repo root
dotnet run --project src/tools/Snakk.DbSeeder
```

## Running a scenario

All scenarios live in `scenarios/`. Each is independently runnable:

```bash
# smoke (1 VU, 30 s) — sanity check, run this first
docker compose --profile loadtest run --rm k6 run \
  -o experimental-opentelemetry /scripts/scenarios/smoke.js

# mixed workload (50 VUs default; tune with VUS + DURATION env vars)
docker compose --profile loadtest run --rm k6 run \
  -o experimental-opentelemetry /scripts/scenarios/mixed.js

# spike — baseline 20 → burst 200 → recovery
docker compose --profile loadtest run --rm k6 run \
  -o experimental-opentelemetry /scripts/scenarios/spike.js

# soak — long-running steady state (default 1 h, 30 VUs)
docker compose --profile loadtest run --rm k6 run \
  -e VUS=20 -e DURATION=30m \
  -o experimental-opentelemetry /scripts/scenarios/soak.js
```

Each VU's HTTP traffic carries a `traceparent` header, so requests show up
as root traces in Tempo and inline with app spans for that request.

## Scenarios

| Scenario                | Shape                                  | Purpose                                                   |
| ----------------------- | -------------------------------------- | --------------------------------------------------------- |
| **Basic**               |                                        |                                                           |
| `smoke.js`              | 1 VU, 30 s                             | "Does anything work?" — verifies auth + read + write end-to-end |
| `browse.js`             | Ramping VUs (default 50, 5 min)        | Anonymous reader pattern, drilling community → discussion |
| `search.js`             | Ramping VUs (default 30, 3 min)        | Hammers the three BFF search endpoints                    |
| `write.js`              | Ramping VUs (default 10, 2 min)        | Authenticated post creation; exercises form + gRPC + DB   |
| `mixed.js`              | 4 sub-scenarios at weighted concurrency | Older single-action sub-scenarios — use `journey.js` instead for new work |
| `spike.js`              | 20 → 200 → 20 VUs in 4 min             | Validates Polly + connection-pool sizing under burst      |
| `soak.js`               | Sustained moderate load (default 1 h)  | Memory-leak hunt + GC behaviour                           |
| **Extended**            |                                        |                                                           |
| `journey.js`            | 50 VUs / 10 min default                | Each VU runs a realistic multi-step journey (reader / searcher / auth-reader / writer / power-user, weighted 60/15/12/8/5). The default "what does real traffic look like" scenario |
| `capacity-200.js`       | 200 VUs / 15 min                       | "Normal day" baseline. Use to compare perf before/after work — strict thresholds. App success ≥ 99 %, p95 home < 1.5 s |
| `growth-500.js`         | 500 VUs / 10 min                       | "Front page of HN" surge. Looser thresholds; existence is the test |
| `burnup.js`             | 50 → 1000 VUs in 50-VU steps, 2 min each | Find the breaking point. `vus_max` reached with all thresholds green is the headline metric |
| `regression-perf.js`    | 20 VUs / 3 min                         | Targets the `docs/PERFORMANCE-AUDIT.md` hotspots: output-cache eligible pages (verifies Server-Timing cache.dur), discussion-detail, user profile. Track per-endpoint p95 across PRs |

## Where to look during a run

Open the **Snakk Traces** dashboard — RED panels show request rate / error
rate / p95 latency by service. Then **Snakk Profiles** with the `$service`
variable set to the most-loaded service to see flame graphs (CPU, alloc,
in-use). The **Snakk Infra** dashboard shows Caddy / Postgres / Valkey /
container resource pressure under load.

For trace pivots: click any slow span in Tempo → "Logs" or "Metrics" buttons
to jump to corresponding Loki entries or RED panel.

## Understanding the dashboard panels

The **Snakk k6 Load Tests** dashboard has *two* failure-rate panels, and the difference matters when the gateway-side `Connection: close + RST` bug is firing:

| Panel | Metric | Meaning |
| --- | --- | --- |
| **Application failure rate** | `k6_app_ok_*` (from `lib/metrics.js#checkOk`) | Real success signal: status 200-399, OR status 0 with a non-empty body containing the expected sentinel. **This is the number to watch.** |
| **Transport-level RST rate** | `k6_http_req_failed_occurred_total` | Share of requests k6 saw a wire-level error on. Stays near 100 % until the gateway bug is fixed; informational only. |

If Application failure rate is low but Transport-level RST rate is high, the platform is working fine — the gateway is closing connections rudely after sending the full response. Once that's fixed, both should drop to 0.

### Per-VU login cache

`lib/auth.js#login` caches the logged-in state per VU (module-level flag — k6 isolates module state per VU). Each VU logs in once total, regardless of how many iterations it runs. This keeps the gateway's `/auth/login` rate limit (20 POST / 5 min per IP) from blowing up during load tests. Use `forceRelogin(user)` if a scenario needs to deliberately re-exercise the login path.

If your scenario needs more than 20 distinct logins per 5-minute window (e.g. ramping past 20 VUs in under 5 minutes), set `DisableRateLimiting=true` on the snakk container in compose, or run scenarios with a slower ramp.

## Tuning

- `BASE_URL` — defaults to `http://snakk:17000` (the gateway via the Docker
  network). Set to `http://gateway:17030` to go through the dev Caddy.
- `VUS`, `DURATION` — most scenarios honour these env vars.
- `K6_OTEL_GRPC_EXPORTER_ENDPOINT` — pre-set in compose to point at the
  collector; override to send k6 metrics elsewhere.

## Cleanup after write-heavy runs

The `write.js` and `mixed.js` scenarios create real discussions in the DB.
Titles all start with `k6 ` so they're easy to find. To purge:

```bash
docker compose exec postgres psql -U snakk -d snakk -c \
  "DELETE FROM discussions WHERE title LIKE 'k6 %';"
```

(Cascades through the seeded post/reply tables. Check the schema if you
need to clean attachments etc.)

## Known limitations

- The auth flow uses Razor Pages form login with antiforgery — if the form
  HTML changes (token input name, etc.), `lib/auth.js` regex needs an
  update. The check inside `login()` will fail loudly when this happens.
- k6 doesn't render JavaScript, so SignalR / WebSocket flows aren't
  exercised. For realtime load testing, use [xk6-websockets](https://github.com/grafana/xk6-websockets)
  or the dedicated realtime client.
- k6's OTel output is `experimental-` as of v0.55 — flag may be renamed in
  future versions.
