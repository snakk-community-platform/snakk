"use strict";
/**
 * Authentication helpers for Snakk.Web
 * Cookie-only auth: server manages HttpOnly cookies, JS only handles CSRF.
 */
// ============================================================================
// Implementation
// ============================================================================
(function () {
    'use strict';
    /**
     * Get CSRF token from meta tag
     */
    function getCSRFToken() {
        const metaTag = document.querySelector('meta[name="csrf-token"]');
        return metaTag?.getAttribute('content') ?? null;
    }
    /**
     * Logout user
     */
    function logout() {
        fetch('/bff/auth/logout', {
            method: 'POST',
            credentials: 'include'
        }).finally(() => {
            window.location.href = '/';
        });
    }
    /**
     * Dispatch custom auth event
     */
    function dispatchAuthEvent(type, detail = {}) {
        document.dispatchEvent(new CustomEvent('snakk:auth:' + type, { detail }));
    }
    // Override fetch to automatically include CSRF token for BFF calls
    function setupFetchInterceptor() {
        const originalFetch = window.fetch;
        window.fetch = function (url, options = {}) {
            const urlString = typeof url === 'string' ? url : url instanceof URL ? url.toString() : url.url;
            // JavaScript ONLY calls /bff/* endpoints (Backend-for-Frontend)
            const isBffCall = urlString.includes('/bff/');
            if (isBffCall) {
                const method = (options.method || 'GET').toUpperCase();
                const headers = {
                    ...(options.headers || {})
                };
                // Add CSRF token for state-changing operations
                if (['POST', 'PUT', 'DELETE', 'PATCH'].includes(method)) {
                    const csrfToken = getCSRFToken();
                    if (csrfToken) {
                        headers['RequestVerificationToken'] = csrfToken;
                        headers['X-CSRF-TOKEN'] = csrfToken;
                    }
                }
                options.headers = headers;
                // Ensure cookies are sent with every BFF request
                options.credentials = 'include';
            }
            return originalFetch(url, options);
        };
    }
    // Export API to window
    const SnakkAuth = {
        getCSRFToken,
        logout
    };
    window.SnakkAuth = SnakkAuth;
    // Backwards compat alias (lowercase)
    window.snakkAuth = SnakkAuth;
    // Initialize
    setupFetchInterceptor();
    // Dispatch ready event
    dispatchAuthEvent('ready');
})();
//# sourceMappingURL=auth.js.map