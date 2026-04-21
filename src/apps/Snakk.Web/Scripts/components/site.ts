    /**
 * Snakk Hover Popup Component
 * Displays entity information on hover
 */

(function(): void {
    'use strict';

    // Guard: skip re-execution during HTMX SPA navigation
    if ((window as any).SnakkPopup) return;

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
    description?: string;
    bio?: string;
    discussionCount?: number;
    replyCount?: number;
    followerCount?: number;
    followingCount?: number;
    spaceCount?: number;
    hubCount?: number;
    gradientCss?: string;
}

interface EntityResolveResult {
    type: string;
    publicId: string;
    name: string;
}

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
    private resolveCache: Map<string, EntityResolveResult | null> = new Map();

    private _mouseOverHandler: ((e: Event) => void) | null = null;
    private _mouseOutHandler: ((e: Event) => void) | null = null;

    constructor(options: SnakkPopupOptions = {}) {
        this.popupDelay = options.popupDelay || 300; // ms before showing popup
        this.hideDelay = options.hideDelay || 200; // ms before hiding popup when mouse leaves
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
            <div class="card card-xs snakk-popup-inner">
                <figure class="snakk-popup-banner">
                    <div class="snakk-popup-banner-info">
                        <div class="snakk-popup-name"></div>
                        <div class="snakk-popup-type"></div>
                    </div>
                </figure>
                <div class="card-body snakk-popup-body">
                    <div class="snakk-popup-description"></div>
                    <div class="snakk-popup-stats"></div>
                    <div class="snakk-popup-stats-skeleton snakk-popup-stats-grid">
                        <div class="stat"><div class="skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="skeleton" style="height:.875rem;width:1.75rem"></div></div>
                        <div class="stat"><div class="skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="skeleton" style="height:.875rem;width:1.75rem"></div></div>
                        <div class="stat"><div class="skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="skeleton" style="height:.875rem;width:1.75rem"></div></div>
                        <div class="stat"><div class="skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="skeleton" style="height:.875rem;width:1.75rem"></div></div>
                    </div>
                </div>
            </div>
            <div class="snakk-popup-avatar-skeleton skeleton"></div>
            <img class="snakk-popup-avatar" src="" alt="" style="display:none" />
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
     * Resolve an internal entity path to type, publicId, and name.
     * Used for .entity-link elements that don't have data-popup-* attributes.
     */
    async resolveEntityPath(path: string): Promise<EntityResolveResult | null> {
        if (this.resolveCache.has(path)) {
            return this.resolveCache.get(path) || null;
        }

        try {
            const response = await fetch(`/bff/entity/resolve?path=${encodeURIComponent(path)}`, {
                credentials: 'include',
            });
            if (!response.ok) {
                this.resolveCache.set(path, null);
                return null;
            }
            const data = await response.json() as EntityResolveResult;
            this.resolveCache.set(path, data);
            return data;
        } catch (err) {
            console.error('[SnakkPopup] Error resolving entity path:', err);
            this.resolveCache.set(path, null);
            return null;
        }
    }

    formatCount(n: number): string {
        if (n >= 1_000_000) return (n / 1_000_000).toFixed(1).replace(/\.0$/, '') + 'M';
        if (n >= 1_000) return (n / 1_000).toFixed(1).replace(/\.0$/, '') + 'K';
        return n.toString();
    }

    createStatElement(label: string, value: number): HTMLElement {
        const stat = document.createElement('div');
        stat.className = 'stat';

        const titleEl = document.createElement('div');
        titleEl.className = 'stat-title';
        titleEl.textContent = label;

        const valueEl = document.createElement('div');
        valueEl.className = 'stat-value';
        valueEl.textContent = this.formatCount(value);

        stat.appendChild(titleEl);
        stat.appendChild(valueEl);

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
        container.className = 'snakk-popup-stats-grid';

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
        let top = rect.bottom + scrollTop + 10;
        let left = rect.left + scrollLeft;

        // Check if popup would go off the right edge
        if (left + popupRect.width > window.innerWidth) {
            left = window.innerWidth - popupRect.width - 16;
        }

        // Check if popup would go off the bottom edge
        if (top + popupRect.height > scrollTop + window.innerHeight) {
            // Position above the trigger instead
            top = rect.top + scrollTop - popupRect.height - 10;
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
        const avatarSkeleton = popup.querySelector('.snakk-popup-avatar-skeleton') as HTMLElement;
        const avatarImg = popup.querySelector('.snakk-popup-avatar') as HTMLImageElement;
        const nameEl = popup.querySelector('.snakk-popup-name') as HTMLElement;
        const typeEl = popup.querySelector('.snakk-popup-type') as HTMLElement;
        const descriptionEl = popup.querySelector('.snakk-popup-description') as HTMLElement;
        const statsSkeleton = popup.querySelector('.snakk-popup-stats-skeleton') as HTMLElement;
        const statsContainer = popup.querySelector('.snakk-popup-stats') as HTMLElement;

        // Set loading state — show skeletons, hide real content
        if (avatarSkeleton) avatarSkeleton.style.display = 'block';
        if (avatarImg) avatarImg.style.display = 'none';
        if (nameEl) nameEl.textContent = name;
        if (typeEl) typeEl.textContent = this.getTypeDisplayName(type);
        if (descriptionEl) descriptionEl.textContent = '';
        if (statsContainer) statsContainer.replaceChildren();
        if (statsSkeleton) statsSkeleton.style.display = 'block';
        const bannerElLoading = popup.querySelector('.snakk-popup-banner') as HTMLElement;
        if (bannerElLoading) bannerElLoading.style.removeProperty('background');

        // Show popup
        popup.style.display = 'block';
        this.positionPopup(popup, triggerEl);

        // Fetch and display stats
        const stats = await this.fetchStats(type, publicId);

        // Guard: if popup was hidden while awaiting (e.g. navigation), bail out
        if (this.currentTrigger !== triggerEl) return;

        // Hide skeletons
        if (statsSkeleton) statsSkeleton.style.display = 'none';

        // Update avatar from API response (includes correct sharded URL)
        if (avatarSkeleton) avatarSkeleton.style.display = 'none';
        if (stats && stats.avatarUrl && avatarImg) {
            avatarImg.src = stats.avatarUrl;
            avatarImg.style.display = 'block';
        }

        // Apply gradient banner
        const bannerEl = popup.querySelector('.snakk-popup-banner') as HTMLElement;
        if (bannerEl && stats?.gradientCss) {
            bannerEl.style.background = stats.gradientCss;
        }

        // Update name from API if available
        if (stats && (stats.name || stats.displayName || stats.title)) {
            if (nameEl) nameEl.textContent = stats.name || stats.displayName || stats.title || name;
        }

        // Populate description
        const descText = stats?.description || stats?.bio || '';
        if (descriptionEl) descriptionEl.textContent = descText;

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
     * Show popup for an entity-link element by resolving its href path first.
     * Once resolved, sets the data-popup-* attributes and delegates to showPopup.
     */
    async showEntityLinkPopup(triggerEl: HTMLElement): Promise<void> {
        const path = triggerEl.getAttribute('href');
        if (!path) return;

        const resolved = await this.resolveEntityPath(path);
        if (!resolved || this.currentTrigger !== triggerEl) return;

        // Set popup attributes so showPopup can use the standard flow
        triggerEl.dataset.popupType = resolved.type;
        triggerEl.dataset.popupId = resolved.publicId;
        triggerEl.dataset.popupName = resolved.name;

        await this.showPopup(triggerEl);
    }

    /**
     * Handle mouse over on trigger elements (mouseover bubbles, mouseenter doesn't)
     */
    handleMouseOver(e: Event): void {
        // Check for standard popup triggers or entity-link elements
        let triggerEl = (e.target as HTMLElement).closest('[data-popup-type]') as HTMLElement | null;
        const isEntityLink = !triggerEl
            && !!(triggerEl = (e.target as HTMLElement).closest('a.entity-link') as HTMLElement | null);

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
            if (isEntityLink && !triggerEl!.dataset.popupType) {
                this.showEntityLinkPopup(triggerEl!);
            } else {
                this.showPopup(triggerEl!);
            }
        }, this.popupDelay);
    }

    /**
     * Handle mouse out on trigger elements
     */
    handleMouseOut(e: Event): void {
        const triggerEl = (e.target as HTMLElement).closest('[data-popup-type], a.entity-link') as HTMLElement | null;
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
     * Immediately dismiss popup and clear all state
     */
    dismissPopup(): void {
        if (this.showTimeout) clearTimeout(this.showTimeout);
        if (this.hideTimeout) clearTimeout(this.hideTimeout);
        this.showTimeout = null;
        this.hideTimeout = null;
        this.currentTrigger = null;

        if (this.currentPopup) {
            this.currentPopup.style.display = 'none';
        }
    }

    /**
     * Initialize event delegation
     */
    init(): void {
        this._mouseOverHandler = (e: Event) => this.handleMouseOver(e);
        this._mouseOutHandler = (e: Event) => this.handleMouseOut(e);

        document.addEventListener('mouseover', this._mouseOverHandler, false);
        document.addEventListener('mouseout', this._mouseOutHandler, false);

        // Close popup on HTMX navigation so it doesn't linger after page swap
        document.addEventListener('htmx:beforeRequest', () => this.dismissPopup(), false);

        // Close popup on scroll so it doesn't float detached from its trigger
        window.addEventListener('scroll', () => this.dismissPopup(), { passive: true });
    }

    /**
     * Clear the stats cache
     */
    clearCache(): void {
        this.statsCache.clear();
        this.resolveCache.clear();
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
        this.resolveCache.clear();
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

// --- Feed dropdown (lazy populate before first open) ---
document.addEventListener('pointerdown', (e: Event) => {
    const btn = (e.target as HTMLElement).closest('.feed-dropdown-btn') as HTMLElement | null;
    if (!btn) return;

    const menu = btn.nextElementSibling as HTMLElement | null;
    if (!menu || menu.children.length > 0) return;

    const base = btn.dataset.feedBase || '';
    const items = [
        { href: `${base}.xml`,  label: 'RSS',  color: 'text-orange-500', icon: '<path d="M6.18 15.64a2.18 2.18 0 0 1 2.18 2.18C8.36 19 7.38 20 6.18 20C5 20 4 19 4 17.82a2.18 2.18 0 0 1 2.18-2.18M4 4.44A15.56 15.56 0 0 1 19.56 20h-2.83A12.73 12.73 0 0 0 4 7.27V4.44m0 5.66a9.9 9.9 0 0 1 9.9 9.9h-2.83A7.07 7.07 0 0 0 4 12.93V10.1z"/>', fill: true },
        { href: `${base}.atom`, label: 'Atom', color: 'text-purple-500', icon: '<circle cx="12" cy="12" r="2.5"/><ellipse cx="12" cy="12" rx="10" ry="4"/><ellipse cx="12" cy="12" rx="10" ry="4" transform="rotate(60 12 12)"/><ellipse cx="12" cy="12" rx="10" ry="4" transform="rotate(120 12 12)"/>', fill: false },
        { href: `${base}.json`, label: 'JSON', color: 'text-green-500', icon: '<path d="M7 4a2 2 0 0 0-2 2v3a2 2 0 0 1-2 2 2 2 0 0 1 2 2v3a2 2 0 0 0 2 2"/><path d="M17 4a2 2 0 0 1 2 2v3a2 2 0 0 0 2 2 2 2 0 0 0-2 2v3a2 2 0 0 1-2 2"/>', fill: false },
    ];

    for (const item of items) {
        const li = document.createElement('li');
        const a = document.createElement('a');
        a.href = item.href;
        a.target = '_blank';
        a.rel = 'noopener';
        const strokeAttr = item.fill ? '' : ' fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"';
        const fillAttr = item.fill ? ' fill="currentColor"' : '';
        a.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 ${item.color} shrink-0" viewBox="0 0 24 24"${fillAttr}${strokeAttr}>${item.icon}</svg>${item.label}`;
        li.appendChild(a);
        menu.appendChild(li);
    }
});

})();
