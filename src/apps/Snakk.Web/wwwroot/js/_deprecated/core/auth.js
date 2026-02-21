/**
 * JWT Token Management for Snakk.Web
 * Handles access tokens and refresh tokens with localStorage
 */
(function() {
    'use strict';

    // Constants
    const TOKEN_KEY = 'snakk_jwt_token';
    const REFRESH_TOKEN_KEY = 'snakk_refresh_token';

    /**
     * Set access token
     */
    function setToken(token) {
        if (token) {
            localStorage.setItem(TOKEN_KEY, token);
            dispatchAuthEvent('token-set');
        }
    }

    /**
     * Get access token
     */
    function getToken() {
        return localStorage.getItem(TOKEN_KEY);
    }

    /**
     * Set refresh token
     */
    function setRefreshToken(refreshToken) {
        if (refreshToken) {
            localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
        }
    }

    /**
     * Get refresh token
     */
    function getRefreshToken() {
        return localStorage.getItem(REFRESH_TOKEN_KEY);
    }

    /**
     * Clear all tokens
     */
    function clearToken() {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(REFRESH_TOKEN_KEY);
        dispatchAuthEvent('tokens-cleared');
    }

    /**
     * Parse JWT payload
     */
    function parseJwt(token) {
        try {
            const parts = token.split('.');
            if (parts.length !== 3) {
                throw new Error(`Invalid JWT format - expected 3 parts, got ${parts.length}`);
            }
            return JSON.parse(atob(parts[1]));
        } catch (error) {
            console.error('[Snakk Auth] JWT parse error:', error);
            return null;
        }
    }

    /**
     * Check if token is expired
     */
    function isTokenExpired(token) {
        const payload = parseJwt(token);
        if (!payload || !payload.exp) {
            return true;
        }

        const exp = payload.exp * 1000; // Convert to milliseconds
        const now = Date.now();
        return now >= exp;
    }

    /**
     * Check if user is authenticated
     */
    function isAuthenticated() {
        const token = getToken();
        if (!token) {
            console.log('[Snakk Auth] isAuthenticated: no token found');
            return false;
        }

        const expired = isTokenExpired(token);
        const payload = parseJwt(token);

        if (payload) {
            const exp = payload.exp * 1000;
            const now = Date.now();
            const minutesRemaining = Math.round((exp - now) / 1000 / 60);

            console.log('[Snakk Auth] Token validation:', {
                exp: new Date(exp).toISOString(),
                now: new Date(now).toISOString(),
                isValid: !expired,
                timeUntilExpiry: !expired ? `${minutesRemaining} minutes` : 'expired'
            });
        }

        return !expired;
    }

    /**
     * Get authorization headers
     */
    function getAuthHeaders() {
        const token = getToken();
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    }

    /**
     * Get CSRF token from meta tag
     */
    function getCSRFToken() {
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content');
    }

    /**
     * Logout user
     */
    function logout() {
        clearToken();
        dispatchAuthEvent('logged-out');
        window.location.href = '/';
    }

    /**
     * Dispatch custom auth event
     */
    function dispatchAuthEvent(type, detail = {}) {
        document.dispatchEvent(new CustomEvent('snakk:auth:' + type, { detail }));
    }

    // Token storage API
    window.snakkAuth = {
        setToken,
        getToken,
        setRefreshToken,
        getRefreshToken,
        clearToken,
        isAuthenticated,
        getAuthHeaders,
        getCSRFToken,
        logout,
        parseJwt,
        isTokenExpired
    };

    // Check URL for legacy token in query parameter (email/password login)
    function checkUrlForToken() {
        const urlParams = new URLSearchParams(window.location.search);
        const tokenFromUrl = urlParams.get('token');

        if (tokenFromUrl) {
            console.log('[Snakk Auth] Legacy token detected in URL, storing...');
            setToken(tokenFromUrl);

            // Remove token from URL
            const newUrl = window.location.pathname +
                (window.location.search.replace(/[?&]token=[^&]+/, '').replace(/^&/, '?') || '');
            window.history.replaceState({}, document.title, newUrl);

            console.log('[Snakk Auth] Token stored and removed from URL');
        }
    }

    // Override fetch to automatically include JWT token and CSRF token for BFF calls
    function setupFetchInterceptor() {
        const originalFetch = window.fetch;

        window.fetch = function(url, options = {}) {
            const urlString = typeof url === 'string' ? url : url.url;

            // JavaScript ONLY calls /bff/* endpoints (Backend-for-Frontend)
            // The BFF layer (ASP.NET) then calls the internal API
            const isBffCall = urlString.includes('/bff/');

            if (isBffCall) {
                const method = (options.method || 'GET').toUpperCase();
                const headers = {
                    ...options.headers,
                    ...getAuthHeaders() // JWT token for BFF authentication
                };

                // Add CSRF token for state-changing operations (required for cookie-based sessions)
                if (['POST', 'PUT', 'DELETE', 'PATCH'].includes(method)) {
                    const csrfToken = getCSRFToken();
                    if (csrfToken) {
                        headers['RequestVerificationToken'] = csrfToken;
                        headers['X-CSRF-TOKEN'] = csrfToken;
                    }
                }

                options.headers = headers;
            }

            return originalFetch(url, options);
        };

        console.log('[Snakk Auth] Fetch interceptor installed for BFF endpoints');
    }

    // Log authentication state on page load
    function logAuthState() {
        const token = getToken();
        const refreshToken = getRefreshToken();

        console.log('[Snakk Auth] Page loaded. LocalStorage contents:');
        console.log('[Snakk Auth] - JWT token:', token ? token.substring(0, 50) + '...' : 'null');
        console.log('[Snakk Auth] - Refresh token:', refreshToken ? refreshToken.substring(0, 50) + '...' : 'null');
        console.log('[Snakk Auth] - Authenticated:', isAuthenticated());
        console.log('[Snakk Auth] - Has token:', !!token);
    }

    // Initialize
    checkUrlForToken();
    setupFetchInterceptor();
    logAuthState();

    // Dispatch ready event
    dispatchAuthEvent('ready');
})();

