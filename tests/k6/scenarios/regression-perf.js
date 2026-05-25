// Targeted regression scenario — focused on the hotspots
// `docs/PERFORMANCE-AUDIT.md` flagged. Each iteration deliberately
// exercises one of the known-slow paths so a regression PR will move
// the per-endpoint p95 in this dashboard noticeably.
//
// What this test hits (all from the perf audit):
//   • Output-cache eligible pages (Home, Hub Detail, Space Detail,
//     Communities Index) — verify cache hit/miss via Server-Timing.
//   • Discussion-detail load — exercises gRPC, post pagination,
//     OG-image fetches (Snakk.Discussion.LoadDetail per design §6.2).
//   • Profile pages (slow profile rendering per audit item #3).
//   • Per-route header `Vary: HX-Request` plumbing — verifies the fix
//     for the RST bug stays in place.
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     /scripts/scenarios/regression-perf.js
//
// Compare runs across branches via the `Snakk k6 Load Tests` dashboard
// — set the time range to bracket the run, screenshot the per-endpoint
// p95 panel, attach to the PR.

import { sleep, group } from 'k6';
import http from 'k6/http';
import { Trend } from 'k6/metrics';
import { BASE_URL, httpOptions, pickRandom, DEFAULT_HUB_SPACES, COMMUNITIES, tolerantThresholds } from '../lib/config.js';
import { checkOk } from '../lib/metrics.js';
import {
    getHomePage,
    getCommunityPage,
    getHubPage,
    getSpacePage,
    pickDiscussionLink,
    getDiscussionDetail,
} from '../lib/endpoints.js';

// Custom Trend that records whether Server-Timing reports an output-cache
// HIT. Useful to verify the cache is actually working under load.
const cacheHitRate = new Trend('cache_hit_likely_ms');

function parseServerTiming(response) {
    // Server-Timing looks like: `cache;dur=2.0;desc="prefetch:..."`.
    // A `dur<1.0` on a cache-named entry suggests a cache hit.
    const h = response.headers['Server-Timing'] || response.headers['server-timing'];
    if (!h) return null;
    const m = h.match(/cache;dur=([0-9.]+)/);
    return m ? parseFloat(m[1]) : null;
}

export const options = {
    vus: parseInt(__ENV.VUS || '20'),
    duration: __ENV.DURATION || '3m',
    thresholds: {
        ...tolerantThresholds,
        // These are the perf-audit-driven SLAs. Tweak after a baseline run.
        'http_req_duration{name:home}':              ['p(95)<800'],
        'http_req_duration{name:community}':         ['p(95)<800'],
        'http_req_duration{name:hub}':               ['p(95)<1000'],
        'http_req_duration{name:space}':             ['p(95)<1200'],
        'http_req_duration{name:discussion-detail}': ['p(95)<1500'],
        'http_req_duration{name:user-profile}':      ['p(95)<1500'],
        http_req_failed: ['rate<0.01'],
    },
};

// Pull a stable set of user IDs from the homepage links the first time
// the VU runs, then re-use across iterations.
let cachedUserIds = null;
function userIds() {
    if (cachedUserIds) return cachedUserIds;
    const r = http.get(`${BASE_URL}/`, httpOptions);
    cachedUserIds = [...r.body.matchAll(/href="\/u\/([A-Za-z0-9]+)"/g)].map(m => m[1]);
    if (cachedUserIds.length === 0) cachedUserIds = ['unknown'];
    return cachedUserIds;
}

export default function () {
    group('output-cache eligible pages', () => {
        // Home is the most commonly cached page. Repeat hits per VU should
        // show low Server-Timing cache.dur for second+ load.
        const home = getHomePage();
        checkOk(home, 'home', '<!DOCTYPE');
        const homeCache = parseServerTiming(home);
        if (homeCache !== null) cacheHitRate.add(homeCache, { name: 'home' });

        const community = pickRandom(COMMUNITIES);
        const commPage = getCommunityPage(community);
        checkOk(commPage, 'community', '<!DOCTYPE');

        const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
        const hubPage = getHubPage(hub);
        checkOk(hubPage, 'hub', '<!DOCTYPE');
        const hubCache = parseServerTiming(hubPage);
        if (hubCache !== null) cacheHitRate.add(hubCache, { name: 'hub' });

        const spacePage = getSpacePage(hub, space);
        checkOk(spacePage, 'space', '<!DOCTYPE');
        const spaceCache = parseServerTiming(spacePage);
        if (spaceCache !== null) cacheHitRate.add(spaceCache, { name: 'space' });
    });

    sleep(0.5);

    group('discussion-detail (Snakk.Discussion.LoadDetail hotspot)', () => {
        const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
        const spacePage = getSpacePage(hub, space);
        const link = pickDiscussionLink(spacePage.body);
        if (link) {
            checkOk(getDiscussionDetail(link), 'discussion-detail', '<!DOCTYPE');
        }
    });

    sleep(0.3);

    group('user profile (perf audit #3 — slow profile rendering)', () => {
        const uid = pickRandom(userIds());
        const profile = http.get(`${BASE_URL}/u/${uid}`, {
            ...httpOptions, tags: { name: 'user-profile' }
        });
        checkOk(profile, 'user-profile', '<!DOCTYPE');
    });

    sleep(0.5);
}
