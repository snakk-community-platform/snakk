/**
 * Read State Batcher
 * Batches read state updates to reduce API calls
 * Flushes every 30 seconds or on page unload
 */
class ReadStateBatcher {
    constructor(options = {}) {
        this.flushInterval = options.flushInterval || 30000; // 30 seconds
        this.storageKey = options.storageKey || 'snakk_pending_read_states';
        this.maxAge = options.maxAge || 5 * 60 * 1000; // 5 minutes

        this.flushTimer = null;
        this.pendingUpdates = {};
        this.isAuthenticated = false;
        this.idleCallbackId = null;

        this._beforeUnloadHandler = null;
        this._visibilityChangeHandler = null;
    }

    /**
     * Initialize the batcher
     * @param {boolean} authenticated
     */
    init(authenticated) {
        this.isAuthenticated = authenticated;
        if (!this.isAuthenticated) return;

        // Load any pending updates from storage (in case of crash)
        this.loadPendingUpdates();

        // Flush immediately if there are pending updates
        if (Object.keys(this.pendingUpdates).length > 0) {
            this.flush();
        }

        // Set up periodic flush
        this.startFlushTimer();

        // Set up event handlers
        this._beforeUnloadHandler = () => this.handleBeforeUnload();
        this._visibilityChangeHandler = () => this.handleVisibilityChange();

        // Flush on page unload
        window.addEventListener('beforeunload', this._beforeUnloadHandler);

        // Flush on visibility change (tab/window hidden)
        document.addEventListener('visibilitychange', this._visibilityChangeHandler);

        // Flush periodically when idle (fallback)
        if ('requestIdleCallback' in window) {
            this.scheduleIdleFlush();
        }

        // Dispatch init event
        this.dispatchEvent('init', { authenticated });
    }

    /**
     * Update read state for a discussion (batched)
     * @param {string} discussionId
     * @param {string} postId - Last read post ID
     */
    updateReadState(discussionId, postId) {
        if (!this.isAuthenticated || !discussionId || !postId) return;

        // Buffer the update
        this.pendingUpdates[discussionId] = {
            discussionId,
            postId,
            timestamp: Date.now()
        };

        // Save to storage (in case of crash)
        this.savePendingUpdates();

        // Dispatch update event
        this.dispatchEvent('update', { discussionId, postId });
    }

