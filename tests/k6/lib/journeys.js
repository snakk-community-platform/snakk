// Reusable user journeys. Each function strings together a realistic
// sequence of actions one user would do in a single visit. Scenarios
// compose these instead of one-shot endpoint hits so each VU exercises
// the same caches, sessions, and code paths a real user would.
//
// Every journey is self-contained: it doesn't assume previous state and
// it cleans up nothing — the journey IS the state.

import { sleep } from 'k6';
import { pickRandom, COMMUNITIES, DEFAULT_HUB_SPACES, SEARCH_TERMS, TEST_USERS, LISTING_PATHS } from './config.js';
import { checkOk } from './metrics.js';
import { login } from './auth.js';
import {
    getHomePage,
    getCommunityPage,
    getHubPage,
    getSpacePage,
    pickDiscussionLink,
    getDiscussionDetail,
    getListingPage,
    getActivitySparkline,
    getTrendingSpacesPartial,
    getTrendingContributorsPartial,
    getPlatformStatsPartial,
    getNotifications,
    getUnreadCount,
    searchSpaces,
    searchDiscussions,
    searchPosts,
    loadNewDiscussionForm,
    extractFormTokens,
    createStandardDiscussion,
} from './endpoints.js';

// Sleep with jitter to model think time. Mean ~base ± 50%.
function think(base) {
    sleep(base * (0.5 + Math.random()));
}

// ──────────────────────────────────────────────────────────────────────
// JOURNEY 1 — Anonymous reader.
// Lands on home, looks at homepage partials, drills into a community,
// reads 2-3 discussions, optionally checks /trending.
// Single biggest workload in any realistic mix.
// ──────────────────────────────────────────────────────────────────────
export function readerJourney() {
    checkOk(getHomePage(), 'home', '<!DOCTYPE');
    // homepage partials fire roughly in parallel in a browser; serialise
    // them here for simplicity but no think time between
    getActivitySparkline();
    getPlatformStatsPartial();
    getTrendingSpacesPartial();
    getTrendingContributorsPartial();
    think(2);

    const community = pickRandom(COMMUNITIES);
    checkOk(getCommunityPage(community), 'community', '<!DOCTYPE');
    think(1.5);

    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    checkOk(getHubPage(hub), 'hub', '<!DOCTYPE');
    think(1.5);

    const spacePage = getSpacePage(hub, space);
    checkOk(spacePage, 'space', '<!DOCTYPE');

    // Read 2-3 discussions
    const reads = 2 + Math.floor(Math.random() * 2);
    for (let i = 0; i < reads; i++) {
        const link = pickDiscussionLink(spacePage.body);
        if (link) {
            think(2);
            checkOk(getDiscussionDetail(link), 'discussion-detail', '<!DOCTYPE');
        }
    }

    // 40% chance the reader checks /trending or /latest before leaving
    if (Math.random() < 0.4) {
        think(1);
        checkOk(getListingPage(pickRandom(LISTING_PATHS)), 'listing', '<!DOCTYPE');
    }

    think(3);
}

// ──────────────────────────────────────────────────────────────────────
// JOURNEY 2 — Searcher. Repeated, varied searches.
// Stresses the search index, not the page renderer.
// ──────────────────────────────────────────────────────────────────────
export function searcherJourney() {
    const searches = 3 + Math.floor(Math.random() * 3);
    for (let i = 0; i < searches; i++) {
        const term = pickRandom(SEARCH_TERMS);
        // Real users typically try one search type, see results,
        // then maybe refine. Pick a primary type per iteration.
        const primary = Math.random();
        if (primary < 0.6) {
            checkOk(searchDiscussions(term), 'search-discussions');
        } else if (primary < 0.85) {
            checkOk(searchSpaces(term),      'search-spaces');
        } else {
            checkOk(searchPosts(term),       'search-posts');
        }
        think(1.5);
    }
}

// ──────────────────────────────────────────────────────────────────────
// JOURNEY 3 — Authenticated reader. Logs in once, then poll-and-read
// pattern: read notifications + sparkline + browse a few discussions.
// Models a returning user keeping their feed warm.
// ──────────────────────────────────────────────────────────────────────
export function authReaderJourney() {
    const user = pickRandom(TEST_USERS);
    if (!login(user)) return;

    checkOk(getUnreadCount(),   'unread-count');
    checkOk(getNotifications(), 'notifications');
    think(1);

    checkOk(getHomePage(), 'home', '<!DOCTYPE');
    think(2);

    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    const spacePage = getSpacePage(hub, space);
    checkOk(spacePage, 'space', '<!DOCTYPE');

    const link = pickDiscussionLink(spacePage.body);
    if (link) {
        think(2);
        checkOk(getDiscussionDetail(link), 'discussion-detail', '<!DOCTYPE');
    }

    think(3);
}

