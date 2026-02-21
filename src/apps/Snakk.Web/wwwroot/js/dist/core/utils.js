"use strict";
/**
 * Snakk Utility Functions
 * Shared helpers for formatting, validation, and DOM manipulation
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    /**
     * Format a date as relative time (e.g., "2m ago", "5h ago", "3d ago")
     */
    function formatRelativeTime(dateString) {
        if (!dateString)
            return '';
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);
        if (diffMins < 1)
            return 'just now';
        if (diffMins < 60)
            return diffMins + 'm ago';
        if (diffHours < 24)
            return diffHours + 'h ago';
        if (diffDays < 7)
            return diffDays + 'd ago';
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }
    /**
     * Format a number with k/M suffix (e.g., 1.2k, 2.5M)
     */
    function formatCount(count) {
        if (count == null)
            return '0';
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
    function escapeHtml(text) {
        if (!text)
            return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    /**
     * Format badges HTML for a discussion (pinned, locked, tags)
     */
    function formatDiscussionBadges(discussion) {
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
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
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
    function throttle(func, limit) {
        let inThrottle = false;
        return function (...args) {
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
    async function copyToClipboard(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        }
        catch (err) {
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
    function truncate(text, maxLength) {
        if (!text || text.length <= maxLength)
            return text;
        return text.substring(0, maxLength).trim() + '...';
    }
    /**
     * Parse query string into object
     */
    function parseQuery(queryString) {
        const params = new URLSearchParams(queryString || window.location.search);
        const result = {};
        for (const [key, value] of params.entries()) {
            result[key] = value;
        }
        return result;
    }
    /**
     * Build query string from object
     */
    function buildQuery(params) {
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
    function isInViewport(element, threshold = 0) {
        const rect = element.getBoundingClientRect();
        return (rect.top >= -threshold &&
            rect.left >= -threshold &&
            rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) + threshold &&
            rect.right <= (window.innerWidth || document.documentElement.clientWidth) + threshold);
    }
    /**
     * Scroll element into view smoothly
     */
    function smoothScrollTo(element, options = {}) {
        const defaultOptions = {
            behavior: 'smooth',
            block: 'center',
            inline: 'nearest'
        };
        element.scrollIntoView({ ...defaultOptions, ...options });
    }
    /**
     * Get element offset from document top
     */
    function getOffsetTop(element) {
        let offsetTop = 0;
        while (element) {
            offsetTop += element.offsetTop;
            element = element.offsetParent;
        }
        return offsetTop;
    }
    /**
     * Validate email format
     */
    function isValidEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }
    /**
     * Validate URL format
     */
    function isValidUrl(url) {
        try {
            new URL(url);
            return true;
        }
        catch {
            return false;
        }
    }
    /**
     * Generate random ID
     */
    function generateId(prefix = 'id') {
        return `${prefix}-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }
    /**
     * Deep clone object (simple version)
     */
    function clone(obj) {
        return JSON.parse(JSON.stringify(obj));
    }
    /**
     * Dispatch custom event
     */
    function dispatchEvent(name, detail = {}) {
        document.dispatchEvent(new CustomEvent(name, { detail }));
    }
    // Export all utilities
    const SnakkUtils = {
        formatRelativeTime,
        formatCount,
        escapeHtml,
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
    window.SnakkUtils = SnakkUtils;
})();
//# sourceMappingURL=utils.js.map