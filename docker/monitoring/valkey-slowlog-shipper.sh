#!/bin/sh
# Valkey slow-log shipper. Polls SLOWLOG GET via a Lua EVAL that emits JSON
# (one entry per line), then SLOWLOG RESET to clear what we've seen.
# Output goes to /var/log/valkey-slowlog/slow.log; the OTel Collector's
# filelog receiver tails that file and ships to Loki.
#
# Why a Lua script? valkey-cli's text output is multi-line and brittle to
# parse in shell. Redis/Valkey ships cjson with the Lua runtime, so we can
# format JSON server-side in one round trip.

set -eu

LOG_DIR="/var/log/valkey-slowlog"
LOG_FILE="$LOG_DIR/slow.log"
INTERVAL="${POLL_INTERVAL:-30}"
HOST="${VALKEY_HOST:-valkey}"
PORT="${VALKEY_PORT:-6379}"

mkdir -p "$LOG_DIR"
touch "$LOG_FILE"

# Wait for valkey to be reachable.
until valkey-cli -h "$HOST" -p "$PORT" PING >/dev/null 2>&1; do
    sleep 1
done

LUA='
local entries = redis.call("SLOWLOG", "GET", 100)
local results = {}
for i, e in ipairs(entries) do
    local cmd = ""
    if type(e[4]) == "table" then
        for j, p in ipairs(e[4]) do
            if j > 1 then cmd = cmd .. " " end
            cmd = cmd .. tostring(p)
        end
    end
    local entry = {
        id = e[1],
        ts_unix = e[2],
        duration_us = e[3],
        command = cmd,
        client_addr = e[5] or "",
        client_name = e[6] or ""
    }
    table.insert(results, cjson.encode(entry))
end
redis.call("SLOWLOG", "RESET")
return results
'

while true; do
    # When stdout is not a tty, valkey-cli emits each array element as a raw
    # line without `N) "..."` framing. The Lua script already returns JSON
    # strings, so we append as-is.
    valkey-cli -h "$HOST" -p "$PORT" EVAL "$LUA" 0 2>/dev/null >> "$LOG_FILE"
    sleep "$INTERVAL"
done
