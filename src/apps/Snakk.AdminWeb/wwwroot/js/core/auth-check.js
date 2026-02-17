/**
 * Proactive session check and token refresh for AdminWeb
 * Checks authentication status every 5 minutes and refreshes tokens when needed
 */
(function() {
    'use strict';

    // Configuration
    const CHECK_INTERVAL = 5 * 60 * 1000; // 5 minutes
    const INITIAL_DELAY = 60 * 1000; // 1 minute

    let checkTimer = null;

    /**
     * Check session status and refresh token if needed
     */
    async function checkSession() {
        try {
            const response = await fetch('/Auth/CheckSession', {
                method: 'GET',
                credentials: 'include',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                console.warn('[Auth] Session check failed:', response.status);
                dispatchSessionEvent('check-failed', { status: response.status });
                return;
            }

            const data = await response.json();

            if (!data.authenticated) {
                // Session expired - dispatch event and redirect
                console.log('[Auth] Session expired, redirecting to login...');
                dispatchSessionEvent('expired', data);

                // Clear any timers
                if (checkTimer) {
                    clearInterval(checkTimer);
                    checkTimer = null;
                }

                // Redirect after a short delay to allow handlers to run
                setTimeout(() => {
                    window.location.href = '/Auth/Login?sessionExpired=true';
                }, 100);
            } else if (data.refreshed) {
                console.log('[Auth] Access token refreshed proactively');
                dispatchSessionEvent('refreshed', data);
            } else {
                dispatchSessionEvent('valid', data);
            }
        } catch (error) {
            console.error('[Auth] Session check error:', error);
            dispatchSessionEvent('error', { error: error.message });
        }
    }

    /**
     * Dispatch custom event for session state changes
     */
    function dispatchSessionEvent(type, detail) {
        document.dispatchEvent(new CustomEvent('admin:session:' + type, {
            detail: detail || {}
        }));
    }

    /**
     * Start session checking
     */
    function startSessionCheck() {
        // Initial check after delay
        setTimeout(checkSession, INITIAL_DELAY);

        // Then check at regular intervals
        checkTimer = setInterval(checkSession, CHECK_INTERVAL);

        console.log('[Auth] Session checking started');
        dispatchSessionEvent('started');
    }

    /**
     * Stop session checking
     */
    function stopSessionCheck() {
        if (checkTimer) {
            clearInterval(checkTimer);
            checkTimer = null;
            console.log('[Auth] Session checking stopped');
            dispatchSessionEvent('stopped');
        }
    }

    // Handle page visibility changes
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            // Page became visible - check immediately
            console.log('[Auth] Page visible, checking session...');
            checkSession();
        }
    });

    // Listen for manual check requests
    document.addEventListener('admin:session:check', () => {
        console.log('[Auth] Manual session check requested');
        checkSession();
    });

    // Initialize on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startSessionCheck);
    } else {
        startSessionCheck();
    }

    // Export API for manual control if needed
    window.AdminAuth = {
        checkSession,
        startSessionCheck,
        stopSessionCheck
    };

    // Dispatch ready event
    dispatchSessionEvent('ready');
})();
