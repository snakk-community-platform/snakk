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
    isGlobalAdmin?: boolean;
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
        popup.className = 'sn-snakk-popup';
        popup.innerHTML = `
            <div class="sn-card-wrap card-xs sn-snakk-popup-inner">
                <figure class="sn-snakk-popup-banner">
                    <div class="sn-snakk-popup-banner-info">
                        <div class="sn-snakk-popup-name"></div>
                        <span class="snakk-popup-admin-badge sn-badge sn-badge-sm sn-badge-warning" style="display:none">Admin</span>
                        <div class="sn-snakk-popup-type"></div>
                    </div>
                </figure>
                <div class="sn-card-content sn-snakk-popup-body">
                    <div class="sn-snakk-popup-description"></div>
                    <div class="sn-snakk-popup-stats"></div>
                    <div class="sn-snakk-popup-actions">
                        <a class="sn-snakk-popup-message sn-btn sn-btn-xs sn-btn-ghost" href="#" style="display:none">
                            <span class="sn-icon icon-chat-bubbles" style="width:0.85rem;height:0.85rem" aria-hidden="true"></span>
                            Message
                        </a>
                        <a class="sn-snakk-popup-goto" href="#" style="display:none"></a>
                    </div>
                    <div class="sn-snakk-popup-stats-skeleton sn-snakk-popup-stats-grid">
                        <div class="sn-stat"><div class="sn-skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="sn-skeleton" style="height:.875rem;width:1.75rem"></div></div>
                        <div class="sn-stat"><div class="sn-skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="sn-skeleton" style="height:.875rem;width:1.75rem"></div></div>
                        <div class="sn-stat"><div class="sn-skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="sn-skeleton" style="height:.875rem;width:1.75rem"></div></div>
                        <div class="sn-stat"><div class="sn-skeleton" style="height:.55rem;width:3rem;margin-bottom:.3rem"></div><div class="sn-skeleton" style="height:.875rem;width:1.75rem"></div></div>
                    </div>
                </div>
            </div>
            <div class="sn-snakk-popup-avatar-skeleton sn-skeleton"></div>
            <img class="sn-snakk-popup-avatar" src="" alt="" style="display:none" />
            <button type="button" class="sn-snakk-popup-close" aria-label="Close">
                <span class="sn-icon icon-x" aria-hidden="true"></span>
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
        popup.querySelector('.sn-snakk-popup-close')?.addEventListener('click', (e) => {
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
        stat.className = 'sn-stat';

        const titleEl = document.createElement('div');
        titleEl.className = 'sn-stat-title';
        titleEl.textContent = label;

        const valueEl = document.createElement('div');
        valueEl.className = 'sn-stat-value';
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
            error.className = 'sn-snakk-popup-error';
            error.textContent = 'Could not load stats';
            fragment.appendChild(error);
            return fragment;
        }

        const container = document.createElement('div');
        container.className = 'sn-snakk-popup-stats-grid';

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

        // Render off-screen while fetching so we can measure final dimensions before revealing.
        // This prevents the popup from visibly jumping as content loads and the size changes.
        popup.classList.remove('sn-snakk-popup--ready');
        popup.style.visibility = 'hidden';
        popup.style.display = 'block';

        // Use caller's signal (entity-link flow) or create a fresh one
        if (!signal) {
            const controller = new AbortController();
            this.fetchAbortController = controller;
            signal = controller.signal;
        }

        const stats = await this.fetchStats(type, publicId, signal);

        // Guard: if trigger changed while awaiting (e.g. mouse moved away), bail out
        if (this.currentTrigger !== triggerEl) return;

        // Populate all content with final data before measuring/positioning
        const avatarSkeleton = popup.querySelector('.sn-snakk-popup-avatar-skeleton') as HTMLElement;
        const avatarImg = popup.querySelector('.sn-snakk-popup-avatar') as HTMLImageElement;
        const nameEl = popup.querySelector('.sn-snakk-popup-name') as HTMLElement;
        const typeEl = popup.querySelector('.sn-snakk-popup-type') as HTMLElement;
        const descriptionEl = popup.querySelector('.sn-snakk-popup-description') as HTMLElement;
        const statsSkeleton = popup.querySelector('.sn-snakk-popup-stats-skeleton') as HTMLElement;
        const statsContainer = popup.querySelector('.sn-snakk-popup-stats') as HTMLElement;
        const gotoLink = popup.querySelector('.sn-snakk-popup-goto') as HTMLAnchorElement;
        const messageLink = popup.querySelector('.sn-snakk-popup-message') as HTMLAnchorElement;

        if (statsSkeleton) statsSkeleton.style.display = 'none';
        if (avatarSkeleton) avatarSkeleton.style.display = 'none';

        const popupAvatarUrl = stats?.avatarThumbnailUrl || stats?.avatarUrl;
        if (stats && popupAvatarUrl && avatarImg) {
            avatarImg.src = popupAvatarUrl;
            avatarImg.style.display = 'block';
        } else if (avatarImg) {
            avatarImg.style.display = 'none';
        }

        if (nameEl) nameEl.textContent = stats?.name || stats?.displayName || stats?.title || name;
        const adminBadgeEl = popup.querySelector('.snakk-popup-admin-badge') as HTMLElement;
        if (adminBadgeEl) adminBadgeEl.style.display = stats?.isGlobalAdmin ? '' : 'none';
        if (typeEl) typeEl.textContent = this.getTypeDisplayName(type);

        const bannerEl = popup.querySelector('.sn-snakk-popup-banner') as HTMLElement;
        if (bannerEl) {
            if (stats?.gradientCss) {
                bannerEl.style.background = stats.gradientCss;
            } else {
                bannerEl.style.removeProperty('background');
            }
        }

        if (descriptionEl) descriptionEl.textContent = stats?.description || stats?.bio || '';

        const statsElements = this.buildStatsElements(type, stats);
        if (statsContainer) statsContainer.replaceChildren(statsElements);

        const sparklineTypes = new Set(['space', 'hub', 'community', 'discussion', 'user']);
        if (sparklineTypes.has(type)) {
            const grid = statsContainer?.querySelector('.sn-snakk-popup-stats-grid');
            if (grid) {
                const sparklineStat = document.createElement('div');
                sparklineStat.className = 'sn-stat sn-snakk-popup-sparkline-stat';

                const titleEl = document.createElement('div');
                titleEl.className = 'sn-stat-title';
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

        if (gotoLink) {
            const href = triggerEl.getAttribute('href');
            const displayName = (stats?.name || stats?.displayName || stats?.title || name).trim();
            if (href) {
                gotoLink.href = href;
                gotoLink.textContent = `Go to ${this.getTypeDisplayName(type)}: ${displayName}`;
                gotoLink.style.display = '';
            } else {
                gotoLink.style.display = 'none';
            }
        }

        if (messageLink) {
            const currentUserPublicId = document.querySelector<HTMLMetaElement>('meta[name="current-user-id"]')?.content ?? '';
            const isOwnProfile = publicId === currentUserPublicId;
            const isMessagingEnabled = document.body.dataset.messaging !== '0';
            if (type === 'user' && currentUserPublicId && !isOwnProfile && isMessagingEnabled) {
                messageLink.href = `/messages/start?recipient=${encodeURIComponent(publicId)}`;
                messageLink.style.display = '';
            } else {
                messageLink.style.display = 'none';
            }
        }

        // Position once against final dimensions, then reveal with animation.
        // Force a reflow between the class removal (above) and addition so the
        // browser treats them as separate frames and replays the animation.
        this.positionPopup(popup, triggerEl);
        void popup.offsetHeight;
        popup.style.visibility = '';
        popup.classList.add('sn-snakk-popup--ready');
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
        // Popups are desktop-only. Also skip when the last pointer event was touch
        // to handle hybrid devices (e.g. iPad with keyboard) that report (hover: hover)
        // but still fire a synthetic mouseover before the click when the user taps a link.
        if (isCoarse || lastPointerType === 'touch') return;
        try { if (localStorage.getItem('snakk:disable-hover-popup') === 'true') return; } catch { /* ignore */ }

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
    syncPopupTitles(): void {
        let disabled = false;
        try { disabled = localStorage.getItem('snakk:disable-hover-popup') === 'true'; } catch { /* ignore */ }
        document.querySelectorAll<HTMLElement>('[data-popup-type][data-popup-name]').forEach(el => {
            if (disabled) {
                el.title = el.dataset.popupName!;
            } else {
                el.removeAttribute('title');
            }
        });
    }

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
            this.dismissPopup();
        };
        document.addEventListener('mousedown', dismissOnAnchor, false);
        document.addEventListener('click', dismissOnAnchor, false);

        // Close popup on browser back/forward navigation
        window.addEventListener('popstate', () => this.dismissPopup());

        // Keep title attributes on [data-popup-type] elements in sync with the
        // hover-popup preference so tooltip and popup are never shown together.
        this.syncPopupTitles();
        document.addEventListener('htmx:afterSettle', () => { this.dismissPopup(); this.syncPopupTitles(); }, false);
        window.addEventListener('storage', (e: StorageEvent) => {
            if (e.key === 'snakk:disable-hover-popup') this.syncPopupTitles();
        });
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
        { href: `${base}.xml`,  label: 'RSS',  iconClass: 'icon-rss',       color: 'text-orange-500' },
        { href: `${base}.atom`, label: 'Atom', iconClass: 'icon-atom',      color: 'text-purple-500' },
        { href: `${base}.json`, label: 'JSON', iconClass: 'icon-json-feed', color: 'text-green-500'  },
    ];

    for (const item of items) {
        const li = document.createElement('li');
        const a = document.createElement('a');
        a.href = item.href;
        a.target = '_blank';
        a.rel = 'noopener';
        a.innerHTML = `<span class="sn-icon ${item.iconClass} sn-h-4 sn-w-4 sn-shrink-0 ${item.color}" aria-hidden="true"></span>${item.label}`;
        li.appendChild(a);
        menu.appendChild(li);
    }
});

// --- Registration nudge: last-used OAuth badge + collapse/expand ---
function initNudgeOAuthLastUsed(): void {
    const STORAGE_KEY = 'snakk:lastOAuthProvider';
    let last: string | null = null;
    try { last = localStorage.getItem(STORAGE_KEY); } catch {}
    if (!last) return;

    // Process each oauth container independently so the nudge card and the
    // modal (both rendered in the same page HTML) behave separately.
    document.querySelectorAll<HTMLElement>('.sn-nudge-oauth').forEach(container => {
        if (container.dataset['lastUsedInit']) return;
        container.dataset['lastUsedInit'] = '1';

        const allBtns = Array.from(container.querySelectorAll<HTMLElement>('.sn-nudge-oauth-btn[data-provider]'));
        if (!allBtns.length) return;

        const lastBtn = allBtns.find(b => b.dataset['provider'] === last);
        if (!lastBtn) return;

        // When the last-used provider is passkey, only proceed if the button is
        // already visible — initNudgePasskey runs first and keeps it hidden when
        // WebAuthn is unavailable.
        if (last === 'passkey' && lastBtn.style.display === 'none') return;

        // Add "Last used" badge
        if (!lastBtn.querySelector('.sn-badge-last-used')) {
            const badge = document.createElement('span');
            badge.className = 'sn-badge-last-used';
            badge.textContent = 'Last used';
            lastBtn.appendChild(badge);
        }

        // When last-used is OAuth: hide other OAuth buttons but leave passkey alone.
        // When last-used is passkey: hide all OAuth buttons.
        const oauthBtns = allBtns.filter(b => b.dataset['provider'] !== 'passkey');
        const toHide = last === 'passkey' ? oauthBtns : oauthBtns.filter(b => b !== lastBtn);
        if (!toHide.length) return;

        const hadGrid = container.classList.contains('sn-nudge-oauth--grid');
        if (hadGrid) container.classList.remove('sn-nudge-oauth--grid');
        toHide.forEach(b => { b.style.display = 'none'; });

        const moreBtn = document.createElement('button');
        moreBtn.type = 'button';
        moreBtn.className = 'sn-w-full sn-text-center sn-text-sm text-base-content/40 hover:text-base-content/70 sn-py-1.5 sn-transition-colors sn-cursor-pointer';
        moreBtn.textContent = 'More sign-in options';
        container.appendChild(moreBtn);

        moreBtn.addEventListener('click', () => {
            toHide.forEach(b => { b.style.display = ''; });
            if (hadGrid) container.classList.add('sn-nudge-oauth--grid');
            moreBtn.remove();
        });
    });
}

// --- Registration nudge: passkey login ---
function initNudgePasskey(): void {
    const btn = document.querySelector<HTMLButtonElement>('[data-nudge-passkey]');
    if (!btn || btn.dataset['nudgePasskeyInit'] || !window.PublicKeyCredential) return;
    btn.dataset['nudgePasskeyInit'] = '1';
    btn.style.display = '';
    const divider = document.getElementById('nudge-alt-divider');
    if (divider) divider.style.display = '';

    const errorEl = document.getElementById('nudge-passkey-error');
    const returnUrl = btn.dataset['returnUrl'] || '/';

    function b64urlToBuffer(s: string): ArrayBuffer {
        const base64 = s.replace(/-/g, '+').replace(/_/g, '/');
        const padded = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, '=');
        const binary = atob(padded);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return bytes.buffer;
    }

    function bufToB64url(buf: ArrayBuffer): string {
        const bytes = new Uint8Array(buf);
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!);
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
    }

    function prepareOptions(optionsJson: string): PublicKeyCredentialRequestOptions {
        const raw = JSON.parse(optionsJson) as {
            challenge: string;
            allowCredentials?: Array<{ id: string; type: string; transports?: AuthenticatorTransport[] }>;
            timeout?: number;
            userVerification?: UserVerificationRequirement;
            rpId?: string;
        };
        return {
            challenge: b64urlToBuffer(raw.challenge),
            allowCredentials: raw.allowCredentials?.map(c => ({
                id: b64urlToBuffer(c.id),
                type: c.type as PublicKeyCredentialType,
                transports: c.transports
            })),
            timeout: raw.timeout,
            userVerification: raw.userVerification ?? 'required',
            rpId: raw.rpId
        };
    }

    function serializeAssertion(cred: PublicKeyCredential): string {
        const resp = cred.response as AuthenticatorAssertionResponse;
        return JSON.stringify({
            id: cred.id,
            rawId: bufToB64url(cred.rawId),
            type: cred.type,
            response: {
                authenticatorData: bufToB64url(resp.authenticatorData),
                clientDataJSON:    bufToB64url(resp.clientDataJSON),
                signature:         bufToB64url(resp.signature),
                userHandle:        resp.userHandle ? bufToB64url(resp.userHandle) : null
            }
        });
    }

    btn.addEventListener('click', () => {
        btn.disabled = true;
        if (errorEl) { errorEl.textContent = ''; errorEl.classList.add('sn-hidden'); }

        fetch('/auth/passkey/begin-login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: null })
        })
        .then(res => {
            if (!res.ok) throw new Error('Could not start passkey login');
            return res.json() as Promise<{ optionsJson: string; challengeId: string }>;
        })
        .then(data => {
            if (errorEl) {
                errorEl.style.color = 'var(--text-secondary)';
                errorEl.textContent = (window as any).T?.nudge?.passkeyPromptHint ?? '';
                errorEl.classList.remove('sn-hidden');
            }
            return navigator.credentials.get({ publicKey: prepareOptions(data.optionsJson) })
                .then(cred => fetch('/auth/passkey/complete-login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        challengeId: data.challengeId,
                        assertionResponseJson: serializeAssertion(cred as PublicKeyCredential),
                        returnUrl
                    })
                }));
        })
        .then(res => {
            if (!res.ok) return (res.json().catch(() => ({})) as Promise<any>).then((e: any) => { throw new Error(e.error || 'Passkey login failed'); });
            return res.json() as Promise<{ twoFactorRequired?: boolean; email?: string; redirectUrl?: string }>;
        })
        .then(data => {
            if (data.twoFactorRequired) {
                window.location.href = '/auth/twofactorverify?email='
                    + encodeURIComponent(data.email || '')
                    + '&returnUrl=' + encodeURIComponent(returnUrl)
                    + '&provider=passkey';
                return;
            }
            try { localStorage.setItem('snakk:lastOAuthProvider', 'passkey'); } catch {}
            window.location.href = data.redirectUrl || returnUrl;
        })
        .catch((err: unknown) => {
            btn.disabled = false;
            if (errorEl) { errorEl.style.color = ''; errorEl.classList.add('sn-hidden'); }
            if (err instanceof Error && err.name === 'NotAllowedError') return;
            if (errorEl) {
                errorEl.textContent = err instanceof Error ? err.message : 'Passkey login failed. Please try again.';
                errorEl.classList.remove('sn-hidden');
            }
        });
    });
}

initNudgePasskey();
initNudgeOAuthLastUsed();
document.addEventListener('htmx:afterSettle', () => { initNudgePasskey(); initNudgeOAuthLastUsed(); });

// Reload on back-navigation so page-specific CSS injected via htmx-head-support
// is always present. htmx:historyRestore covers HTMX-managed history; pageshow
// with persisted:true covers the native browser back/forward cache (bfcache).
document.addEventListener('htmx:historyRestore', () => window.location.reload());
window.addEventListener('pageshow', (e: PageTransitionEvent) => {
    if (e.persisted) window.location.reload();
});

})();
