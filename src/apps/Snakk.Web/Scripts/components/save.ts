/**
 * Save / Bookmark Component
 * Handles save toggle for discussions and posts.
 * Uses data-action delegation so it works for dynamically loaded content (HTMX).
 */

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    interface SaveToggleResponse {
        isSaved: boolean;
    }

    function updateAllSaveButtons(discussionId: string, isSaved: boolean): void {
        document.querySelectorAll<HTMLElement>(`[data-action="toggle-save-discussion"][data-discussion-id="${CSS.escape(discussionId)}"]`)
            .forEach(b => setSaveState(b, isSaved));
    }

    async function toggleSaveDiscussion(btn: HTMLElement, discussionId: string): Promise<void> {
        const currentlySaved = btn.dataset.saved === 'true';

        // Optimistic update — sync all save buttons for this discussion
        updateAllSaveButtons(discussionId, !currentlySaved);

        const cache = (window as any).SnakkSaveCache;
        if (cache) cache.setDiscussionSaved(discussionId, !currentlySaved);

        try {
            const response = await fetch(`/bff/discussions/${discussionId}/save`, {
                method: 'POST',
                credentials: 'include'
            });

            if (!response.ok) {
                updateAllSaveButtons(discussionId, currentlySaved);
                if (cache) cache.setDiscussionSaved(discussionId, currentlySaved);
                return;
            }

            const result: SaveToggleResponse = await response.json();
            updateAllSaveButtons(discussionId, result.isSaved);
            if (cache) cache.setDiscussionSaved(discussionId, result.isSaved);
        } catch {
            updateAllSaveButtons(discussionId, currentlySaved);
            if (cache) cache.setDiscussionSaved(discussionId, currentlySaved);
        }
    }

    async function toggleSavePost(btn: HTMLElement, postId: string): Promise<void> {
        const currentlySaved = btn.dataset.saved === 'true';

        // Optimistic update
        setSaveState(btn, !currentlySaved);

        const cache = (window as any).SnakkSaveCache;
        if (cache) cache.setPostSaved(postId, !currentlySaved);

        try {
            const response = await fetch(`/bff/posts/${postId}/save`, {
                method: 'POST',
                credentials: 'include'
            });

            if (!response.ok) {
                setSaveState(btn, currentlySaved);
                if (cache) cache.setPostSaved(postId, currentlySaved);
                return;
            }

            const result: SaveToggleResponse = await response.json();
            setSaveState(btn, result.isSaved);
            if (cache) cache.setPostSaved(postId, result.isSaved);
        } catch {
            setSaveState(btn, currentlySaved);
            if (cache) cache.setPostSaved(postId, currentlySaved);
        }
    }

    function setSaveState(btn: HTMLElement, isSaved: boolean): void {
        btn.dataset.saved = isSaved ? 'true' : 'false';
        btn.setAttribute('aria-pressed', isSaved ? 'true' : 'false');
        btn.title = isSaved ? 'Unsave' : 'Save';
        btn.classList.toggle('is-saved', isSaved);
        btn.classList.toggle('btn-primary', isSaved);

        // Update visible text label if present (e.g. action pane "Save" button)
        const labelEl = btn.querySelector<HTMLElement>('span:not([aria-hidden])');
        if (labelEl) labelEl.textContent = isSaved ? 'Saved' : 'Save';
    }

    // Global event delegation for save actions
    document.addEventListener('click', async (e) => {
        const target = e.target as HTMLElement;
        const action = target.closest('[data-action]') as HTMLElement | null;
        if (!action) return;

        const actionName = action.dataset.action;

        if (actionName === 'toggle-save-discussion') {
            e.preventDefault();
            e.stopPropagation();
            const discussionId = action.dataset.discussionId || '';
            if (!discussionId) return;
            await toggleSaveDiscussion(action, discussionId);
        } else if (actionName === 'toggle-save-post') {
            e.preventDefault();
            e.stopPropagation();
            const postId = action.dataset.postId || '';
            if (!postId) return;
            await toggleSavePost(action, postId);
        }
    });

})();
