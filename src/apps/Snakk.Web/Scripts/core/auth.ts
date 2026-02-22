/**
 * Authentication helpers for Snakk.Web
 * Cookie-only auth: server manages HttpOnly cookies, JS only handles CSRF.
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface SnakkAuthAPI {
    getCSRFToken(): string | null;
    logout(): void;
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    /**
     * Get CSRF token from meta tag
     */
    function getCSRFToken(): string | null {
        const metaTag = document.querySelector<HTMLMetaElement>('meta[name="csrf-token"]');
        return metaTag?.getAttribute('content') ?? null;
    }

    /**
     * Logout user
     */
    function logout(): void {
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
    function dispatchAuthEvent(type: string, detail: Record<string, any> = {}): void {
        document.dispatchEvent(new CustomEvent('snakk:auth:' + type, { detail }));
    }

    // Override fetch to automatically include CSRF token for BFF calls
    function setupFetchInterceptor(): void {
        const originalFetch = window.fetch;

        window.fetch = function(url: RequestInfo | URL, options: RequestInit = {}): Promise<Response> {
            const urlString = typeof url === 'string' ? url : url instanceof URL ? url.toString() : url.url;

            // JavaScript ONLY calls /bff/* endpoints (Backend-for-Frontend)
            const isBffCall = urlString.includes('/bff/');

            if (isBffCall) {
                const method = (options.method || 'GET').toUpperCase();
                const headers: Record<string, string> = {
                    ...(options.headers as Record<string, string> || {})
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
    const SnakkAuth: SnakkAuthAPI = {
        getCSRFToken,
        logout
    };

    (window as any).SnakkAuth = SnakkAuth;
    // Backwards compat alias (lowercase)
    (window as any).snakkAuth = SnakkAuth;

    // Initialize
    setupFetchInterceptor();

    // Dispatch ready event
    dispatchAuthEvent('ready');
})();
