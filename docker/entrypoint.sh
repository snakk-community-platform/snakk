#!/bin/bash
set -e

echo "=== Snakk All-in-One Container ==="

STORAGE_PATH="/app/storage"
MARKER_FILE="$STORAGE_PATH/.setup-complete"

# Ensure storage directories exist
mkdir -p "$STORAGE_PATH/avatars/generated" "$STORAGE_PATH/avatars/uploaded"

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

# Start supervisord in the background
echo "Starting all services..."
/usr/bin/supervisord -c /etc/supervisor/conf.d/snakk.conf &
SUPERVISOR_PID=$!

# If setup is NOT complete, watch for the marker file and restart services when it appears
if [ "$SETUP_WAS_COMPLETE" = false ]; then
    echo ""
    echo "============================================"
    echo "  Setup not complete."
    echo "  Visit http://localhost:17000 to begin."
    echo "============================================"
    echo ""

    (
        while [ ! -f "$MARKER_FILE" ]; do
            sleep 2
        done

        echo ""
        echo "=== Setup complete! Restarting all services with new configuration... ==="
        sleep 3  # Let the wizard's HTTP response reach the browser

        supervisorctl restart all

        echo "=== All services restarted. Platform is live! ==="
    ) &
fi

# Wait for supervisord (keeps the container alive)
wait $SUPERVISOR_PID
