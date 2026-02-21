"use strict";
/**
 * Automatic Token Refresh for Snakk.Web
 * Proactively refreshes JWT tokens before they expire
 * Ported from AdminWeb auth-check.js
 */
(function () {
    'use strict';
    // Configuration
    const CHECK_INTERVAL = 5 * 60 * 1000; // 5 minutes
    const INITIAL_DELAY = 60 * 1000; // 1 minute
    const REFRESH_THRESHOLD_MINUTES = 5; // Refresh if less than 5 minutes remaining
    let checkTimer = null;
    let isRefreshing = false;
    /**
     * Check token expiration and refresh if needed
     */
    async function checkAndRefreshToken() {
        try {
            // Skip if already refreshing
            if (isRefreshing) {
                console.log('[Token Refresh] Already refreshing, skipping...');
                return;
            }
            const token = window.SnakkAuth?.getToken();
            if (!token) {
                console.log('[Token Refresh] No token found, skipping check');
                return;
            }
            // Check if token is expiring soon
            const shouldRefresh = isTokenExpiringSoon(token, REFRESH_THRESHOLD_MINUTES);
            if (!shouldRefresh) {
                const payload = window.SnakkAuth?.parseJwt(token);
                if (payload?.exp) {
                    const exp = payload.exp * 1000;
                    const minutesRemaining = Math.round((exp - Date.now()) / 1000 / 60);
                    console.log(`[Token Refresh] Token valid for ${minutesRemaining} more minutes`);
                }
                dispatchTokenEvent('valid');
                return;
            }
            // Token is expiring soon - refresh it
            console.log('[Token Refresh] Token expiring soon, refreshing...');
            isRefreshing = true;
            const success = await window.SnakkAuth?.refreshTokens();
            if (success) {
                console.log('[Token Refresh] Tokens refreshed successfully');
                dispatchTokenEvent('refreshed');
            }
            else {
                console.error('[Token Refresh] Token refresh failed');
                dispatchTokenEvent('refresh-failed');
                // Clear timer and redirect to login after delay
                if (checkTimer) {
                    clearInterval(checkTimer);
                    checkTimer = null;
                }
                setTimeout(() => {
                    console.log('[Token Refresh] Redirecting to login due to refresh failure');
                    window.location.href = '/auth/login?sessionExpired=true';
                }, 100);
            }
        }
        catch (error) {
            console.error('[Token Refresh] Check error:', error);
            dispatchTokenEvent('error', { error: error.message });
        }
        finally {
            isRefreshing = false;
        }
    }
    /**
     * Check if token is expiring soon
     */
    function isTokenExpiringSoon(token, thresholdMinutes) {
        try {
            const payload = window.SnakkAuth?.parseJwt(token);
            if (!payload || !payload.exp) {
                console.warn('[Token Refresh] Invalid token payload');
                return true;
            }
            const exp = payload.exp * 1000; // Convert to milliseconds
            const now = Date.now();
            const minutesRemaining = (exp - now) / 1000 / 60;
            return minutesRemaining < thresholdMinutes;
        }
        catch (error) {
            console.error('[Token Refresh] Error checking token expiration:', error);
            return true;
        }
    }
    /**
     * Dispatch custom event for token state changes
     */
    function dispatchTokenEvent(type, detail = {}) {
        document.dispatchEvent(new CustomEvent('snakk:auth:token-' + type, {
            detail: detail
        }));
    }
    /**
     * Start automatic token checking
     */
    function startTokenRefresh() {
        // Initial check after delay
        setTimeout(checkAndRefreshToken, INITIAL_DELAY);
        // Then check at regular intervals
        checkTimer = window.setInterval(checkAndRefreshToken, CHECK_INTERVAL);
        console.log('[Token Refresh] Automatic token refresh started');
        console.log(`[Token Refresh] Will check every ${CHECK_INTERVAL / 60000} minutes`);
        console.log(`[Token Refresh] Will refresh if < ${REFRESH_THRESHOLD_MINUTES} minutes remaining`);
        dispatchTokenEvent('started');
    }
    /**
     * Stop automatic token checking
     */
    function stopTokenRefresh() {
        if (checkTimer) {
            clearInterval(checkTimer);
            checkTimer = null;
            console.log('[Token Refresh] Automatic token refresh stopped');
            dispatchTokenEvent('stopped');
        }
    }
    /**
     * Handle page visibility changes
     */
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            // Page became visible - check immediately
            console.log('[Token Refresh] Page visible, checking token...');
            checkAndRefreshToken();
        }
    });
    /**
     * Listen for manual check requests
     */
    document.addEventListener('snakk:auth:check-token', () => {
        console.log('[Token Refresh] Manual token check requested');
        checkAndRefreshToken();
    });
    /**
     * Listen for logout events to stop refresh timer
     */
    document.addEventListener('snakk:auth:logged-out', () => {
        console.log('[Token Refresh] User logged out, stopping refresh timer');
        stopTokenRefresh();
    });
    /**
     * Listen for new token events to reset timer
     */
    document.addEventListener('snakk:auth:token-set', () => {
        console.log('[Token Refresh] New token detected, resetting timer');
        // If timer wasn't running, start it
        if (!checkTimer) {
            startTokenRefresh();
        }
    });
    // Initialize on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            // Only start if user has a token
            const token = window.SnakkAuth?.getToken();
            if (token) {
                startTokenRefresh();
            }
            else {
                console.log('[Token Refresh] No token found on page load, not starting');
            }
        });
    }
    else {
        // DOM already loaded
        const token = window.SnakkAuth?.getToken();
        if (token) {
            startTokenRefresh();
        }
        else {
            console.log('[Token Refresh] No token found on page load, not starting');
        }
    }
    // Export API for manual control if needed
    window.SnakkTokenRefresh = {
        checkAndRefreshToken,
        startTokenRefresh,
        stopTokenRefresh,
        isRefreshing: () => isRefreshing
    };
    // Dispatch ready event
    dispatchTokenEvent('ready');
})();
//# sourceMappingURL=token-refresh.js.map