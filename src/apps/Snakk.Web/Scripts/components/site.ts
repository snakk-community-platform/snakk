/**
 * Snakk Hover Popup Component
 * Displays entity information on hover
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface SnakkPopupOptions {
    popupDelay?: number;
    hideDelay?: number;
}

interface EntityStats {
    publicId?: string;
    name?: string;
    displayName?: string;
    title?: string;
    avatarUrl?: string;
    discussionCount?: number;
    replyCount?: number;
    followerCount?: number;
    followingCount?: number;
    spaceCount?: number;
    hubCount?: number;
}

type EntityType = 'hub' | 'space' | 'community' | 'user' | 'discussion';

// ============================================================================
// Implementation
// ============================================================================

class SnakkPopup {
    private popupDelay: number;
    private hideDelay: number;

    private currentPopup: HTMLElement | null = null;
    private showTimeout: ReturnType<typeof setTimeout> | null = null;
    private hideTimeout: ReturnType<typeof setTimeout> | null = null;
    private currentTrigger: HTMLElement | null = null;
    private statsCache: Map<string, EntityStats | null> = new Map();

    private _mouseOverHandler: ((e: Event) => void) | null = null;
    private _mouseOutHandler: ((e: Event) => void) | null = null;

    constructor(options: SnakkPopupOptions = {}) {
        this.popupDelay = options.popupDelay || 300; // ms before showing popup
        this.hideDelay = options.hideDelay || 200; // ms before hiding popup when mouse leaves
    }

    /**
     * Get avatar URL based on entity type
     */
    getAvatarUrl(type: string, publicId: string): string {
        // All entity types use .svg extension for CDN caching
        return `/avatars/generated/${type}/${publicId}.svg`;
    }

    /**
     * Get type display name
     */
    getTypeDisplayName(type: string): string {
        const names: Record<string, string> = {
            hub: 'Hub',
            space: 'Space',
            community: 'Community',
            user: 'User',
            discussion: 'Discussion'
        };
        return names[type] || type;
    }

    /**
     * Create popup element
     */
    createPopupElement(): HTMLElement {
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
            if (this.hideTimeout) {
                clearTimeout(this.hideTimeout);
            }
        });
        popup.addEventListener('mouseleave', () => {
            this.scheduleHide();
        });

        return popup;
    }

    /**
     * Get or create popup
     */
    getPopup(): HTMLElement {
        if (!this.currentPopup) {
            this.currentPopup = this.createPopupElement();
        }
        return this.currentPopup;
    }

    /**
     * Fetch stats for an entity
     */
    async fetchStats(type: string, publicId: string): Promise<EntityStats | null> {
        const cacheKey = `${type}:${publicId}`;
        if (this.statsCache.has(cacheKey)) {
            return this.statsCache.get(cacheKey) || null;
        }

        let endpoint: string;
        switch (type) {
            case 'hub':
                endpoint = `/bff/hubs/${publicId}/stats`;
                break;
            case 'space':
                endpoint = `/bff/spaces/${publicId}/stats`;
                break;
            case 'community':
                endpoint = `/bff/communities/${publicId}/stats`;
                break;
            case 'user':
                endpoint = `/bff/users/${publicId}/stats-popup`;
                break;
            case 'discussion':
                endpoint = `/bff/discussions/${publicId}/stats`;
                break;
            default:
                return null;
        }

        try {
            const response = await fetch(endpoint, { credentials: 'include' });
            if (!response.ok) {
                return null;
            }
            const data = await response.json() as EntityStats;
            this.statsCache.set(cacheKey, data);
            return data;
        } catch (err) {
            console.error('[SnakkPopup] Error fetching stats:', err);
            return null;
        }
    }

    /**
     * Create a single stat element
     */
    createStatElement(label: string, value: number): HTMLElement {
        const stat = document.createElement('div');
        stat.className = 'snakk-popup-stat';

        const labelSpan = document.createElement('span');
        labelSpan.className = 'stat-label';
        labelSpan.textContent = label;

        const valueSpan = document.createElement('span');
        valueSpan.className = 'stat-value';
        valueSpan.textContent = value.toString();

        stat.appendChild(labelSpan);
        stat.appendChild(valueSpan);

        return stat;
    }

    /**
     * Build stats elements based on entity type
     */
    buildStatsElements(type: string, stats: EntityStats | null): DocumentFragment {
        const fragment = document.createDocumentFragment();

        if (!stats) {
            const error = document.createElement('div');
            error.className = 'snakk-popup-error';
            error.textContent = 'Could not load stats';
            fragment.appendChild(error);
            return fragment;
        }

        const container = document.createElement('div');
        container.className = 'snakk-popup-stats-list';

        // Always show discussion count and reply count
        if (stats.discussionCount !== undefined) {
            container.appendChild(this.createStatElement('Discussions', stats.discussionCount));
        }
        if (stats.replyCount !== undefined) {
            container.appendChild(this.createStatElement('Replies', stats.replyCount));
        }

        // Follower count for discussions, spaces, users
        if (['discussion', 'space', 'user'].includes(type) && stats.followerCount !== undefined) {
            container.appendChild(this.createStatElement('Followers', stats.followerCount));
        }

        // Following count for users
        if (type === 'user' && stats.followingCount !== undefined) {
            container.appendChild(this.createStatElement('Following', stats.followingCount));
        }

        // Space count for hubs and communities
        if (['hub', 'community'].includes(type) && stats.spaceCount !== undefined) {
            container.appendChild(this.createStatElement('Spaces', stats.spaceCount));
        }

        // Hub count for communities
        if (type === 'community' && stats.hubCount !== undefined) {
            container.appendChild(this.createStatElement('Hubs', stats.hubCount));
        }

        fragment.appendChild(container);
        return fragment;
    }

    /**
     * Position popup near trigger element
     */
    positionPopup(popup: HTMLElement, triggerEl: HTMLElement): void {
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

    /**
     * Show popup for a trigger element
     */
    async showPopup(triggerEl: HTMLElement): Promise<void> {
        const type = triggerEl.dataset.popupType;
        const publicId = triggerEl.dataset.popupId;
        const name = triggerEl.dataset.popupName || triggerEl.textContent?.trim() || '';

        if (!type || !publicId) {
            return;
        }

        const popup = this.getPopup();

        // Set initial content
        const avatarImg = popup.querySelector('.snakk-popup-avatar') as HTMLImageElement;
        const nameEl = popup.querySelector('.snakk-popup-name') as HTMLElement;
        const typeEl = popup.querySelector('.snakk-popup-type') as HTMLElement;
        const loadingEl = popup.querySelector('.snakk-popup-loading') as HTMLElement;
        const statsContainer = popup.querySelector('.snakk-popup-stats') as HTMLElement;

        // Set loading state
        if (avatarImg) avatarImg.src = this.getAvatarUrl(type, publicId); // Fallback while loading
        if (nameEl) nameEl.textContent = name;
        if (typeEl) typeEl.textContent = this.getTypeDisplayName(type);

        if (statsContainer) statsContainer.replaceChildren(); // Clear stats
        if (loadingEl) loadingEl.style.display = 'block';

        // Show popup
        popup.style.display = 'block';
        this.positionPopup(popup, triggerEl);

        // Fetch and display stats
        const stats = await this.fetchStats(type, publicId);
        if (loadingEl) loadingEl.style.display = 'none';

        // Update with data from API (includes correct avatarUrl with sharding)
        if (stats) {
            if (avatarImg && stats.avatarUrl) avatarImg.src = stats.avatarUrl;
            if (nameEl && (stats.name || stats.displayName || stats.title)) {
                nameEl.textContent = stats.name || stats.displayName || stats.title || name;
            }
        }

        const statsElements = this.buildStatsElements(type, stats);
        if (statsContainer) statsContainer.replaceChildren(statsElements);

        // Reposition after content loads (size may have changed)
        this.positionPopup(popup, triggerEl);
    }

    /**
     * Hide popup
     */
    hidePopup(): void {
        const popup = this.getPopup();
        popup.style.display = 'none';
        this.currentTrigger = null;
    }

    /**
     * Schedule hide with delay
     */
    scheduleHide(): void {
        if (this.hideTimeout) {
            clearTimeout(this.hideTimeout);
        }
        this.hideTimeout = setTimeout(() => {
            this.hidePopup();
        }, this.hideDelay);
    }

    /**
     * Handle mouse over on trigger elements (mouseover bubbles, mouseenter doesn't)
     */
    handleMouseOver(e: Event): void {
        const triggerEl = (e.target as HTMLElement).closest('[data-popup-type]') as HTMLElement | null;
        if (!triggerEl) {
            return;
        }

        // Skip breadcrumb current items
        if (triggerEl.classList.contains('breadcrumb-current')) {
            return;
        }

        // Skip if we're already tracking this trigger
        if (this.currentTrigger === triggerEl) {
            if (this.hideTimeout) {
                clearTimeout(this.hideTimeout);
            }
            return;
        }

        if (this.hideTimeout) clearTimeout(this.hideTimeout);
        if (this.showTimeout) clearTimeout(this.showTimeout);

        this.currentTrigger = triggerEl;
        this.showTimeout = setTimeout(() => {
            this.showPopup(triggerEl);
        }, this.popupDelay);
    }

    /**
     * Handle mouse out on trigger elements
     */
    handleMouseOut(e: Event): void {
        const triggerEl = (e.target as HTMLElement).closest('[data-popup-type]') as HTMLElement | null;
        if (!triggerEl) return;

        // Check if we're moving to a child element within the same trigger
        const mouseEvent = e as MouseEvent;
        const relatedTarget = mouseEvent.relatedTarget as Node | null;
        if (relatedTarget && triggerEl.contains(relatedTarget)) {
            return; // Still within the trigger, don't hide
        }

        // Check if moving to the popup itself
        const popup = this.currentPopup;
        if (popup && relatedTarget && (popup === relatedTarget || popup.contains(relatedTarget))) {
            return; // Moving to popup, don't hide
        }

        if (this.showTimeout) {
            clearTimeout(this.showTimeout);
        }
        this.currentTrigger = null;
        this.scheduleHide();
    }

    /**
     * Initialize event delegation
     */
    init(): void {
        this._mouseOverHandler = (e: Event) => this.handleMouseOver(e);
        this._mouseOutHandler = (e: Event) => this.handleMouseOut(e);

        document.addEventListener('mouseover', this._mouseOverHandler, false);
        document.addEventListener('mouseout', this._mouseOutHandler, false);
    }

    /**
     * Clear the stats cache
     */
    clearCache(): void {
        this.statsCache.clear();
    }

    /**
     * Destroy the popup component (cleanup)
     */
    destroy(): void {
        // Clear timeouts
        if (this.showTimeout) clearTimeout(this.showTimeout);
        if (this.hideTimeout) clearTimeout(this.hideTimeout);

        // Remove event listeners
        if (this._mouseOverHandler) {
            document.removeEventListener('mouseover', this._mouseOverHandler, false);
        }
        if (this._mouseOutHandler) {
            document.removeEventListener('mouseout', this._mouseOutHandler, false);
        }

        // Remove popup element
        if (this.currentPopup) {
            this.currentPopup.remove();
            this.currentPopup = null;
        }

        // Clear state
        this.currentTrigger = null;
        this.statsCache.clear();
        this._mouseOverHandler = null;
        this._mouseOutHandler = null;
    }
}

// Export the class
(window as any).SnakkPopup = SnakkPopup;

// Create and initialize singleton instance for backward compatibility
(window as any).SnakkPopupInstance = new SnakkPopup();

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        (window as any).SnakkPopupInstance.init();
    });
} else {
    (window as any).SnakkPopupInstance.init();
}
