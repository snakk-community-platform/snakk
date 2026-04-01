#!/bin/bash
set -e

echo "=== Snakk All-in-One Container ==="

STORAGE_PATH="/app/storage"
CONF_DIR="$STORAGE_PATH/conf"
MARKER_FILE="$CONF_DIR/snakk-config.json"

# Ensure storage and runtime directories exist
mkdir -p "$STORAGE_PATH/avatars/generated" "$STORAGE_PATH/avatars/uploaded" "$CONF_DIR" /app/run

# If setup was previously completed, run DbSeeder for any pending migrations
if [ -f "$MARKER_FILE" ]; then
    echo "Setup already complete. Checking for pending migrations..."
    dotnet /app/dbseeder/Snakk.DbSeeder.dll --skip-seed || {
        echo "WARNING: DbSeeder failed. Continuing anyway."
    }
fi

# Track whether setup was already done before starting
SETUP_WAS_COMPLETE=false
if [ -f "$MARKER_FILE" ]; then
    SETUP_WAS_COMPLETE=true
fi

# Start supervisord (gateway, setup, realtime auto-start; others are deferred)
echo "Starting core services..."
/usr/bin/supervisord -c /etc/supervisor/conf.d/snakk.conf &
SUPERVISOR_PID=$!
sleep 2

CONF="/etc/supervisor/conf.d/snakk.conf"

if [ "$SETUP_WAS_COMPLETE" = true ]; then
    # Setup already done — start all app services, stop setup wizard
    echo "Starting application services..."
    supervisorctl -c "$CONF" stop setup 2>/dev/null || true
    supervisorctl -c "$CONF" start api web auth admin worker
    echo "=== All services running ==="
else
    echo ""
    echo "============================================"
    echo "  Setup not complete."
    echo "  Visit http://localhost:17000 to begin."
    echo "============================================"
    echo ""

    # Watch for the marker file and start services when setup completes
    (
        while [ ! -f "$MARKER_FILE" ]; do
            sleep 2
        done

        echo ""
        echo "=== Setup complete! Starting application services... ==="
        sleep 3  # Let the wizard's HTTP response reach the browser

        # Run migrations + seed
        dotnet /app/dbseeder/Snakk.DbSeeder.dll --skip-seed || echo "WARNING: DbSeeder failed"

        # Stop setup, start app services, restart gateway to pick up new routing
        supervisorctl -c "$CONF" stop setup
        supervisorctl -c "$CONF" start api web auth admin worker
        supervisorctl -c "$CONF" restart gateway

        echo "=== All services running. Platform is live! ==="
    ) &
fi

# Wait for supervisord (keeps the container alive)
wait $SUPERVISOR_PID
