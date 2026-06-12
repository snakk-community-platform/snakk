/**
 * Web Vitals RUM (Real User Monitoring)
 * Collects LCP, CLS, INP, TTFB via PerformanceObserver and sends one beacon
 * to /bff/rum on page hide/unload.
 *
 * Sampling: 25% of page loads (decided once at script load).
 * No external dependencies — hand-rolled collectors only.
 */

(function (): void {
    'use strict';

    // ── Sampling gate — bail early for the 75% we don't sample ──────────────
    if (Math.random() >= 0.25) return;

    // ── Feature detection ────────────────────────────────────────────────────
    if (typeof PerformanceObserver === 'undefined') return;
    if (!('supportedEntryTypes' in PerformanceObserver)) return;

    // ── State ────────────────────────────────────────────────────────────────
    let lcp: number | undefined;
    let cls: number | undefined;
    let inp: number | undefined;
    let ttfb: number | undefined;
    let htmxNav = false;
    let reported = false;

    // ── TTFB (cheap, no observer needed) ────────────────────────────────────
    try {
        const navEntry = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
        if (navEntry) {
            ttfb = navEntry.responseStart;
        }
    } catch (_) { /* ignore */ }

    // ── LCP ──────────────────────────────────────────────────────────────────
    try {
        if ((PerformanceObserver.supportedEntryTypes as string[]).includes('largest-contentful-paint')) {
            const lcpObs = new PerformanceObserver((list) => {
                try {
                    const entries = list.getEntries();
                    if (entries.length > 0) {
                        // Keep latest entry (spec: last entry is the best candidate)
                        lcp = (entries[entries.length - 1] as any).startTime as number;
                    }
                } catch (_) { /* ignore */ }
            });
            lcpObs.observe({ type: 'largest-contentful-paint', buffered: true });
        }
    } catch (_) { /* ignore */ }

    // ── CLS — session-window algorithm ──────────────────────────────────────
    try {
        if ((PerformanceObserver.supportedEntryTypes as string[]).includes('layout-shift')) {
            let sessionValue = 0;
            let sessionMaxValue = 0;
            let sessionStartTime = 0;
            let lastEntryTime = 0;

            const clsObs = new PerformanceObserver((list) => {
                try {
                    for (const entry of list.getEntries()) {
                        const e = entry as any;
                        // Skip entries caused by recent user input
                        if (e.hadRecentInput) continue;

                        const now: number = e.startTime;

                        // Start new session if gap > 1s or window > 5s
                        if (now - lastEntryTime > 1000 || now - sessionStartTime > 5000) {
                            sessionValue = 0;
                            sessionStartTime = now;
                        }

                        lastEntryTime = now;
                        sessionValue += e.value as number;

                        if (sessionValue > sessionMaxValue) {
                            sessionMaxValue = sessionValue;
                            cls = sessionMaxValue;
                        }
                    }
                } catch (_) { /* ignore */ }
            });
            clsObs.observe({ type: 'layout-shift', buffered: true });
        }
    } catch (_) { /* ignore */ }

    // ── INP — keep the single largest event duration ─────────────────────────
    try {
        if ((PerformanceObserver.supportedEntryTypes as string[]).includes('event')) {
            const inpObs = new PerformanceObserver((list) => {
                try {
                    for (const entry of list.getEntries()) {
                        const duration = (entry as any).duration as number;
                        if (inp === undefined || duration > inp) {
                            inp = duration;
                        }
                    }
                } catch (_) { /* ignore */ }
            });
            inpObs.observe({ type: 'event', buffered: true, durationThreshold: 40 } as any);
        }
    } catch (_) { /* ignore */ }

    // ── HTMX navigation detection ────────────────────────────────────────────
    try {
        document.addEventListener('htmx:pushedIntoHistory', () => { htmxNav = true; }, { passive: true });
        document.addEventListener('htmx:replacedInHistory', () => { htmxNav = true; }, { passive: true });
    } catch (_) { /* ignore */ }

    // ── Beacon on page hide ───────────────────────────────────────────────────
    function sendBeacon(): void {
        if (reported) return;
        reported = true;

        try {
            if (!navigator.sendBeacon) return;

            // Build metrics object — only include values that were observed
            const metrics: Record<string, number> = {};
            if (lcp !== undefined) metrics['lcp'] = Math.round(lcp);
            if (cls !== undefined) metrics['cls'] = Math.round(cls * 10000) / 10000; // 4 decimal places
            if (inp !== undefined) metrics['inp'] = Math.round(inp);
            if (ttfb !== undefined) metrics['ttfb'] = Math.round(ttfb);

            if (Object.keys(metrics).length === 0) return;

            const payload: Record<string, unknown> = {
                metrics,
                path: location.pathname,
                htmxNav,
            };

            // Connection type if available
            try {
                const conn = (navigator as any).connection;
                if (conn && conn.effectiveType) {
                    payload['effectiveType'] = conn.effectiveType as string;
                }
            } catch (_) { /* ignore */ }

            navigator.sendBeacon('/bff/rum', JSON.stringify(payload));
        } catch (_) { /* ignore */ }
    }

    // Finalize LCP before sending (stop observing so the last entry is settled)
    function finalizeAndSend(): void {
        sendBeacon();
    }

    try {
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'hidden') {
                finalizeAndSend();
            }
        }, { passive: true });

        window.addEventListener('pagehide', finalizeAndSend, { passive: true });
    } catch (_) { /* ignore */ }
})();
