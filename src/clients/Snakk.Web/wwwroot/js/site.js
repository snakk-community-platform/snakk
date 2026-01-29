// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Snakk Hover Popup Component
(function() {
    const POPUP_DELAY = 300; // ms before showing popup
    const HIDE_DELAY = 200; // ms before hiding popup when mouse leaves

    let currentPopup = null;
    let showTimeout = null;
    let hideTimeout = null;
    let currentTrigger = null;
    let statsCache = new Map();

    // Create popup element
    function createPopupElement() {
        const popup = document.createElement('div');
        popup.className = 'snakk-popup';
        popup.innerHTML = `
            <div class="snakk-popup-content">
                <div class="snakk-popup-header">
                    <img class="snakk-popup-avatar" src="" alt="" />
                    <div class="snakk-popup-info">
                        <div class="snakk-popup-name"></div>
                        <div class="snakk-popup-type"></div>
                    </div>
                </div>
                <div class="snakk-popup-stats"></div>
                <div class="snakk-popup-loading">Loading...</div>
            </div>
        `;
        popup.style.display = 'none';
        document.body.appendChild(popup);

        // Keep popup visible when hovering over it
        popup.addEventListener('mouseenter', () => {
            clearTimeout(hideTimeout);
        });
        popup.addEventListener('mouseleave', () => {
            scheduleHide();
        });

        return popup;
    }

    // Get or create popup
    function getPopup() {
        if (!currentPopup) {
            currentPopup = createPopupElement();
        }
        return currentPopup;
    }

    // Get API base URL
    function getApiBaseUrl() {
        return window.snakkApiBaseUrl || 'https://localhost:7291';
    }

    // Get avatar URL based on entity type
    function getAvatarUrl(type, publicId) {
        const apiBase = getApiBaseUrl();
        // All entity types use .svg extension for CDN caching
        return `${apiBase}/avatars/${type}/${publicId}.svg`;
    }

    // Fetch stats for an entity
    async function fetchStats(type, publicId) {
        const cacheKey = `${type}:${publicId}`;
        if (statsCache.has(cacheKey)) {
            return statsCache.get(cacheKey);
        }

        const apiBase = getApiBaseUrl();
        let endpoint;
        switch (type) {
            case 'hub':
                endpoint = `${apiBase}/api/hubs/${publicId}/stats`;
                break;
            case 'space':
                endpoint = `${apiBase}/api/spaces/${publicId}/stats`;
                break;
            case 'community':
                endpoint = `${apiBase}/api/communities/${publicId}/stats`;
                break;
            case 'user':
                endpoint = `${apiBase}/api/users/${publicId}/stats`;
                break;
            case 'discussion':
                endpoint = `${apiBase}/api/discussions/${publicId}/stats`;
                break;
            default:
                return null;
        }

        try {
            const response = await fetch(endpoint, { credentials: 'include' });
            if (!response.ok) {
                return null;
            }
            const data = await response.json();
            statsCache.set(cacheKey, data);
            return data;
        } catch (err) {
            console.error('[Popup] Error fetching stats:', err);
            return null;
        }
    }

    // Format stat label
    function formatStatLabel(key) {
        const labels = {
            discussionCount: 'Discussions',
            replyCount: 'Replies',
            followerCount: 'Followers',
            spaceCount: 'Spaces',
            hubCount: 'Hubs',
            followingCount: 'Following'
        };
        return labels[key] || key;
    }

    // Get type display name
    function getTypeDisplayName(type) {
        const names = {
            hub: 'Hub',
            space: 'Space',
            community: 'Community',
            user: 'User',
            discussion: 'Discussion'
        };
        return names[type] || type;
    }

    // Build stats HTML based on entity type
    function buildStatsHtml(type, stats) {
        if (!stats) return '<div class="snakk-popup-error">Could not load stats</div>';

        const items = [];

        // Always show discussion count and reply count
        if (stats.discussionCount !== undefined) {
            items.push(`<div class="snakk-popup-stat"><span class="stat-label">Discussions</span><span class="stat-value">${stats.discussionCount}</span></div>`);
        }
        if (stats.replyCount !== undefined) {
            items.push(`<div class="snakk-popup-stat"><span class="stat-label">Replies</span><span class="stat-value">${stats.replyCount}</span></div>`);
        }

        // Follower count for discussions, spaces, users
        if (['discussion', 'space', 'user'].includes(type) && stats.followerCount !== undefined) {
            items.push(`<div class="snakk-popup-stat"><span class="stat-label">Followers</span><span class="stat-value">${stats.followerCount}</span></div>`);
        }

        // Following count for users
        if (type === 'user' && stats.followingCount !== undefined) {
            items.push(`<div class="snakk-popup-stat"><span class="stat-label">Following</span><span class="stat-value">${stats.followingCount}</span></div>`);
        }

        // Space count for hubs and communities
        if (['hub', 'community'].includes(type) && stats.spaceCount !== undefined) {
            items.push(`<div class="snakk-popup-stat"><span class="stat-label">Spaces</span><span class="stat-value">${stats.spaceCount}</span></div>`);
        }

        // Hub count for communities
        if (type === 'community' && stats.hubCount !== undefined) {
            items.push(`<div class="snakk-popup-stat"><span class="stat-label">Hubs</span><span class="stat-value">${stats.hubCount}</span></div>`);
        }

        return `<div class="snakk-popup-stats-list">${items.join('')}</div>`;
    }

    // Position popup near trigger element
    function positionPopup(popup, triggerEl) {
        const rect = triggerEl.getBoundingClientRect();
        const popupRect = popup.getBoundingClientRect();
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;

        // Default: position below and aligned to the left of the trigger
        let top = rect.bottom + scrollTop + 8;
        let left = rect.left + scrollLeft;

        // Check if popup would go off the right edge
        if (left + popupRect.width > window.innerWidth) {
            left = window.innerWidth - popupRect.width - 16;
        }

        // Check if popup would go off the bottom edge
        if (top + popupRect.height > scrollTop + window.innerHeight) {
            // Position above the trigger instead
            top = rect.top + scrollTop - popupRect.height - 8;
        }

        // Ensure left is not negative
        if (left < 8) left = 8;

        popup.style.top = `${top}px`;
        popup.style.left = `${left}px`;
    }

    // Show popup for a trigger element
    async function showPopup(triggerEl) {
        const type = triggerEl.dataset.popupType;
        const publicId = triggerEl.dataset.popupId;
        const name = triggerEl.dataset.popupName || triggerEl.textContent.trim();

        if (!type || !publicId) {
            return;
        }

        const popup = getPopup();

        // Set initial content
        popup.querySelector('.snakk-popup-avatar').src = getAvatarUrl(type, publicId);
        popup.querySelector('.snakk-popup-name').textContent = name;
        popup.querySelector('.snakk-popup-type').textContent = getTypeDisplayName(type);
        popup.querySelector('.snakk-popup-stats').innerHTML = '';
        popup.querySelector('.snakk-popup-loading').style.display = 'block';

        // Show popup
        popup.style.display = 'block';
        positionPopup(popup, triggerEl);

        // Fetch and display stats
        const stats = await fetchStats(type, publicId);
        popup.querySelector('.snakk-popup-loading').style.display = 'none';
        popup.querySelector('.snakk-popup-stats').innerHTML = buildStatsHtml(type, stats);

        // Reposition after content loads (size may have changed)
        positionPopup(popup, triggerEl);
    }

    // Hide popup
    function hidePopup() {
        const popup = getPopup();
        popup.style.display = 'none';
        currentTrigger = null;
    }

    // Schedule hide with delay
    function scheduleHide() {
        clearTimeout(hideTimeout);
        hideTimeout = setTimeout(() => {
            hidePopup();
        }, HIDE_DELAY);
    }

    // Handle mouse over on trigger elements (mouseover bubbles, mouseenter doesn't)
    function handleMouseOver(e) {
        const triggerEl = e.target.closest('[data-popup-type]');
        if (!triggerEl) {
            return;
        }

        // Skip if we're already tracking this trigger
        if (currentTrigger === triggerEl) {
            clearTimeout(hideTimeout);
            return;
        }

        clearTimeout(hideTimeout);
        clearTimeout(showTimeout);

        currentTrigger = triggerEl;
        showTimeout = setTimeout(() => {
            showPopup(triggerEl);
        }, POPUP_DELAY);
    }

    // Handle mouse out on trigger elements
    function handleMouseOut(e) {
        const triggerEl = e.target.closest('[data-popup-type]');
        if (!triggerEl) return;

        // Check if we're moving to a child element within the same trigger
        const relatedTarget = e.relatedTarget;
        if (relatedTarget && triggerEl.contains(relatedTarget)) {
            return; // Still within the trigger, don't hide
        }

        // Check if moving to the popup itself
        const popup = currentPopup;
        if (popup && relatedTarget && (popup === relatedTarget || popup.contains(relatedTarget))) {
            return; // Moving to popup, don't hide
        }

        clearTimeout(showTimeout);
        currentTrigger = null;
        scheduleHide();
    }

    // Initialize event delegation
    function init() {
        document.addEventListener('mouseover', handleMouseOver, false);
        document.addEventListener('mouseout', handleMouseOut, false);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
