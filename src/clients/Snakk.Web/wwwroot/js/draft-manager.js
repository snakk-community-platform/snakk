/**
 * Draft Manager
 * Auto-saves reply/post drafts to prevent data loss
 */
(function() {
    'use strict';

    const STORAGE_KEY = 'snakk_drafts';
    const AUTO_SAVE_INTERVAL = 5000; // 5 seconds

    let autoSaveTimer = null;
    let currentDraftKey = null;

    /**
     * Generate draft key for a discussion
     * @param {string} discussionId
     * @param {string|null} replyToPostId
     * @returns {string}
     */
    function getDraftKey(discussionId, replyToPostId = null) {
        return replyToPostId
            ? `${discussionId}:reply:${replyToPostId}`
            : `${discussionId}:post`;
    }

    /**
     * Get all drafts from storage
     * @returns {Object}
     */
    function getAllDrafts() {
        try {
            return JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
        } catch (e) {
            console.error('Failed to load drafts:', e);
            return {};
        }
    }

    /**
     * Save all drafts to storage
     * @param {Object} drafts
     */
    function saveAllDrafts(drafts) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(drafts));
        } catch (e) {
            console.error('Failed to save drafts:', e);
        }
    }

    /**
     * Get a draft for a specific discussion
     * @param {string} discussionId
     * @param {string|null} replyToPostId
     * @returns {Object|null}
     */
    function getDraft(discussionId, replyToPostId = null) {
        const drafts = getAllDrafts();
        const key = getDraftKey(discussionId, replyToPostId);
        const draft = drafts[key];

        if (!draft) return null;

        // Check if draft is too old (older than 7 days)
        const age = Date.now() - draft.savedAt;
        const maxAge = 7 * 24 * 60 * 60 * 1000;
        if (age > maxAge) {
            deleteDraft(discussionId, replyToPostId);
            return null;
        }

        return draft;
    }

    /**
     * Save a draft
     * @param {string} discussionId
     * @param {string} content
     * @param {string|null} replyToPostId
     */
    function saveDraft(discussionId, content, replyToPostId = null) {
        if (!content || content.trim().length === 0) {
            // Don't save empty drafts
            deleteDraft(discussionId, replyToPostId);
            return;
        }

        const drafts = getAllDrafts();
        const key = getDraftKey(discussionId, replyToPostId);

        drafts[key] = {
            content,
            discussionId,
            replyToPostId,
            savedAt: Date.now()
        };

        saveAllDrafts(drafts);
    }

    /**
     * Delete a draft
     * @param {string} discussionId
     * @param {string|null} replyToPostId
     */
    function deleteDraft(discussionId, replyToPostId = null) {
        const drafts = getAllDrafts();
        const key = getDraftKey(discussionId, replyToPostId);
        delete drafts[key];
        saveAllDrafts(drafts);
    }

    /**
     * Start auto-saving for a textarea
     * @param {string} discussionId
     * @param {HTMLTextAreaElement} textarea
     * @param {function} getReplyToPostId - Function that returns current replyToPostId
     */
    function startAutoSave(discussionId, textarea, getReplyToPostId) {
        // Stop any existing auto-save
        stopAutoSave();

        currentDraftKey = discussionId;

        // Set up auto-save timer
        autoSaveTimer = setInterval(() => {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            saveDraft(discussionId, content, replyToPostId);
        }, AUTO_SAVE_INTERVAL);

        // Also save on blur
        textarea.addEventListener('blur', function saveDraftOnBlur() {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            saveDraft(discussionId, content, replyToPostId);
        });

        // Save before page unload
        window.addEventListener('beforeunload', function saveDraftOnUnload() {
            const content = textarea.value;
            const replyToPostId = getReplyToPostId ? getReplyToPostId() : null;
            saveDraft(discussionId, content, replyToPostId);
        });
    }

    /**
     * Stop auto-saving
     */
    function stopAutoSave() {
        if (autoSaveTimer) {
            clearInterval(autoSaveTimer);
            autoSaveTimer = null;
        }
        currentDraftKey = null;
    }

    /**
     * Restore draft to textarea and show indicator
     * @param {string} discussionId
     * @param {HTMLTextAreaElement} textarea
     * @param {string|null} replyToPostId
     * @returns {boolean} true if draft was restored
     */
    function restoreDraft(discussionId, textarea, replyToPostId = null) {
        const draft = getDraft(discussionId, replyToPostId);
        if (!draft || !draft.content) return false;

        textarea.value = draft.content;

        // Trigger auto-grow if function exists
        if (typeof autoGrow === 'function') {
            autoGrow(textarea);
        }

        // Show "Draft restored" indicator
        showDraftRestoredIndicator();

        return true;
    }

    /**
     * Show draft restored indicator
     */
    function showDraftRestoredIndicator() {
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
     * @param {string} discussionId
     * @param {string|null} replyToPostId
     */
    function clearDraftOnSuccess(discussionId, replyToPostId = null) {
        deleteDraft(discussionId, replyToPostId);
        stopAutoSave();
    }

    /**
     * Get count of all saved drafts
     * @returns {number}
     */
    function getDraftCount() {
        const drafts = getAllDrafts();
        return Object.keys(drafts).length;
    }

    /**
     * Get all drafts with metadata
     * @returns {Array}
     */
    function getAllDraftsList() {
        const drafts = getAllDrafts();
        return Object.values(drafts).map(draft => ({
            ...draft,
            age: Date.now() - draft.savedAt,
            preview: draft.content.substring(0, 100) + (draft.content.length > 100 ? '...' : '')
        }));
    }

    /**
     * Clear all drafts
     */
    function clearAllDrafts() {
        localStorage.removeItem(STORAGE_KEY);
    }

    /**
     * Prune old drafts (older than 7 days)
     */
    function pruneOldDrafts() {
        const drafts = getAllDrafts();
        const maxAge = 7 * 24 * 60 * 60 * 1000;
        let changed = false;

        for (const [key, draft] of Object.entries(drafts)) {
            const age = Date.now() - draft.savedAt;
            if (age > maxAge) {
                delete drafts[key];
                changed = true;
            }
        }

        if (changed) {
            saveAllDrafts(drafts);
        }
    }

    // Prune old drafts on load
    pruneOldDrafts();

    // Export API
    window.SnakkDraftManager = {
        getDraft,
        saveDraft,
        deleteDraft,
        startAutoSave,
        stopAutoSave,
        restoreDraft,
        clearDraftOnSuccess,
        getDraftCount,
        getAllDraftsList,
        clearAllDrafts,
        pruneOldDrafts
    };
})();
