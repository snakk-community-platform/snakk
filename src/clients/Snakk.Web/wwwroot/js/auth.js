// JWT Token Management
(function() {
    'use strict';

    const TOKEN_KEY = 'snakk_jwt_token';

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

        clearToken: function() {
            localStorage.removeItem(TOKEN_KEY);
        },

        isAuthenticated: function() {
            const token = this.getToken();
            if (!token) return false;

            // Check if token is expired
            try {
                const payload = JSON.parse(atob(token.split('.')[1]));
                const exp = payload.exp * 1000; // Convert to milliseconds
                return Date.now() < exp;
            } catch (e) {
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

    // Check URL for token parameter (from OAuth redirect)
    const urlParams = new URLSearchParams(window.location.search);
    const tokenFromUrl = urlParams.get('token');
    if (tokenFromUrl) {
        window.snakkAuth.setToken(tokenFromUrl);
        // Remove token from URL
        const newUrl = window.location.pathname;
        window.history.replaceState({}, document.title, newUrl);
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
})();
