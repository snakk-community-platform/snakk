/**
 * Service Worker Registration
 * Registers /sw.js with scope '/' on window load via requestIdleCallback
 * (or setTimeout fallback). Errors are swallowed silently — the SW is a
 * progressive enhancement; failure must never break the page.
 */

(function (): void {
    'use strict';

    if (!('serviceWorker' in navigator)) return;

    function register(): void {
        navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function () {
            // Silently swallow — SW is a non-critical enhancement
        });
    }

    function scheduleRegister(): void {
        if ('requestIdleCallback' in window) {
            (window as any).requestIdleCallback(register);
        } else {
            setTimeout(register, 200);
        }
    }

    if (document.readyState === 'complete') {
        scheduleRegister();
    } else {
        window.addEventListener('load', scheduleRegister, { once: true, passive: true });
    }
})();
