// JWT Token Management
(function() {
    'use strict';

    const TOKEN_KEY = 'snakk_jwt_token';
    const REFRESH_TOKEN_KEY = 'snakk_refresh_token';

    // Token storage
    window.snakkAuth = {
        setToken: function(token) {
            if (token) {
                localStorage.setItem(TOKEN_KEY, token);
            }
        },

        getToken: function() {
            return localStorage.getItem(TOKEN_KEY);
        },

        setRefreshToken: function(refreshToken) {
            if (refreshToken) {
                localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
            }
        },

        getRefreshToken: function() {
            return localStorage.getItem(REFRESH_TOKEN_KEY);
        },

        clearToken: function() {
            localStorage.removeItem(TOKEN_KEY);
            localStorage.removeItem(REFRESH_TOKEN_KEY);
        },

        isAuthenticated: function() {
            const token = this.getToken();
            if (!token) {
                console.log('[Snakk Auth] isAuthenticated: no token found');
                return false;
            }

            // Check if token is expired
            try {
                const parts = token.split('.');
                if (parts.length !== 3) {
                    console.error('[Snakk Auth] Invalid JWT format - expected 3 parts, got:', parts.length);
                    return false;
                }

                const payload = JSON.parse(atob(parts[1]));
                const exp = payload.exp * 1000; // Convert to milliseconds
                const now = Date.now();
                const isValid = now < exp;

                console.log('[Snakk Auth] Token validation:', {
                    exp: new Date(exp).toISOString(),
                    now: new Date(now).toISOString(),
                    isValid: isValid,
                    timeUntilExpiry: isValid ? `${Math.round((exp - now) / 1000 / 60)} minutes` : 'expired'
                });

                return isValid;
            } catch (e) {
                console.error('[Snakk Auth] Token validation error:', e);
                return false;
            }
        },

        getAuthHeaders: function() {
            const token = this.getToken();
            if (token) {
                return {
                    'Authorization': `Bearer ${token}`
                };
            }
            return {};
        },

        logout: function() {
            this.clearToken();
            window.location.href = '/';
        }
    };

    // Check URL for legacy token in query parameter (email/password login)
    const urlParams = new URLSearchParams(window.location.search);
    const tokenFromUrl = urlParams.get('token');

    if (tokenFromUrl) {
        console.log('[Snakk Auth] Legacy token detected in URL, storing...');
        window.snakkAuth.setToken(tokenFromUrl);
        // Remove token from URL
        window.history.replaceState({}, document.title, window.location.pathname);
        console.log('[Snakk Auth] Token stored and removed from URL');
    }

    // Override fetch to automatically include JWT token
    const originalFetch = window.fetch;
    window.fetch = function(url, options = {}) {
        // Only add auth header for API calls
        if (typeof url === 'string' && (url.includes('/api/') || url.startsWith('https://localhost:7291'))) {
            options.headers = {
                ...options.headers,
                ...window.snakkAuth.getAuthHeaders()
            };
        }
        return originalFetch(url, options);
    };

    // Log authentication state on page load
    const tokenFromStorage = localStorage.getItem('snakk_jwt_token');
    const refreshFromStorage = localStorage.getItem('snakk_refresh_token');
    console.log('[Snakk Auth] Page loaded. LocalStorage contents:');
    console.log('[Snakk Auth] - JWT token:', tokenFromStorage ? tokenFromStorage.substring(0, 50) + '...' : 'null');
    console.log('[Snakk Auth] - Refresh token:', refreshFromStorage ? refreshFromStorage.substring(0, 50) + '...' : 'null');
    console.log('[Snakk Auth] - Authenticated:', window.snakkAuth.isAuthenticated());
    console.log('[Snakk Auth] - Has token:', !!window.snakkAuth.getToken());
})();
