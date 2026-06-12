/**
 * Snakk Service Worker — static asset cache
 *
 * Caching strategy: cache-first for fingerprinted static assets only.
 * Fingerprinted URLs carry a `v` query parameter (asp-append-version).
 * Everything else (HTML, /bff/*, SignalR) falls through to the network
 * untouched — respondWith is never called for those requests.
 *
 * Cache name: snakk-static-v1
 * On activate: delete all caches whose name starts with 'snakk-static-'
 * but is not the current CACHE_NAME.
 */

'use strict';

const CACHE_NAME = 'snakk-static-v1';
const CACHE_PREFIX = 'snakk-static-';

/**
 * Paths whose prefix makes them eligible for caching.
 * Only same-origin GET requests with a `v` query param are cached.
 */
const CACHEABLE_PREFIXES = [
    '/js/vendor/',
    '/js/dist/',
    '/css/',
    '/img/',
    '/avatars/generated/',
];

function isCacheable(request) {
    if (request.method !== 'GET') return false;

    let url;
    try {
        url = new URL(request.url);
    } catch (_) {
        return false;
    }

    // Same-origin only
    if (url.origin !== self.location.origin) return false;

    // Must carry the asp-append-version fingerprint query param
    if (!url.searchParams.has('v')) return false;

    // Path must start with one of the cacheable prefixes
    const pathname = url.pathname;
    for (let i = 0; i < CACHEABLE_PREFIXES.length; i++) {
        if (pathname.startsWith(CACHEABLE_PREFIXES[i])) return true;
    }

    return false;
}

// ── Install ──────────────────────────────────────────────────────────────────

self.addEventListener('install', function (event) {
    // Activate immediately — don't wait for old SW to be released
    self.skipWaiting();
});

// ── Activate ─────────────────────────────────────────────────────────────────

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys().then(function (keys) {
            return Promise.all(
                keys
                    .filter(function (key) {
                        return key.startsWith(CACHE_PREFIX) && key !== CACHE_NAME;
                    })
                    .map(function (key) {
                        return caches.delete(key);
                    })
            );
        }).then(function () {
            // Take control of all open clients immediately
            return self.clients.claim();
        })
    );
});

// ── Fetch ────────────────────────────────────────────────────────────────────

self.addEventListener('fetch', function (event) {
    if (!isCacheable(event.request)) {
        // Fall through — do not call respondWith; browser handles normally
        return;
    }

    event.respondWith(
        caches.open(CACHE_NAME).then(function (cache) {
            return cache.match(event.request).then(function (cached) {
                if (cached) {
                    return cached;
                }

                return fetch(event.request).then(function (response) {
                    // Only cache valid responses
                    if (response && response.ok) {
                        cache.put(event.request, response.clone());
                    }
                    return response;
                });
            });
        }).catch(function () {
            // If anything goes wrong fall back to a plain network request
            return fetch(event.request);
        })
    );
});
