/**
 * JWT Token Management for Snakk.Web
 * Handles access tokens and refresh tokens with localStorage
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface JwtPayload {
    sub: string;
    name?: string;
    email?: string;
    role?: string | string[];
    exp: number;
    iat: number;
    nbf?: number;
    [key: string]: any;
}

interface AuthEventDetail {
    userId?: string;
    displayName?: string;
    [key: string]: any;
}

interface SnakkAuthAPI {
    setToken(token: string): void;
    getToken(): string | null;
    setRefreshToken(refreshToken: string): void;
    getRefreshToken(): string | null;
    refreshTokens(): Promise<boolean>;
    clearToken(): void;
    isAuthenticated(): boolean;
    getAuthHeaders(): Record<string, string>;
    getCSRFToken(): string | null;
    logout(): void;
    parseJwt(token: string): JwtPayload | null;
    isTokenExpired(token: string): boolean;
}

// ============================================================================
// Implementation
// ============================================================================

(function(): void {
    'use strict';

    // Constants
    const TOKEN_KEY = 'snakk_jwt_token';
    const REFRESH_TOKEN_KEY = 'snakk_refresh_token';

    /**
     * Set access token
     */
    function setToken(token: string): void {
        if (token) {
            localStorage.setItem(TOKEN_KEY, token);
            dispatchAuthEvent('token-set');
        }
    }

    /**
     * Get access token
     */
    function getToken(): string | null {
        return localStorage.getItem(TOKEN_KEY);
    }

    /**
     * Set refresh token
     */
    function setRefreshToken(refreshToken: string): void {
        if (refreshToken) {
            localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
        }
    }

    /**
     * Get refresh token
     */
    function getRefreshToken(): string | null {
        return localStorage.getItem(REFRESH_TOKEN_KEY);
    }

    /**
     * Clear all tokens
     */
    function clearToken(): void {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(REFRESH_TOKEN_KEY);
        dispatchAuthEvent('tokens-cleared');
    }

    /**
     * Parse JWT payload
     */
    function parseJwt(token: string): JwtPayload | null {
        try {
            const parts = token.split('.');
            if (parts.length !== 3 || !parts[1]) {
                throw new Error(`Invalid JWT format - expected 3 parts, got ${parts.length}`);
            }
            return JSON.parse(atob(parts[1])) as JwtPayload;
        } catch (error) {
            console.error('[Snakk Auth] JWT parse error:', error);
            return null;
        }
    }

    /**
     * Check if token is expired
     */
    function isTokenExpired(token: string): boolean {
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
    function isAuthenticated(): boolean {
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
    function getAuthHeaders(): Record<string, string> {
        const token = getToken();
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    }

    /**
     * Get CSRF token from meta tag
     */
    function getCSRFToken(): string | null {
        const metaTag = document.querySelector<HTMLMetaElement>('meta[name="csrf-token"]');
        return metaTag?.getAttribute('content') ?? null;
    }

    /**
     * Refresh access token using refresh token
     */
    async function refreshTokens(): Promise<boolean> {
        const refreshToken = getRefreshToken();
        if (!refreshToken) {
            console.warn('[Snakk Auth] No refresh token available');
            return false;
        }

        try {
            console.log('[Snakk Auth] Refreshing tokens...');
            const response = await fetch('/bff/auth/refresh', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ refreshToken })
            });

            if (!response.ok) {
                console.error('[Snakk Auth] Token refresh failed:', response.status);
                clearToken();
                dispatchAuthEvent('refresh-failed');
                return false;
            }

            const data = await response.json();
            if (data.accessToken && data.refreshToken) {
                setToken(data.accessToken);
                setRefreshToken(data.refreshToken);
                dispatchAuthEvent('tokens-refreshed');
                console.log('[Snakk Auth] Tokens refreshed successfully');
                return true;
            }

            console.error('[Snakk Auth] Token refresh response missing tokens');
            return false;
        } catch (error) {
            console.error('[Snakk Auth] Token refresh error:', error);
            clearToken();
            dispatchAuthEvent('refresh-error', { error });
            return false;
        }
    }

    /**
     * Logout user
     */
    function logout(): void {
        clearToken();
        dispatchAuthEvent('logged-out');
        window.location.href = '/';
    }

    /**
     * Dispatch custom auth event
     */
    function dispatchAuthEvent(type: string, detail: AuthEventDetail = {}): void {
        document.dispatchEvent(new CustomEvent('snakk:auth:' + type, { detail }));
    }

    // Export API to window
    const SnakkAuth: SnakkAuthAPI = {
        setToken,
        getToken,
        setRefreshToken,
        getRefreshToken,
        refreshTokens,
        clearToken,
        isAuthenticated,
        getAuthHeaders,
        getCSRFToken,
        logout,
        parseJwt,
        isTokenExpired
    };

    (window as any).SnakkAuth = SnakkAuth;

    // Check URL for legacy token in query parameter (email/password login)
    function checkUrlForToken(): void {
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
    function setupFetchInterceptor(): void {
        const originalFetch = window.fetch;

        window.fetch = function(url: RequestInfo | URL, options: RequestInit = {}): Promise<Response> {
            const urlString = typeof url === 'string' ? url : url instanceof URL ? url.toString() : url.url;

            // JavaScript ONLY calls /bff/* endpoints (Backend-for-Frontend)
            // The BFF layer (ASP.NET) then calls the internal API
            const isBffCall = urlString.includes('/bff/');

            if (isBffCall) {
                const method = (options.method || 'GET').toUpperCase();
                const headers: Record<string, string> = {
                    ...(options.headers as Record<string, string> || {}),
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
    function logAuthState(): void {
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
