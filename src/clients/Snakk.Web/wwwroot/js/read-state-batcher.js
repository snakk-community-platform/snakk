/**
 * Read State Batcher
 * Batches read state updates to reduce API calls
 * Flushes every 30 seconds or on page unload
 */
(function() {
    'use strict';

    const FLUSH_INTERVAL = 30000; // 30 seconds
    const STORAGE_KEY = 'snakk_pending_read_states';

    let flushTimer = null;
    let pendingUpdates = {};
    let isAuthenticated = false;

    /**
     * Initialize the batcher
     * @param {boolean} authenticated
     */
    function init(authenticated) {
        isAuthenticated = authenticated;
        if (!isAuthenticated) return;

        // Load any pending updates from storage (in case of crash)
        loadPendingUpdates();

        // Flush immediately if there are pending updates
        if (Object.keys(pendingUpdates).length > 0) {
            flush();
        }

        // Set up periodic flush
        startFlushTimer();

        // Flush on page unload
        window.addEventListener('beforeunload', handleBeforeUnload);

        // Flush on visibility change (tab/window hidden)
        document.addEventListener('visibilitychange', handleVisibilityChange);

        // Flush periodically when idle (fallback)
        if ('requestIdleCallback' in window) {
            scheduleIdleFlush();
        }
    }

    /**
     * Update read state for a discussion (batched)
     * @param {string} discussionId
     * @param {string} postId - Last read post ID
     */
    function updateReadState(discussionId, postId) {
        if (!isAuthenticated || !discussionId || !postId) return;

        // Buffer the update
        pendingUpdates[discussionId] = {
            discussionId,
            postId,
            timestamp: Date.now()
        };

        // Save to storage (in case of crash)
        savePendingUpdates();
    }

    /**
     * Flush all pending updates to server
     * @returns {Promise<void>}
     */
    async function flush() {
        if (!isAuthenticated || Object.keys(pendingUpdates).length === 0) {
            return;
        }

        const updates = { ...pendingUpdates };
        pendingUpdates = {}; // Clear immediately to prevent duplicates
        savePendingUpdates();

        try {
            // Send batch update to server
            const batch = Object.values(updates);

            const response = await fetch('/bff/read-states/batch', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ updates: batch }),
                credentials: 'include'
            });

            if (!response.ok) {
                console.error('Failed to flush read states:', response.status);
                // Restore updates on failure
                Object.assign(pendingUpdates, updates);
                savePendingUpdates();
            }
        } catch (err) {
            console.error('Error flushing read states:', err);
            // Restore updates on error
            Object.assign(pendingUpdates, updates);
            savePendingUpdates();
        }
    }

    /**
     * Load pending updates from localStorage
     */
    function loadPendingUpdates() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            if (stored) {
                pendingUpdates = JSON.parse(stored);

                // Prune very old updates (older than 5 minutes)
                const now = Date.now();
                const maxAge = 5 * 60 * 1000;
                Object.keys(pendingUpdates).forEach(discussionId => {
                    const update = pendingUpdates[discussionId];
                    if (now - update.timestamp > maxAge) {
                        delete pendingUpdates[discussionId];
                    }
                });
            }
        } catch (e) {
            console.error('Failed to load pending read states:', e);
            pendingUpdates = {};
        }
    }

    /**
     * Save pending updates to localStorage
     */
    function savePendingUpdates() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(pendingUpdates));
        } catch (e) {
            console.error('Failed to save pending read states:', e);
        }
    }

    /**
     * Start the flush timer
     */
    function startFlushTimer() {
        if (flushTimer) {
            clearInterval(flushTimer);
        }

        flushTimer = setInterval(() => {
            flush();
        }, FLUSH_INTERVAL);
    }

    /**
     * Stop the flush timer
     */
    function stopFlushTimer() {
        if (flushTimer) {
            clearInterval(flushTimer);
            flushTimer = null;
        }
    }

    /**
     * Handle beforeunload event
     * Use sendBeacon for guaranteed delivery
     */
    function handleBeforeUnload() {
        if (Object.keys(pendingUpdates).length === 0) return;

        const updates = Object.values(pendingUpdates);
        const data = JSON.stringify({ updates });

        // Try to use sendBeacon for guaranteed delivery
        if ('sendBeacon' in navigator) {
            const blob = new Blob([data], { type: 'application/json' });
            navigator.sendBeacon('/bff/read-states/batch', blob);
        } else {
            // Fallback: synchronous XHR (not recommended but necessary)
            try {
                const xhr = new XMLHttpRequest();
                xhr.open('POST', '/bff/read-states/batch', false); // synchronous
                xhr.setRequestHeader('Content-Type', 'application/json');
                xhr.withCredentials = true;
                xhr.send(data);
            } catch (e) {
                console.error('Failed to send read states on unload:', e);
            }
        }

        // Clear pending updates
        pendingUpdates = {};
        localStorage.removeItem(STORAGE_KEY);
    }

    /**
     * Handle visibility change (tab/window hidden)
     */
    function handleVisibilityChange() {
        if (document.hidden) {
            // Page is hidden, flush immediately
            flush();
        }
    }

    /**
     * Schedule idle flush using requestIdleCallback
     */
    function scheduleIdleFlush() {
        window.requestIdleCallback(() => {
            if (Object.keys(pendingUpdates).length > 0) {
                flush();
            }
            // Schedule next idle flush
            scheduleIdleFlush();
        }, { timeout: 60000 }); // 1 minute timeout
    }

    /**
     * Force immediate flush
     * @returns {Promise<void>}
     */
    async function forceFlush() {
        return flush();
    }

    /**
     * Get count of pending updates
     * @returns {number}
     */
    function getPendingCount() {
        return Object.keys(pendingUpdates).length;
    }

    /**
     * Get all pending updates
     * @returns {Object}
     */
    function getPendingUpdates() {
        return { ...pendingUpdates };
    }

    /**
     * Clear all pending updates (for testing)
     */
    function clearPendingUpdates() {
        pendingUpdates = {};
        localStorage.removeItem(STORAGE_KEY);
    }

    /**
     * Shutdown the batcher (cleanup)
     */
    function shutdown() {
        stopFlushTimer();
        flush(); // Final flush
        window.removeEventListener('beforeunload', handleBeforeUnload);
        document.removeEventListener('visibilitychange', handleVisibilityChange);
    }

    // Export API
    window.SnakkReadStateBatcher = {
        init,
        updateReadState,
        flush,
        forceFlush,
        getPendingCount,
        getPendingUpdates,
        clearPendingUpdates,
        shutdown
    };
})();
