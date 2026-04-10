#!/bin/bash
# capture-hang.sh — forensic snapshot during a Snakk hang event.
#
# Run this on the server *while* the frontpage is hanging. It captures
# everything we need to diagnose the root cause without restarting anything.
#
# Usage:
#   sudo bash capture-hang.sh
#
# Output goes to /tmp/snakk-hang-<timestamp>/

set -u

TS="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="/tmp/snakk-hang-${TS}"
mkdir -p "$OUT"

echo "=== Snakk hang capture: $TS ==="
echo "Output directory: $OUT"
echo

# ----- Container & host state -----
echo "[1/9] Container state..."
docker ps -a > "$OUT/01-docker-ps.txt" 2>&1
docker inspect snakk-snakk-1 --format='Started: {{.State.StartedAt}} | Restarts: {{.RestartCount}} | Health: {{.State.Health.Status}}' > "$OUT/02-container-status.txt" 2>&1 || true

echo "[2/9] Host resources..."
{
    echo "=== free -h ==="
    free -h
    echo
    echo "=== df -h ==="
    df -h
    echo
    echo "=== uptime ==="
    uptime
    echo
    echo "=== top -bn1 (top 20) ==="
    top -bn1 -o %CPU | head -27
} > "$OUT/03-host-resources.txt" 2>&1

echo "[3/9] Container resource usage..."
docker stats --no-stream > "$OUT/04-docker-stats.txt" 2>&1

# ----- Supervisord process state -----
echo "[4/9] Supervisord service state..."
docker compose exec -T snakk supervisorctl -c /etc/supervisor/conf.d/snakk.conf status > "$OUT/05-supervisord-status.txt" 2>&1 || true

# ----- PostgreSQL: what's it doing right now? -----
echo "[5/9] PostgreSQL active queries..."
docker compose exec -T postgres psql -U snakk -d snakk -c "
SELECT
    pid,
    age(clock_timestamp(), query_start) AS duration,
    state,
    wait_event_type,
    wait_event,
    LEFT(query, 200) AS query_snippet
FROM pg_stat_activity
WHERE state != 'idle'
  AND query NOT LIKE '%pg_stat_activity%'
ORDER BY query_start;
" > "$OUT/06-pg-active-queries.txt" 2>&1 || true

echo "[6/9] PostgreSQL connection counts..."
docker compose exec -T postgres psql -U snakk -d snakk -c "
SELECT count(*), state FROM pg_stat_activity GROUP BY state;
" > "$OUT/07-pg-connection-counts.txt" 2>&1 || true

echo "[7/9] PostgreSQL locks..."
docker compose exec -T postgres psql -U snakk -d snakk -c "
SELECT
    blocked.pid AS blocked_pid,
    blocking.pid AS blocking_pid,
    blocked.usename AS blocked_user,
    blocking.usename AS blocking_user,
    LEFT(blocked.query, 100) AS blocked_query,
    LEFT(blocking.query, 100) AS blocking_query
FROM pg_stat_activity blocked
JOIN pg_stat_activity blocking ON blocking.pid = ANY(pg_blocking_pids(blocked.pid))
WHERE blocked.state != 'idle';
" > "$OUT/08-pg-locks.txt" 2>&1 || true

# ----- Snakk logs -----
echo "[8/9] Snakk recent logs (last 2000 lines)..."
docker compose logs snakk --tail=2000 > "$OUT/09-snakk-logs.txt" 2>&1
docker compose logs snakk --tail=2000 2>&1 | grep -iE "error|exception|slow|timeout|deadline" > "$OUT/10-snakk-errors-only.txt"

echo "[9/9] Health check responses..."
{
    echo "=== Gateway health ==="
    curl -sk -m 5 -D - "http://localhost:17000/health" 2>&1 || echo "TIMEOUT"
    echo
    echo "=== Frontpage (timeout after 5s) ==="
    time curl -sk -m 5 -o /dev/null -w "HTTP %{http_code} in %{time_total}s\n" "http://localhost:17000/" 2>&1 || echo "FAILED"
    echo
    echo "=== /communities (timeout after 5s) ==="
    time curl -sk -m 5 -o /dev/null -w "HTTP %{http_code} in %{time_total}s\n" "http://localhost:17000/communities" 2>&1 || echo "FAILED"
} > "$OUT/11-health-checks.txt" 2>&1

# ----- Summary -----
{
    echo "=== Snakk hang capture summary ==="
    echo "Captured at: $TS"
    echo "Output: $OUT"
    echo
    echo "Files captured:"
    ls -la "$OUT"
} > "$OUT/00-README.txt"

echo
echo "============================================"
echo "Capture complete: $OUT"
echo "============================================"
echo
echo "Quick triage:"
echo
echo "--- Active PG queries ---"
cat "$OUT/06-pg-active-queries.txt" | head -20
echo
echo "--- Recent errors ---"
tail -20 "$OUT/10-snakk-errors-only.txt"
echo
echo "--- Health checks ---"
cat "$OUT/11-health-checks.txt"
echo
echo "Full capture in: $OUT"
echo "Tar it up to share: tar czf snakk-hang-${TS}.tar.gz -C /tmp snakk-hang-${TS}"
