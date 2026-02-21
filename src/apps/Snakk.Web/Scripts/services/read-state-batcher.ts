/**
 * Read State Batcher
 * Batches read state updates to reduce API calls
 * Flushes every 30 seconds or on page unload
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface ReadStateBatcherOptions {
    flushInterval?: number;
    storageKey?: string;
    maxAge?: number;
}

interface ReadStateUpdate {
    discussionId: string;
    postId: string;
    timestamp: number;
}

interface PendingUpdates {
    [discussionId: string]: ReadStateUpdate;
}

interface BatchRequest {
    updates: ReadStateUpdate[];
}


// ============================================================================
// Implementation
// ============================================================================

class ReadStateBatcher {
    private flushInterval: number;
    private storageKey: string;
    private maxAge: number;

    private flushTimer: ReturnType<typeof setInterval> | null = null;
    private pendingUpdates: PendingUpdates = {};
    private isAuthenticated: boolean = false;
    private idleCallbackId: number | null = null;

    private _beforeUnloadHandler: (() => void) | null = null;
    private _visibilityChangeHandler: (() => void) | null = null;

    constructor(options: ReadStateBatcherOptions = {}) {
        this.flushInterval = options.flushInterval || 30000; // 30 seconds
        this.storageKey = options.storageKey || 'snakk_pending_read_states';
        this.maxAge = options.maxAge || 5 * 60 * 1000; // 5 minutes
    }

    /**
     * Initialize the batcher
     */
    init(authenticated: boolean): void {
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
     */
    updateReadState(discussionId: string, postId: string): void {
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
     */
    async flush(): Promise<void> {
        if (!this.isAuthenticated || Object.keys(this.pendingUpdates).length === 0) {
            return;
        }

        const updates = { ...this.pendingUpdates };
        this.pendingUpdates = {}; // Clear immediately to prevent duplicates
        this.savePendingUpdates();

        try {
            // Send batch update to server
            const batch = Object.values(updates);
            const requestBody: BatchRequest = { updates: batch };

            const response = await fetch('/bff/read-states/batch', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestBody),
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
            const errorMessage = err instanceof Error ? err.message : 'Unknown error';
            console.error('[ReadStateBatcher] Error flushing read states:', err);
            // Restore updates on error
            Object.assign(this.pendingUpdates, updates);
            this.savePendingUpdates();
            this.dispatchEvent('flush-error', { error: errorMessage });
        }
    }

    /**
     * Load pending updates from localStorage
     */
    private loadPendingUpdates(): void {
        try {
            const stored = localStorage.getItem(this.storageKey);
            if (stored) {
                this.pendingUpdates = JSON.parse(stored) as PendingUpdates;

                // Prune very old updates
                const now = Date.now();
                Object.keys(this.pendingUpdates).forEach(discussionId => {
                    const update = this.pendingUpdates[discussionId];
                    if (update && now - update.timestamp > this.maxAge) {
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
    private savePendingUpdates(): void {
        try {
            localStorage.setItem(this.storageKey, JSON.stringify(this.pendingUpdates));
        } catch (e) {
            console.error('[ReadStateBatcher] Failed to save pending read states:', e);
        }
    }

    /**
     * Start the flush timer
     */
    private startFlushTimer(): void {
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
    private stopFlushTimer(): void {
        if (this.flushTimer) {
            clearInterval(this.flushTimer);
            this.flushTimer = null;
        }
    }

    /**
     * Handle beforeunload event
     * Use sendBeacon for guaranteed delivery
     */
    private handleBeforeUnload(): void {
        if (Object.keys(this.pendingUpdates).length === 0) return;

        const updates = Object.values(this.pendingUpdates);
        const requestBody: BatchRequest = { updates };
        const data = JSON.stringify(requestBody);

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
    private handleVisibilityChange(): void {
        if (document.hidden) {
            // Page is hidden, flush immediately
            this.flush();
        }
    }

    /**
     * Schedule idle flush using requestIdleCallback
     */
    private scheduleIdleFlush(): void {
        if (!('requestIdleCallback' in window) || !window.requestIdleCallback) return;

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
     */
    async forceFlush(): Promise<void> {
        return this.flush();
    }

    /**
     * Get count of pending updates
     */
    getPendingCount(): number {
        return Object.keys(this.pendingUpdates).length;
    }

    /**
     * Get all pending updates
     */
    getPendingUpdates(): PendingUpdates {
        return { ...this.pendingUpdates };
    }

    /**
     * Clear all pending updates (for testing)
     */
    clearPendingUpdates(): void {
        this.pendingUpdates = {};
        localStorage.removeItem(this.storageKey);
        this.dispatchEvent('cleared');
    }

    /**
     * Dispatch custom event
     */
    private dispatchEvent(eventName: string, detail: any = {}): void {
        document.dispatchEvent(new CustomEvent(`snakk:read-state-batcher:${eventName}`, { detail }));
    }

    /**
     * Shutdown the batcher (cleanup)
     */
    shutdown(): void {
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
        if (this.idleCallbackId !== null && 'cancelIdleCallback' in window && window.cancelIdleCallback) {
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
    destroy(): void {
        this.shutdown();
    }
}

// Export the class
(window as any).ReadStateBatcher = ReadStateBatcher;

// Export singleton instance for backward compatibility
(window as any).SnakkReadStateBatcher = new ReadStateBatcher();