    /**
     * Flush all pending updates to server
     * @returns {Promise<void>}
     */
    async flush() {
        if (!this.isAuthenticated || Object.keys(this.pendingUpdates).length === 0) {
            return;
        }

        const updates = { ...this.pendingUpdates };
        this.pendingUpdates = {}; // Clear immediately to prevent duplicates
        this.savePendingUpdates();

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
                console.error('[ReadStateBatcher] Failed to flush read states:', response.status);
                // Restore updates on failure
                Object.assign(this.pendingUpdates, updates);
                this.savePendingUpdates();
                this.dispatchEvent('flush-error', { status: response.status, batch });
            } else {
                this.dispatchEvent('flush-success', { count: batch.length });
            }
        } catch (err) {
            console.error('[ReadStateBatcher] Error flushing read states:', err);
            // Restore updates on error
            Object.assign(this.pendingUpdates, updates);
            this.savePendingUpdates();
            this.dispatchEvent('flush-error', { error: err.message });
        }
    }

    /**
     * Load pending updates from localStorage
     */
    loadPendingUpdates() {
        try {
            const stored = localStorage.getItem(this.storageKey);
            if (stored) {
                this.pendingUpdates = JSON.parse(stored);

                // Prune very old updates
                const now = Date.now();
                Object.keys(this.pendingUpdates).forEach(discussionId => {
                    const update = this.pendingUpdates[discussionId];
                    if (now - update.timestamp > this.maxAge) {
                        delete this.pendingUpdates[discussionId];
                    }
                });
            }
        } catch (e) {
            console.error('[ReadStateBatcher] Failed to load pending read states:', e);
            this.pendingUpdates = {};
        }
    }

    /**
     * Save pending updates to localStorage
     */
    savePendingUpdates() {
        try {
            localStorage.setItem(this.storageKey, JSON.stringify(this.pendingUpdates));
        } catch (e) {
            console.error('[ReadStateBatcher] Failed to save pending read states:', e);
        }
    }

    /**
     * Start the flush timer
     */
    startFlushTimer() {
        if (this.flushTimer) {
            clearInterval(this.flushTimer);
        }

        this.flushTimer = setInterval(() => {
            this.flush();
        }, this.flushInterval);
    }

    /**
     * Stop the flush timer
     */
    stopFlushTimer() {
        if (this.flushTimer) {
            clearInterval(this.flushTimer);
            this.flushTimer = null;
        }
    }

    /**
     * Handle beforeunload event
     * Use sendBeacon for guaranteed delivery
     */
    handleBeforeUnload() {
        if (Object.keys(this.pendingUpdates).length === 0) return;

        const updates = Object.values(this.pendingUpdates);
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
                console.error('[ReadStateBatcher] Failed to send read states on unload:', e);
            }
        }

        // Clear pending updates
        this.pendingUpdates = {};
        localStorage.removeItem(this.storageKey);
    }

    /**
     * Handle visibility change (tab/window hidden)
     */
    handleVisibilityChange() {
        if (document.hidden) {
            // Page is hidden, flush immediately
            this.flush();
        }
    }

    /**
     * Schedule idle flush using requestIdleCallback
     */
    scheduleIdleFlush() {
        if (!('requestIdleCallback' in window)) return;

        this.idleCallbackId = window.requestIdleCallback(() => {
            if (Object.keys(this.pendingUpdates).length > 0) {
                this.flush();
            }
            // Schedule next idle flush
            this.scheduleIdleFlush();
        }, { timeout: 60000 }); // 1 minute timeout
    }

    /**
     * Force immediate flush
     * @returns {Promise<void>}
     */
    async forceFlush() {
        return this.flush();
    }

    /**
     * Get count of pending updates
     * @returns {number}
     */
    getPendingCount() {
        return Object.keys(this.pendingUpdates).length;
    }

    /**
     * Get all pending updates
     * @returns {Object}
     */
    getPendingUpdates() {
        return { ...this.pendingUpdates };
    }

    /**
     * Clear all pending updates (for testing)
     */
    clearPendingUpdates() {
        this.pendingUpdates = {};
        localStorage.removeItem(this.storageKey);
        this.dispatchEvent('cleared');
    }

    /**
     * Dispatch custom event
     * @param {string} eventName
     * @param {Object} detail
     */
    dispatchEvent(eventName, detail = {}) {
        document.dispatchEvent(new CustomEvent(`snakk:read-state-batcher:${eventName}`, { detail }));
    }

    /**
     * Shutdown the batcher (cleanup)
     */
    shutdown() {
        this.stopFlushTimer();
        this.flush(); // Final flush

        // Remove event listeners
        if (this._beforeUnloadHandler) {
            window.removeEventListener('beforeunload', this._beforeUnloadHandler);
        }
        if (this._visibilityChangeHandler) {
            document.removeEventListener('visibilitychange', this._visibilityChangeHandler);
        }

        // Cancel idle callback
        if (this.idleCallbackId && 'cancelIdleCallback' in window) {
            window.cancelIdleCallback(this.idleCallbackId);
        }

        this._beforeUnloadHandler = null;
        this._visibilityChangeHandler = null;
        this.idleCallbackId = null;

        this.dispatchEvent('shutdown');
    }

    /**
     * Destroy the batcher (alias for shutdown)
     */
    destroy() {
        this.shutdown();
    }
}

// Export the class
window.ReadStateBatcher = ReadStateBatcher;

// Export singleton instance for backward compatibility
window.SnakkReadStateBatcher = new ReadStateBatcher();
