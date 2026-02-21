/**
 * Draft Manager
 * Auto-saves reply/post drafts to prevent data loss
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface DraftManagerOptions {
    storageKey?: string;
    autoSaveInterval?: number;
    maxDraftAge?: number;
}

interface Draft {
    content: string;
    discussionId: string;
    replyToPostId: string | null;
    savedAt: number;
}

interface DraftsStorage {
    [key: string]: Draft;
}


// ============================================================================
// Implementation
// ============================================================================

class DraftManager {
    private storageKey: string;
    private autoSaveInterval: number;
    private maxDraftAge: number;

    private autoSaveTimer: ReturnType<typeof setInterval> | null = null;
    private _blurHandler: (() => void) | null = null;
    private _unloadHandler: (() => void) | null = null;
    private _textarea: HTMLTextAreaElement | null = null;

    constructor(options: DraftManagerOptions = {}) {
        this.storageKey = options.storageKey || 'snakk_drafts';
        this.autoSaveInterval = options.autoSaveInterval || 5000; // 5 seconds
        this.maxDraftAge = options.maxDraftAge || 7 * 24 * 60 * 60 * 1000; // 7 days

        // Prune old drafts on initialization
        this.pruneOldDrafts();
    }

    /**
     * Generate draft key for a discussion
     */
    getDraftKey(discussionId: string, replyToPostId: string | null = null): string {
        return replyToPostId
            ? `${discussionId}:reply:${replyToPostId}`
            : `${discussionId}:post`;
    }

    /**
     * Get all drafts from storage
     */
    getAllDrafts(): DraftsStorage {
        try {
            return JSON.parse(localStorage.getItem(this.storageKey) || '{}') as DraftsStorage;
        } catch (e) {
            console.error('Failed to load drafts:', e);
            return {};
        }
    }

    /**
     * Save all drafts to storage
     */
    saveAllDrafts(drafts: DraftsStorage): void {
        try {
            localStorage.setItem(this.storageKey, JSON.stringify(drafts));
        } catch (e) {
            console.error('Failed to save drafts:', e);
        }
    }

    /**
     * Get a draft for a specific discussion
     */
    getDraft(discussionId: string, replyToPostId: string | null = null): Draft | null {
        const drafts = this.getAllDrafts();
        const key = this.getDraftKey(discussionId, replyToPostId);
        const draft = drafts[key];

        if (!draft) return null;

        // Check if draft is too old
        const age = Date.now() - draft.savedAt;
        if (age > this.maxDraftAge) {
            this.deleteDraft(discussionId, replyToPostId);
            return null;
        }

        return draft;
    }

    /**
     * Save a draft
     */
    saveDraft(discussionId: string, content: string, replyToPostId: string | null = null): void {
        if (!content || content.trim().length === 0) {
            // Don't save empty drafts
            this.deleteDraft(discussionId, replyToPostId);
            return;
        }

        const drafts = this.getAllDrafts();
        const key = this.getDraftKey(discussionId, replyToPostId);

        drafts[key] = {
            content,
            discussionId,
            replyToPostId,
            savedAt: Date.now()
        };

        this.saveAllDrafts(drafts);

        // Dispatch custom event
        this.dispatchEvent('draft:saved', { key, content });
    }

    /**
     * Delete a draft
     */
    deleteDraft(discussionId: string, replyToPostId: string | null = null): void {
        const drafts = this.getAllDrafts();
        const key = this.getDraftKey(discussionId, replyToPostId);
        delete drafts[key];
        this.saveAllDrafts(drafts);

        // Dispatch custom event
        this.dispatchEvent('draft:deleted', { key });
    }

    /**
     * Start auto-saving for a textarea
     */
    startAutoSave(
        discussionId: string,
        textarea: HTMLTextAreaElement,
        getReplyToPostId?: () => string | null
    ): void {
        // Stop any existing auto-save
        this.stopAutoSave();

        // Set up auto-save timer
        this.autoSaveTimer = setInterval(() => {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            this.saveDraft(discussionId, content, replyToPostId);
        }, this.autoSaveInterval);

        // Also save on blur
        this._blurHandler = () => {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            this.saveDraft(discussionId, content, replyToPostId);
        };
        textarea.addEventListener('blur', this._blurHandler);

        // Save before page unload
        this._unloadHandler = () => {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            this.saveDraft(discussionId, content, replyToPostId);
        };
        window.addEventListener('beforeunload', this._unloadHandler);

        // Store textarea reference for cleanup
        this._textarea = textarea;
    }

    /**
     * Stop auto-saving
     */
    stopAutoSave(): void {
        if (this.autoSaveTimer) {
            clearInterval(this.autoSaveTimer);
            this.autoSaveTimer = null;
        }

        // Remove event listeners
        if (this._blurHandler && this._textarea) {
            this._textarea.removeEventListener('blur', this._blurHandler);
        }
        if (this._unloadHandler) {
            window.removeEventListener('beforeunload', this._unloadHandler);
        }

        this._blurHandler = null;
        this._unloadHandler = null;
        this._textarea = null;
    }

    /**
     * Restore draft to textarea and show indicator
     */
    restoreDraft(
        discussionId: string,
        textarea: HTMLTextAreaElement,
        replyToPostId: string | null = null
    ): boolean {
        const draft = this.getDraft(discussionId, replyToPostId);
        if (!draft || !draft.content) return false;

        textarea.value = draft.content;

        // Trigger auto-grow if function exists
        if (typeof (window as any).autoGrow === 'function') {
            (window as any).autoGrow(textarea);
        }

        // Show "Draft restored" indicator
        this.showDraftRestoredIndicator();

        // Dispatch custom event
        this.dispatchEvent('draft:restored', { discussionId, replyToPostId, content: draft.content });

        return true;
    }

    /**
     * Show draft restored indicator
     */
    showDraftRestoredIndicator(): void {
        const indicator = document.createElement('div');
        indicator.className = 'fixed top-20 left-1/2 transform -translate-x-1/2 bg-info/10 border border-info text-info px-4 py-2 rounded-lg shadow-lg z-50 flex items-center gap-2';
        indicator.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span class="text-sm font-medium">Draft restored</span>
        `;

        document.body.appendChild(indicator);

        // Remove after 3 seconds
        setTimeout(() => {
            indicator.style.opacity = '0';
            indicator.style.transition = 'opacity 0.3s';
            setTimeout(() => indicator.remove(), 300);
        }, 3000);
    }

    /**
     * Clear draft and stop auto-save (called on successful post)
     */
    clearDraftOnSuccess(discussionId: string, replyToPostId: string | null = null): void {
        this.deleteDraft(discussionId, replyToPostId);
        this.stopAutoSave();
    }

    /**
     * Get count of all saved drafts
     */
    getDraftCount(): number {
        const drafts = this.getAllDrafts();
        return Object.keys(drafts).length;
    }

    /**
     * Get all drafts with metadata
     */
    getAllDraftsList(): Array<Draft & { age: number; preview: string }> {
        const drafts = this.getAllDrafts();
        return Object.values(drafts).map(draft => ({
            ...draft,
            age: Date.now() - draft.savedAt,
            preview: draft.content.substring(0, 100) + (draft.content.length > 100 ? '...' : '')
        }));
    }

    /**
     * Clear all drafts
     */
    clearAllDrafts(): void {
        localStorage.removeItem(this.storageKey);
        this.dispatchEvent('draft:all-cleared');
    }

    /**
     * Prune old drafts (older than maxDraftAge)
     */
    pruneOldDrafts(): void {
        const drafts = this.getAllDrafts();
        let changed = false;

        for (const [key, draft] of Object.entries(drafts)) {
            const age = Date.now() - draft.savedAt;
            if (age > this.maxDraftAge) {
                delete drafts[key];
                changed = true;
            }
        }

        if (changed) {
            this.saveAllDrafts(drafts);
        }
    }

    /**
     * Dispatch custom event
     */
    private dispatchEvent(eventName: string, detail: any = {}): void {
        document.dispatchEvent(new CustomEvent(`snakk:${eventName}`, { detail }));
    }

    /**
     * Destroy the draft manager (cleanup)
     */
    destroy(): void {
        this.stopAutoSave();
    }
}

// Export the class
(window as any).DraftManager = DraftManager;

// Export singleton instance for backward compatibility
(window as any).SnakkDraftManager = new DraftManager();
