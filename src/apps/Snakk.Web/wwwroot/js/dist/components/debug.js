"use strict";
/**
 * Debug Logger
 * Outputs chronological log of frontend + backend events to browser console.
 * Reads Server-Timing headers from page load and HTMX responses.
 * Dev-only — loaded conditionally in _Layout.cshtml.
 */
(function () {
    'use strict';
    // ========================================================================
    // State
    // ========================================================================
    const htmxStartTimes = new Map();
    let timeOrigin = 0;
    // Buffered entries for deferred output (before + during page load)
    const buffer = [];
    let flushed = false;
    // ========================================================================
    // Console styling
    // ========================================================================
    const snakkBadge = 'background:#1e293b;color:#94a3b8;padding:1px 5px;border-radius:3px;font-weight:bold';
    const sourceBadge = {
        'Snakk.Web': 'background:#3b82f6;color:white;padding:1px 5px;border-radius:3px',
        'Browser': 'background:#8b5cf6;color:white;padding:1px 5px;border-radius:3px',
        'HTMX': 'background:#14b8a6;color:white;padding:1px 5px;border-radius:3px',
        'SignalR': 'background:#f59e0b;color:white;padding:1px 5px;border-radius:3px',
        'Error': 'background:#ef4444;color:white;padding:1px 5px;border-radius:3px',
        'Context': 'background:#4b5563;color:white;padding:1px 5px;border-radius:3px',
    };
    const resetStyle = 'color:inherit';
    const requestStyle = 'color:#f87171'; // red-400
    const responseStyle = 'color:#4ade80'; // green-400
    // ========================================================================
    // Core
    // ========================================================================
    function emit(source, message, timestamp) {
        const isRequest = message.startsWith('\u27A1');
        const isResponse = message.startsWith('\u2B05');
        const msgStyle = isRequest ? requestStyle : isResponse ? responseStyle : resetStyle;
        const timeStr = timestamp < 0 ? '' : `${timestamp}ms`;
        const badge = sourceBadge[source] || sourceBadge['Context'];
        if (source === 'Error') {
            console.error('%cSNAKK%c %c%s%c %s %c%s', snakkBadge, resetStyle, badge, source, resetStyle, timeStr, msgStyle, message);
        }
        else {
            console.debug('%cSNAKK%c %c%s%c %s %c%s', snakkBadge, resetStyle, badge, source, resetStyle, timeStr, msgStyle, message);
        }
    }
    function add(source, message, atTimestamp) {
        const ts = atTimestamp !== undefined ? atTimestamp : Math.round(performance.now() - timeOrigin);
        if (!flushed) {
            buffer.push({ source, message, timestamp: ts });
        }
        else {
            emit(source, message, ts);
        }
    }
    function log(source, message, atTimestamp) {
        add(source, message, atTimestamp);
    }
    function flush() {
        if (flushed)
            return;
        flushed = true;
        // Sort buffered entries chronologically, context first
        buffer.sort((a, b) => a.timestamp - b.timestamp);
        for (const e of buffer) {
            emit(e.source, e.message, e.timestamp);
        }
        buffer.length = 0;
    }
    // ========================================================================
    // Server-Timing parsing
    // ========================================================================
    function parseServerTiming(header) {
        if (!header)
            return [];
        return header.split(',').map(entry => {
            const parts = entry.trim().split(';');
            const name = parts[0]?.trim() ?? '';
            let duration = 0;
            let description = '';
            for (const part of parts.slice(1)) {
                const trimmed = part.trim();
                if (trimmed.startsWith('dur=')) {
                    duration = parseFloat(trimmed.substring(4)) || 0;
                }
                else if (trimmed.startsWith('desc=')) {
                    description = trimmed.substring(5).replace(/^"|"$/g, '');
                }
            }
            let offsetMs = 0;
            const offsetMatch = description.match(/\s@(\d+)$/);
            if (offsetMatch && offsetMatch[1]) {
                offsetMs = parseInt(offsetMatch[1], 10);
            }
            return { name, duration, description, offsetMs };
        });
    }
    function logServerTimingEntry(t, requestBaseMs) {
        const desc = t.description.replace(/\s*@\d+$/, '');
        const label = desc || t.name;
        const startMs = requestBaseMs + t.offsetMs;
        const endMs = startMs + Math.round(t.duration);
        if (t.name === 'api') {
            add('Snakk.Web', `\u27A1 ${label}`, startMs);
            add('Snakk.Web', `\u2B05 ${label} (${Math.round(t.duration)}ms)`, endMs);
        }
        else if (t.name === 'prefetch') {
            add('Snakk.Web', `\u27A1 ${label}`, startMs);
        }
        else {
            add('Snakk.Web', `\u2B05 ${label} (${Math.round(t.duration)}ms)`, startMs);
        }
    }
    // ========================================================================
    // Event Listeners
    // ========================================================================
    function readContextEntries() {
        const el = document.getElementById('snakk-debug-context');
        if (!el)
            return;
        const slug = el.dataset.communitySlug ?? 'null';
        const name = el.dataset.communityName;
        add('Context', `Host: ${el.dataset.host ?? '?'}`, -1);
        add('Context', `Community: ${slug}${name ? ` (${name})` : ''}`, -1);
        add('Context', `IsDefault: ${el.dataset.isDefault ?? '?'}  IsCustomDomain: ${el.dataset.isCustomDomain ?? '?'}`, -1);
        add('Context', `Path: ${el.dataset.path ?? '?'}`, -1);
    }
    // Initial page request (reconstructed)
    add('HTMX', `\u27A1 GET ${window.location.pathname}${window.location.search}`, 0);
    document.addEventListener('DOMContentLoaded', () => {
        readContextEntries();
        add('Browser', 'DOM ready');
    });
    window.addEventListener('load', () => {
        const navEntries = performance.getEntriesByType('navigation');
        if (navEntries.length > 0) {
            const nav = navEntries[0];
            if (nav) {
                const duration = Math.round(nav.responseEnd - nav.requestStart);
                const responseTime = Math.round(nav.responseEnd);
                add('HTMX', `\u2B05 GET ${window.location.pathname}${window.location.search} (${duration}ms)`, responseTime);
            }
        }
        add('Browser', 'Page loaded');
        // Read Server-Timing from initial page navigation
        if (navEntries.length > 0) {
            const nav = navEntries[0];
            if (nav && 'serverTiming' in nav) {
                const timings = nav.serverTiming;
                for (const t of timings) {
                    if (t.name === 'total' || t.name === 'cache')
                        continue;
                    const desc = t.description || t.name;
                    const offsetMatch = desc.match(/\s@(\d+)$/);
                    const offsetMs = offsetMatch && offsetMatch[1] ? parseInt(offsetMatch[1], 10) : 0;
                    logServerTimingEntry({ name: t.name, duration: t.duration, description: desc, offsetMs }, 0);
                }
            }
        }
        // Flush on next frame to capture any pending PerformanceObserver entries
        requestAnimationFrame(() => flush());
    });
    // HTMX events
    let htmxPagePath = null;
    document.addEventListener('htmx:beforeRequest', ((e) => {
        const detail = e.detail;
        const path = detail?.pathInfo?.requestPath || detail?.requestConfig?.path || '?';
        const method = detail?.requestConfig?.verb?.toUpperCase() || 'GET';
        const target = detail?.target;
        if (target?.id === 'main-content') {
            timeOrigin = performance.now();
            htmxPagePath = path;
            // Log context for new page navigation
            console.debug('%cSNAKK%c %c───── page navigation ─────%c', snakkBadge, resetStyle, 'color:#6b7280;font-style:italic', resetStyle);
            readContextEntries();
        }
        htmxStartTimes.set(path, performance.now());
        log('HTMX', `\u27A1 ${method} ${path}`);
    }));
    document.addEventListener('htmx:afterRequest', ((e) => {
        const detail = e.detail;
        const xhr = detail?.xhr;
        if (!xhr)
            return;
        const path = detail?.pathInfo?.requestPath || detail?.requestConfig?.path || '?';
        const startTime = htmxStartTimes.get(path);
        const requestBaseMs = startTime !== undefined ? Math.round(startTime - timeOrigin) : 0;
        const serverTiming = xhr.getResponseHeader('Server-Timing');
        const timings = parseServerTiming(serverTiming);
        for (const t of timings) {
            if (t.name === 'total' || t.name === 'cache')
                continue;
            logServerTimingEntry(t, requestBaseMs);
        }
    }));
    document.addEventListener('htmx:afterSettle', ((e) => {
        const detail = e.detail;
        const path = detail?.pathInfo?.requestPath || detail?.requestConfig?.path || '?';
        const startTime = htmxStartTimes.get(path);
        const duration = startTime ? Math.round(performance.now() - startTime) : 0;
        htmxStartTimes.delete(path);
        const target = detail?.target;
        const cacheDiv = target?.querySelector('[data-cache-source]');
        const cacheSource = cacheDiv?.dataset.cacheSource;
        const cacheTag = cacheSource ? ` [${cacheSource}]` : '';
        if (target?.id === 'main-content' && htmxPagePath === path) {
            htmxPagePath = null;
            log('HTMX', `\u2B05 GET ${path} (${duration}ms)`);
            return;
        }
        log('HTMX', `\u2B05 ${path}${cacheTag} (${duration}ms)`);
    }));
    document.addEventListener('htmx:responseError', ((e) => {
        const detail = e.detail;
        const path = detail?.pathInfo?.requestPath || '?';
        const status = detail?.xhr?.status || '?';
        log('Error', `HTMX ${path} (${status})`);
    }));
    // BFF fetch calls
    if (typeof PerformanceObserver !== 'undefined') {
        const observer = new PerformanceObserver((list) => {
            for (const entry of list.getEntries()) {
                const resource = entry;
                const url = resource.name;
                if (url.includes('/bff/')) {
                    const path = new URL(url).pathname;
                    const duration = Math.round(resource.duration);
                    const startMs = Math.round(resource.startTime - timeOrigin);
                    const endMs = startMs + duration;
                    log('Snakk.Web', `\u27A1 BFF ${path}`, startMs);
                    log('Snakk.Web', `\u2B05 BFF ${path} (${duration}ms)`, endMs);
                }
            }
        });
        observer.observe({ entryTypes: ['resource'] });
    }
    // Export for external use
    window.SnakkDebug = { log };
})();
//# sourceMappingURL=debug.js.map