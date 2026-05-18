    /**
 * Snakk Hover Popup Component
 * Displays entity information on hover
 */

(function(): void {
    'use strict';

    // Guard: skip re-execution during HTMX SPA navigation
    if ((window as any).SnakkPopup) return;

    // Coarse pointer = touch primary input. Cached + refreshed on change.
    const coarseQuery = window.matchMedia('(hover: none)');
    let isCoarse = coarseQuery.matches;
    const onCoarseChange = (e: MediaQueryListEvent | MediaQueryList) => {
        isCoarse = e.matches;
    };
    if (coarseQuery.addEventListener) {
        coarseQuery.addEventListener('change', onCoarseChange);
    } else if ((coarseQuery as any).addListener) {
        (coarseQuery as any).addListener(onCoarseChange);
    }

    // Track the most recent pointer type so we can suppress hover-popup on touch
    // even on devices that report (hover: hover) (e.g. iPad with Smart Keyboard).
    let lastPointerType = '';
    document.addEventListener('pointerdown', (e: PointerEvent) => {
        lastPointerType = e.pointerType;
    }, { capture: true, passive: true });

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
    avatarThumbnailUrl?: string;
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

    private fetchAbortController: AbortController | null = null;

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
                    <a class="snakk-popup-goto" href="#" style="display:none"></a>
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
            <button type="button" class="snakk-popup-close" aria-label="Close">
                <span class="icon icon-x" aria-hidden="true"></span>
            </button>
        `;
        popup.style.display = 'none';
        document.body.appendChild(popup);

        // Keep popup visible when hovering over it (mouse only — no effect on touch)
        popup.addEventListener('mouseenter', () => {
            if (isCoarse) return;
            if (this.hideTimeout) {
                clearTimeout(this.hideTimeout);
            }
        });
        popup.addEventListener('mouseleave', () => {
            if (isCoarse) return;
            this.scheduleHide();
        });

        // Touch close button
        popup.querySelector('.snakk-popup-close')?.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.dismissPopup();
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
    async fetchStats(type: string, publicId: string, signal?: AbortSignal): Promise<EntityStats | null> {
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
            const response = await fetch(endpoint, { credentials: 'include', signal });
            if (!response.ok) {
                return null;
            }
            const data = await response.json() as EntityStats;
            this.statsCache.set(cacheKey, data);
            return data;
        } catch (err) {
            if (err instanceof Error && err.name === 'AbortError') return null;
            console.error('[SnakkPopup] Error fetching stats:', err);
            return null;
        }
    }

    /**
     * Resolve an internal entity path to type, publicId, and name.
     * Used for .entity-link elements that don't have data-popup-* attributes.
     */
    async resolveEntityPath(path: string, signal?: AbortSignal): Promise<EntityResolveResult | null> {
        if (this.resolveCache.has(path)) {
            return this.resolveCache.get(path) || null;
        }

        try {
            const response = await fetch(`/bff/entity/resolve?path=${encodeURIComponent(path)}`, {
                credentials: 'include',
                signal,
            });
            if (!response.ok) {
                this.resolveCache.set(path, null);
                return null;
            }
            const data = await response.json() as EntityResolveResult;
            this.resolveCache.set(path, data);
            return data;
        } catch (err) {
            if (err instanceof Error && err.name === 'AbortError') return null;
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

        const gap = 10;
        const edgeMargin = 8;

        // Viewport-space available below and above the trigger
        const spaceBelow = window.innerHeight - rect.bottom - gap;
        const spaceAbove = rect.top - gap;

        // Default: position below the trigger
        let top = rect.bottom + scrollTop + gap;
        let placeAbove = false;

        // Flip above only when popup doesn't fit below AND actually fits above.
        // Both conditions must hold so the popup never goes off-screen above,
        // and never covers the trigger when content grows during load.
        if (popupRect.height > spaceBelow && popupRect.height <= spaceAbove) {
            top = rect.top + scrollTop - popupRect.height - gap;
            placeAbove = true;
        }

        // Clamp popup height to the available space in the chosen direction.
        // This prevents content growth between the two positionPopup calls
        // from pushing the popup boundary over the trigger element.
        const availableSpace = placeAbove ? spaceAbove : spaceBelow;
        popup.style.maxHeight = `${Math.max(availableSpace - edgeMargin, 120)}px`;

        // Horizontal: clamp to viewport
        let left = rect.left + scrollLeft;
        if (left + popupRect.width > window.innerWidth - edgeMargin) {
            left = window.innerWidth - popupRect.width - edgeMargin;
        }
        if (left < edgeMargin) left = edgeMargin;

        popup.style.top = `${top}px`;
        popup.style.left = `${left}px`;
    }

    /**
     * Show popup for a trigger element
     */
    async showPopup(triggerEl: HTMLElement, signal?: AbortSignal): Promise<void> {
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
        const gotoLink = popup.querySelector('.snakk-popup-goto') as HTMLAnchorElement;

        // Set loading state — show skeletons, hide real content
        if (avatarSkeleton) avatarSkeleton.style.display = 'block';
        if (avatarImg) avatarImg.style.display = 'none';
        if (nameEl) nameEl.textContent = name;
        if (typeEl) typeEl.textContent = this.getTypeDisplayName(type);
        if (descriptionEl) descriptionEl.textContent = '';
        if (statsContainer) statsContainer.replaceChildren();
        if (gotoLink) gotoLink.style.display = 'none';
        if (statsSkeleton) statsSkeleton.style.display = 'block';
        const bannerElLoading = popup.querySelector('.snakk-popup-banner') as HTMLElement;
        if (bannerElLoading) bannerElLoading.style.removeProperty('background');

        // Show popup
        popup.style.display = 'block';
        this.positionPopup(popup, triggerEl);

        // Use caller's signal (entity-link flow) or create a fresh one
        if (!signal) {
            const controller = new AbortController();
            this.fetchAbortController = controller;
            signal = controller.signal;
        }

        // Fetch and display stats
        const stats = await this.fetchStats(type, publicId, signal);

        // Guard: if popup was hidden while awaiting (e.g. navigation), bail out
        if (this.currentTrigger !== triggerEl) return;

        // Hide skeletons
        if (statsSkeleton) statsSkeleton.style.display = 'none';

        // Update avatar from API response (includes correct sharded URL)
        if (avatarSkeleton) avatarSkeleton.style.display = 'none';
        const popupAvatarUrl = stats?.avatarThumbnailUrl || stats?.avatarUrl;
        if (stats && popupAvatarUrl && avatarImg) {
            avatarImg.src = popupAvatarUrl;
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

        // Append sparkline as a stat box inside the grid
        const sparklineTypes = new Set(['space', 'hub', 'community', 'discussion', 'user']);
        if (sparklineTypes.has(type)) {
            const grid = statsContainer?.querySelector('.snakk-popup-stats-grid');
            if (grid) {
                const sparklineStat = document.createElement('div');
                sparklineStat.className = 'stat snakk-popup-sparkline-stat';

                const titleEl = document.createElement('div');
                titleEl.className = 'stat-title';
                titleEl.textContent = 'Activity';

                const sparklineContainer = document.createElement('div');
                sparklineContainer.className = 'sn-sparkline-container';
                sparklineContainer.dataset.sparklineType = type;
                sparklineContainer.dataset.sparklineId = publicId;
                sparklineContainer.dataset.sparklineDays = '14';
                sparklineContainer.dataset.sparklineMetric = 'postCount';

                sparklineStat.appendChild(titleEl);
                sparklineStat.appendChild(sparklineContainer);
                grid.appendChild(sparklineStat);

                const snakkSparkline = (window as any).SnakkSparkline;
                if (snakkSparkline) snakkSparkline.init();
            }
        }

        // Populate "Go to" link
        if (gotoLink) {
            const href = triggerEl.getAttribute('href');
            const displayName = (stats?.name || stats?.displayName || stats?.title || name).trim();
            if (href) {
                gotoLink.href = href;
                gotoLink.textContent = `Go to ${this.getTypeDisplayName(type)}: ${displayName}`;
                gotoLink.style.display = '';
            }
        }

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

        const controller = new AbortController();
        this.fetchAbortController = controller;

        const resolved = await this.resolveEntityPath(path, controller.signal);
        if (!resolved || this.currentTrigger !== triggerEl) return;

        // Set popup attributes so showPopup can use the standard flow
        triggerEl.dataset.popupType = resolved.type;
        triggerEl.dataset.popupId = resolved.publicId;
        triggerEl.dataset.popupName = resolved.name;

        await this.showPopup(triggerEl, controller.signal);
    }

    /**
     * Handle mouse over on trigger elements (mouseover bubbles, mouseenter doesn't)
     */
    handleMouseOver(e: Event): void {
        // Touch devices get tap-to-open via handleTriggerClick; ignore hover paths.
        // Also skip when the last pointer event was touch, to handle hybrid devices
        // (e.g. iPad with keyboard) that report (hover: hover) but still fire a
        // synthetic mouseover before the click when the user taps a link.
        if (isCoarse || lastPointerType === 'touch') return;

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
        if (isCoarse || lastPointerType === 'touch') return;

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

        if (this.fetchAbortController) {
            this.fetchAbortController.abort();
            this.fetchAbortController = null;
        }

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

        // Tap-to-open on coarse pointers. First tap on a trigger opens the popup
        // and suppresses the link navigation; tapping the "Go to" link navigates.
        document.addEventListener('click', (e: Event) => {
            if (!isCoarse) return;
            const target = e.target as HTMLElement;

            // Tap inside an already-open popup is for its own controls — do nothing here.
            if (this.currentPopup && this.currentPopup.contains(target)) return;

            let triggerEl = target.closest('[data-popup-type]') as HTMLElement | null;
            const isEntityLink = !triggerEl
                && !!(triggerEl = target.closest('a.entity-link') as HTMLElement | null);
            if (!triggerEl || triggerEl.classList.contains('breadcrumb-current')) return;

            e.preventDefault();
            if (this.showTimeout) clearTimeout(this.showTimeout);
            if (this.hideTimeout) clearTimeout(this.hideTimeout);
            this.currentTrigger = triggerEl;
            if (isEntityLink && !triggerEl.dataset.popupType) {
                this.showEntityLinkPopup(triggerEl);
            } else {
                this.showPopup(triggerEl);
            }
        }, false);

        // Outside-tap close on coarse pointers.
        document.addEventListener('pointerdown', (e: Event) => {
            if (!isCoarse) return;
            if (!this.currentPopup || this.currentPopup.style.display === 'none') return;
            const target = e.target as HTMLElement;
            if (this.currentPopup.contains(target)) return;
            if (target.closest('[data-popup-type], a.entity-link')) return;
            this.dismissPopup();
        }, false);

        // Close popup on HTMX navigation so it doesn't linger after page swap
        document.addEventListener('htmx:beforeRequest', () => this.dismissPopup(), false);

        // Close popup on scroll so it doesn't float detached from its trigger
        window.addEventListener('scroll', () => this.dismissPopup(), { passive: true });

        // Close popup (and cancel fetches) when any link click triggers navigation.
        // On touch: skip trigger elements — the tap-to-open handler manages those.
        const dismissOnAnchor = (e: Event) => {
            if ((e as MouseEvent).button !== undefined && (e as MouseEvent).button !== 0) return;
            const anchor = (e.target as HTMLElement).closest('a') as HTMLAnchorElement | null;
            if (!anchor) return;
            if (this.currentPopup && this.currentPopup.contains(anchor)) return;
            if (isCoarse && anchor.closest('[data-popup-type], a.entity-link')) return;
            this.dismissPopup();
        };
        document.addEventListener('mousedown', dismissOnAnchor, false);
        document.addEventListener('click', dismissOnAnchor, false);

        // Close popup on browser back/forward navigation
        window.addEventListener('popstate', () => this.dismissPopup());
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

        if (this.fetchAbortController) {
            this.fetchAbortController.abort();
            this.fetchAbortController = null;
        }

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
