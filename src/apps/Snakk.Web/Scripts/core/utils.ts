/**
 * Snakk Utility Functions
 * Shared helpers for formatting, validation, and DOM manipulation
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface Discussion {
    isPinned?: boolean;
    isLocked?: boolean;
    tags?: string[];
    [key: string]: any;
}

interface ScrollOptions {
    behavior?: ScrollBehavior;
    block?: ScrollLogicalPosition;
    inline?: ScrollLogicalPosition;
}

interface SnakkUtilsAPI {
    formatRelativeTime(dateString: string | Date): string;
    formatCount(count: number | null | undefined): string;
    escapeHtml(text: string): string;
    sanitizeHtml(html: string): string;
    sanitizeUrl(url: string): string;
    formatDiscussionBadges(discussion: Discussion): string;
    debounce<T extends (...args: any[]) => any>(func: T, wait: number): (...args: Parameters<T>) => void;
    throttle<T extends (...args: any[]) => any>(func: T, limit: number): (...args: Parameters<T>) => void;
    copyToClipboard(text: string): Promise<boolean>;
    truncate(text: string, maxLength: number): string;
    parseQuery(queryString?: string): Record<string, string>;
    buildQuery(params: Record<string, any>): string;
    isInViewport(element: Element, threshold?: number): boolean;
    smoothScrollTo(element: Element, options?: ScrollOptions): void;
    getOffsetTop(element: HTMLElement): number;
    isValidEmail(email: string): boolean;
    isValidUrl(url: string): boolean;
    generateId(prefix?: string): string;
    clone<T>(obj: T): T;
    dispatchEvent(name: string, detail?: any): void;
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    /**
     * Format a date as relative time (e.g., "2m ago", "5h ago", "3d ago")
     */
    function formatRelativeTime(dateString: string | Date): string {
        if (!dateString) return '';
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'just now';
        if (diffMins < 60) return diffMins + 'm ago';
        if (diffHours < 24) return diffHours + 'h ago';
        if (diffDays < 7) return diffDays + 'd ago';
        const tz = (window as any).snakkTimezone || 'UTC';
        try {
            if (diffDays < 365) return date.toLocaleDateString('en-US', { timeZone: tz, month: 'short', day: 'numeric' });
            return date.toLocaleDateString('en-US', { timeZone: tz, month: 'short', day: 'numeric', year: 'numeric' });
        } catch {
            if (diffDays < 365) return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
        }
    }

    /**
     * Format a number with k/M suffix (e.g., 1.2k, 2.5M)
     */
    function formatCount(count: number | null | undefined): string {
        if (count == null) return '0';
        if (count >= 1000000) {
            return (count / 1000000).toFixed(1).replace(/\.0$/, '') + 'M';
        }
        if (count >= 1000) {
            return (count / 1000).toFixed(1).replace(/\.0$/, '') + 'k';
        }
        return count.toString();
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text: string): string {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Sanitize HTML to prevent XSS while preserving safe markup.
     * Strips script/iframe/object tags, event handler attributes, and javascript: URLs.
     * Use for server-rendered HTML content (e.g., rendered markdown from BFF).
     */
    function sanitizeHtml(html: string): string {
        if (!html) return '';

        const parser = new DOMParser();
        const doc = parser.parseFromString(html, 'text/html');

        // Remove dangerous elements entirely
        const dangerousTags = ['script', 'iframe', 'object', 'embed', 'form', 'base', 'meta', 'link', 'style'];
        dangerousTags.forEach(tag => {
            doc.querySelectorAll(tag).forEach(el => el.remove());
        });

        // Sanitize all remaining elements
        doc.body.querySelectorAll('*').forEach(el => {
            // Remove event handler attributes (onclick, onerror, onload, etc.)
            Array.from(el.attributes).forEach(attr => {
                if (attr.name.startsWith('on')) {
                    el.removeAttribute(attr.name);
                }
            });

            // Sanitize href/src/action attributes — block javascript: and data: (except images)
            ['href', 'src', 'action', 'formaction', 'xlink:href'].forEach(attrName => {
                const value = el.getAttribute(attrName);
                if (!value) return;
                const trimmed = value.trim().toLowerCase();
                if (trimmed.startsWith('javascript:')) {
                    el.removeAttribute(attrName);
                }
                if (attrName === 'src' && trimmed.startsWith('data:') && !trimmed.startsWith('data:image/')) {
                    el.removeAttribute(attrName);
                }
            });
        });

        return doc.body.innerHTML;
    }

    /**
     * Sanitize a URL to prevent javascript: and data: protocol injection.
     * Returns '#' for dangerous URLs.
     */
    function sanitizeUrl(url: string): string {
        if (!url) return '#';
        const trimmed = url.trim().toLowerCase();
        if (trimmed.startsWith('javascript:') || trimmed.startsWith('data:')) {
            return '#';
        }
        return url;
    }

    /**
     * Format badges HTML for a discussion (pinned, locked, tags)
     */
    function formatDiscussionBadges(discussion: Discussion): string {
        let badges = '';

        if (discussion.isPinned) {
            badges += '<span class="badge badge-primary badge-xs ml-2">Pinned</span>';
        }
        if (discussion.isLocked) {
            badges += '<span class="badge badge-warning badge-xs ml-2">Locked</span>';
        }
        if (discussion.tags && Array.isArray(discussion.tags) && discussion.tags.length > 0) {
            discussion.tags.slice(0, 3).forEach(tag => {
                badges += `<span class="badge badge-ghost badge-xs ml-2">${escapeHtml(tag)}</span>`;
            });
            if (discussion.tags.length > 3) {
                badges += '<span class="text-muted ml-1">...</span>';
            }
        }

        return badges;
    }

    /**
     * Debounce function calls
     */
    function debounce<T extends (...args: any[]) => any>(
        func: T,
        wait: number
    ): (...args: Parameters<T>) => void {
        let timeout: ReturnType<typeof setTimeout> | undefined;
        return function executedFunction(...args: Parameters<T>): void {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    /**
     * Throttle function calls
     */
    function throttle<T extends (...args: any[]) => any>(
        func: T,
        limit: number
    ): (...args: Parameters<T>) => void {
        let inThrottle: boolean = false;
        return function(this: any, ...args: Parameters<T>): void {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    /**
     * Copy text to clipboard
     */
    async function copyToClipboard(text: string): Promise<boolean> {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            // Fallback for older browsers
            const textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.select();
            const success = document.execCommand('copy');
            document.body.removeChild(textarea);
            return success;
        }
    }

    /**
     * Truncate text to a max length with ellipsis
     */
    function truncate(text: string, maxLength: number): string {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength).trim() + '...';
    }

    /**
     * Parse query string into object
     */
    function parseQuery(queryString?: string): Record<string, string> {
        const params = new URLSearchParams(queryString || window.location.search);
        const result: Record<string, string> = {};
        for (const [key, value] of params.entries()) {
            result[key] = value;
        }
        return result;
    }

    /**
     * Build query string from object
     */
    function buildQuery(params: Record<string, any>): string {
        const searchParams = new URLSearchParams();
        for (const [key, value] of Object.entries(params)) {
            if (value !== null && value !== undefined) {
                searchParams.append(key, String(value));
            }
        }
        return searchParams.toString();
    }

    /**
     * Check if element is in viewport
     */
    function isInViewport(element: Element, threshold: number = 0): boolean {
        const rect = element.getBoundingClientRect();
        return (
            rect.top >= -threshold &&
            rect.left >= -threshold &&
            rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) + threshold &&
            rect.right <= (window.innerWidth || document.documentElement.clientWidth) + threshold
        );
    }

    /**
     * Scroll element into view smoothly
     */
    function smoothScrollTo(element: Element, options: ScrollOptions = {}): void {
        const defaultOptions: ScrollIntoViewOptions = {
            behavior: 'smooth',
            block: 'center',
            inline: 'nearest'
        };
        element.scrollIntoView({ ...defaultOptions, ...options });
    }

    /**
     * Get element offset from document top
     */
    function getOffsetTop(element: HTMLElement | null): number {
        let offsetTop = 0;
        while (element) {
            offsetTop += element.offsetTop;
            element = element.offsetParent as HTMLElement | null;
        }
        return offsetTop;
    }

    /**
     * Validate email format
     */
    function isValidEmail(email: string): boolean {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    /**
     * Validate URL format
     */
    function isValidUrl(url: string): boolean {
        try {
            new URL(url);
            return true;
        } catch {
            return false;
        }
    }

    /**
     * Generate random ID
     */
    function generateId(prefix: string = 'id'): string {
        return `${prefix}-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * Deep clone object (simple version)
     */
    function clone<T>(obj: T): T {
        return JSON.parse(JSON.stringify(obj));
    }

    /**
     * Dispatch custom event
     */
    function dispatchEvent(name: string, detail: any = {}): void {
        document.dispatchEvent(new CustomEvent(name, { detail }));
    }

    // Export all utilities
    const SnakkUtils: SnakkUtilsAPI = {
        formatRelativeTime,
        formatCount,
        escapeHtml,
        sanitizeHtml,
        sanitizeUrl,
        formatDiscussionBadges,
        debounce,
        throttle,
        copyToClipboard,
        truncate,
        parseQuery,
        buildQuery,
        isInViewport,
        smoothScrollTo,
        getOffsetTop,
        isValidEmail,
        isValidUrl,
        generateId,
        clone,
        dispatchEvent
    };

    (window as any).SnakkUtils = SnakkUtils;
})();