// ──────────────────────────────────────────────────────────────────────
// JOURNEY 4 — Writer. Logs in, reads, then creates a discussion.
// Exercises the full write path: antiforgery → gRPC → DB insert.
// ──────────────────────────────────────────────────────────────────────
const SAMPLE_BODIES = [
    'Generated by k6 load test — feel free to delete.',
    'Hello world. Testing post creation under load.',
    'A longer body. Markdown is not rendered here, just stored. The content service should handle this size without trouble. Adding a few more characters to reach a couple hundred bytes which is more representative of real posts.',
    'Quick test.',
    'Multi-paragraph body.\n\nSecond paragraph with some more text to vary the size distribution. This helps detect issues that only show up with larger payloads.',
];

export function writerJourney() {
    const user = pickRandom(TEST_USERS);
    if (!login(user)) return;

    // Browse a bit first — writers don't post in a vacuum
    checkOk(getHomePage(), 'home', '<!DOCTYPE');
    think(1.5);

    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    const spacePage = getSpacePage(hub, space);
    checkOk(spacePage, 'space', '<!DOCTYPE');
    think(2);

    // Load form
    const form = loadNewDiscussionForm();
    if (!checkOk(form, 'new-discussion-form', '<!DOCTYPE')) return;

    const { token, spaceId } = extractFormTokens(form.body);
    if (!token || !spaceId) return;

    think(5);   // user composes their post — 5 s of "typing"

    const created = createStandardDiscussion(
        `k6 journey ${user.name} ${Date.now()}`,
        pickRandom(SAMPLE_BODIES),
        spaceId, token,
    );
    checkOk(created, 'create-discussion');

    think(2);
}

// ──────────────────────────────────────────────────────────────────────
// JOURNEY 5 — Power user. Long session combining browse + auth-read +
// search + occasional write. Models a heavy active user.
// ──────────────────────────────────────────────────────────────────────
export function powerUserJourney() {
    const user = pickRandom(TEST_USERS);
    if (!login(user)) return;

    // Open the platform
    checkOk(getUnreadCount(),   'unread-count');
    checkOk(getHomePage(),      'home', '<!DOCTYPE');
    getActivitySparkline();
    getPlatformStatsPartial();
    think(2);

    // Drill into something
    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    checkOk(getHubPage(hub),                          'hub', '<!DOCTYPE');
    const spacePage = getSpacePage(hub, space);
    checkOk(spacePage,                                'space', '<!DOCTYPE');

    // Read 3 discussions
    for (let i = 0; i < 3; i++) {
        const link = pickDiscussionLink(spacePage.body);
        if (link) {
            think(2 + Math.random() * 3);
            checkOk(getDiscussionDetail(link), 'discussion-detail', '<!DOCTYPE');
        }
    }

    // Search for something
    think(1);
    checkOk(searchDiscussions(pickRandom(SEARCH_TERMS)), 'search-discussions');

    // 20 % chance to create a discussion at the end of the session
    if (Math.random() < 0.2) {
        const form = loadNewDiscussionForm();
        if (checkOk(form, 'new-discussion-form', '<!DOCTYPE')) {
            const { token, spaceId } = extractFormTokens(form.body);
            if (token && spaceId) {
                think(8);
                checkOk(
                    createStandardDiscussion(
                        `k6 power ${user.name} ${Date.now()}`,
                        pickRandom(SAMPLE_BODIES),
                        spaceId, token,
                    ),
                    'create-discussion',
                );
            }
        }
    }

    think(3);
}

// Weighted picker — call this from a scenario default function.
// Distribution approximates a real community platform:
//   60% anonymous readers, 15% searchers, 12% auth readers,
//   8% power users, 5% writers.
export function pickAndRunJourney() {
    const r = Math.random();
    if      (r < 0.60) readerJourney();
    else if (r < 0.75) searcherJourney();
    else if (r < 0.87) authReaderJourney();
    else if (r < 0.95) powerUserJourney();
    else               writerJourney();
}
