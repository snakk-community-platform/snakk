// Space Detail Page (follow buttons, sticky sidebar, discussion previews)
// Infinite scroll is now handled by HTMX (hx-trigger="revealed").

(function() {
    'use strict';

    let currentFollowLevel: string | null = null;
    let isFollowing = false;

    /**
     * Initialize space page functionality
     */
    function initSpacePage(): void {
        // Read config from <script type="application/json"> instead of window.SnakkConfig
        const configEl = document.getElementById('space-page-config');
        if (configEl) {
            try {
                (window as any).SnakkConfig = JSON.parse(configEl.textContent || '{}');
            } catch { /* ignore parse errors */ }
        }

        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!config || !spaceConfig) return;

        loadFollowStatus();
    }

    /**
     * Load user's follow status for this space
     */
    async function loadFollowStatus(): Promise<void> {
        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) return;
        const spaceId = spaceConfig.publicId;

        try {
            const response = await fetch(`/bff/spaces/${spaceId}/follow-status`, {
                credentials: 'include'
            });
            const result = await response.json();
            isFollowing = result.isFollowing;
            currentFollowLevel = result.level;
            updateFollowUI();
        } catch (err) {
            console.error('Error loading follow status:', err);
        }
    }

    /**
     * Toggle follow status for this space
     */
    async function toggleFollowSpace(): Promise<void> {
        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) return;
        const spaceId = spaceConfig.publicId;

        try {
            const level = currentFollowLevel || 'DiscussionsOnly';
            const response = await fetch(`/bff/spaces/${spaceId}/follow?level=${level}`, {
                method: 'POST',
                credentials: 'include'
            });

            if (!response.ok) {
                console.error('Failed to toggle follow');
                return;
            }

            const result = await response.json();
            isFollowing = result.isFollowing;
            if (isFollowing) {
                currentFollowLevel = result.level || 'DiscussionsOnly';
            }
            updateFollowUI();
        } catch (err) {
            console.error('Error toggling follow:', err);
        }
    }

    /**
     * Set the follow level (DiscussionsOnly or DiscussionsAndPosts)
     */
    async function setFollowLevel(level: string): Promise<void> {
        if (!isFollowing) return;

        const config = window.SnakkConfig;
        const spaceConfig = config?.space;
        if (!spaceConfig) return;
        const spaceId = spaceConfig.publicId;

        try {
            const response = await fetch(`/bff/spaces/${spaceId}/follow-level?level=${level}`, {
                method: 'PUT',
                credentials: 'include'
            });

            if (!response.ok) {
                console.error('Failed to update follow level');
                return;
            }

            const result = await response.json();
            currentFollowLevel = result.level;
            updateFollowUI();
        } catch (err) {
            console.error('Error updating follow level:', err);
        }
    }

    /**
     * Update the follow button UI based on current state
     */
    function updateFollowUI(): void {
        const rpBtn = document.querySelector<HTMLElement>('.rp-subscribe-btn');
        if (rpBtn) {
            const rpText = rpBtn.querySelector('span');
            if (isFollowing) {
                rpBtn.classList.add('btn-primary');
                rpBtn.classList.remove('btn-ghost');
                if (rpText) rpText.textContent = 'Followed';
            } else {
                rpBtn.classList.remove('btn-primary');
                rpBtn.classList.add('btn-ghost');
                if (rpText) rpText.textContent = 'Follow';
            }
        }

        const toggleBtn = document.getElementById('follow-toggle-btn');
        const followText = document.getElementById('follow-text');
        const followIcon = document.getElementById('follow-icon');
        const levelToggle = document.getElementById('level-toggle');
        const discussionsBtn = document.getElementById('level-discussions-btn');
        const postsBtn = document.getElementById('level-posts-btn');

        if (!toggleBtn || !followText || !followIcon || !levelToggle || !discussionsBtn || !postsBtn) return;

        if (isFollowing) {
            toggleBtn.classList.add('btn-primary');
            toggleBtn.classList.remove('btn-ghost');
            toggleBtn.classList.remove('rounded-r-none', 'border-r-0');
            toggleBtn.classList.add('rounded-l-lg', 'rounded-r-none');
            followText.textContent = 'Followed';
            followIcon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />';

            levelToggle.classList.remove('hidden');

            if (currentFollowLevel === 'DiscussionsAndPosts') {
                discussionsBtn.classList.remove('btn-primary');
                discussionsBtn.classList.add('btn-ghost');
                postsBtn.classList.add('btn-primary');
                postsBtn.classList.remove('btn-ghost');
            } else {
                discussionsBtn.classList.add('btn-primary');
                discussionsBtn.classList.remove('btn-ghost');
                postsBtn.classList.remove('btn-primary');
                postsBtn.classList.add('btn-ghost');
            }
        } else {
            toggleBtn.classList.remove('btn-primary');
            toggleBtn.classList.add('btn-ghost');
            toggleBtn.classList.add('rounded-lg');
            toggleBtn.classList.remove('rounded-l-lg', 'rounded-r-none');
            followText.textContent = 'Follow';
            followIcon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />';

            levelToggle.classList.add('hidden');
        }
    }

    // ===== Event Delegation for Follow Actions =====
    document.addEventListener('click', async (e: MouseEvent) => {
        const target = e.target as HTMLElement | null;
        if (!target) return;

        const action = target.closest('[data-action]') as HTMLElement | null;
        if (!action || !action.dataset.action) return;

        const actionName = action.dataset.action;

        switch (actionName) {
            case 'toggle-follow-space':
                e.preventDefault();
                await toggleFollowSpace();
                break;

            case 'set-follow-level':
                e.preventDefault();
                if (action.dataset.level) {
                    await setFollowLevel(action.dataset.level);
                }
                break;
        }
    });

    // Track if page has been initialized to prevent duplicate calls
    let isSpacePageInitialized = false;

    // Run on initial page load
    document.addEventListener('DOMContentLoaded', function() {
        if (!isSpacePageInitialized) {
            isSpacePageInitialized = true;
            initSpacePage();
        }
    });

    // Run after HTMX content swap (for SPA-like navigation)
    document.body.addEventListener('htmx:load', function() {
        if (document.getElementById('discussions-container') && !isSpacePageInitialized) {
            isSpacePageInitialized = true;
            initSpacePage();
        }
    });
})();
