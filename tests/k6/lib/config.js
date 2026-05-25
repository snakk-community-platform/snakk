// Shared configuration for all k6 scenarios.
//
// Override via env vars when running:
//   docker compose run --rm \
//     -e BASE_URL=http://gateway:17030 \
//     -e USER_EMAIL=test@snakk.dev \
//     k6 run /scripts/scenarios/load.js

export const BASE_URL = __ENV.BASE_URL || 'http://snakk:17000';
export const AUTH_BASE_URL = __ENV.AUTH_BASE_URL || `${BASE_URL}/auth`;

// Seeded test users from src/tools/Snakk.DbSeeder/Services/DatabaseSeeder.cs
// (EnsureTestUserExistsAsync). All share password "test123!".
export const TEST_USERS = [
    { email: 'test@snakk.dev',      name: 'Test User', password: 'test123!' },
    { email: 'alice@snakk.local',   name: 'Alice',     password: 'test123!' },
    { email: 'bob@snakk.local',     name: 'Bob',       password: 'test123!' },
    { email: 'charlie@snakk.local', name: 'Charlie',   password: 'test123!' },
    { email: 'dave@snakk.local',    name: 'Dave',      password: 'test123!' },
    { email: 'eve@snakk.local',     name: 'Eve',       password: 'test123!' },
    { email: 'frank@snakk.local',   name: 'Frank',     password: 'test123!' },
];

// Seeded community/hub/space slugs.
// The DEFAULT_COMMUNITY is treated as "the implicit community" by the
// gateway — paths under it skip the `/c/{slug}` prefix (so `/c/main/h/X`
// 301-redirects to `/h/X`). Multi-community paths use the `/c/` form.
export const COMMUNITIES = ['main', 'snakk', 'test1', 'test2', 'test3', 'cooking', 'fitness'];
export const DEFAULT_COMMUNITY = 'main';

// Known hub+space pairs in the default community, discovered from the
// homepage's link inventory. Conservative — only seeded pairs we've verified
// resolve to 200. Run `tests/k6/scenarios/probe.js` to refresh this list
// after seed changes (TODO).
export const DEFAULT_HUB_SPACES = [
    { hub: 'technology', space: 'web-dev' },
];

// Listing pages that don't require a hub/space (homepage feeds).
export const LISTING_PATHS = ['/', '/trending', '/top', '/latest'];

// Common search terms — match seeded content so we get realistic result counts
// rather than always-empty pages (which would skew p95 toward 1ms).
export const SEARCH_TERMS = [
    'fitness', 'cooking', 'discussion', 'reply', 'post', 'general',
    'community', 'hub', 'space', 'user', 'comment', 'recipe', 'workout'
];

export function pickRandom(list) {
    return list[Math.floor(Math.random() * list.length)];
}

// Common HTTP options. Sensible user-agent so the access log distinguishes
// synthetic load from real browser traffic. `Accept-Encoding: identity`
// works around a known gateway-side issue where chunked + gzipped responses
// get RST mid-stream after the body is fully written — uncompressed bodies
// are delivered before the reset hits.
//
// See OBSERVABILITY-TODO §Known gateway issues (the "Connection: close + RST"
// bug surfaced by k6).
export const httpOptions = {
    redirects: 5,
    headers: {
        'User-Agent': 'k6-snakk-loadtest/1.0',
        'Accept': 'text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8',
        'Accept-Language': 'en-US,en;q=0.9',
        'Accept-Encoding': 'identity',
    },
};

// k6 reports `status === 0` when the server sends a clean Connection: close
// followed by an RST, even if the full body was received. This helper treats
// "non-empty body of expected content" as success regardless of the wire-level
// reset, so checks reflect *application* correctness rather than transport
// quirks. Pass a sentinel substring expected in the response body (e.g.
// '<!DOCTYPE' for HTML pages, '{' for JSON endpoints).
export function ok(response, sentinel = null) {
    if (response.status >= 200 && response.status < 400) return true;
    if (response.status === 0 && response.body && response.body.length > 0) {
        if (sentinel === null) return true;
        return response.body.includes(sentinel);
    }
    return false;
}

// Stricter thresholds suitable for the gateway-bug environment: rely on
// `checks` (which use ok()) rather than http_req_failed (which counts
// transport-level RSTs even when the body was fully delivered).
//
// Once the gateway "Connection: close + RST" bug is fixed, scenarios can
// migrate back to http_req_failed-based thresholds.
export const tolerantThresholds = {
    // application-level checks must pass; that's our real success signal
    checks: ['rate>0.95'],
    // duration thresholds stay strict
    http_req_duration: ['p(95)<2000', 'p(99)<5000'],
};
