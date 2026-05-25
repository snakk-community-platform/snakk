// Endpoint catalogue + helpers that emit URLs against the Snakk gateway.
// Concentrating path knowledge here means scenarios stay declarative.

import http from 'k6/http';
import { BASE_URL, httpOptions } from './config.js';

// --- Public pages (anonymous) ---

export function getHomePage() {
    return http.get(`${BASE_URL}/`, { ...httpOptions, tags: { name: 'home' } });
}

// Community / hub / space pages. The default community uses bare `/h/` paths
// (the gateway 301-strips `/c/main/`); other communities keep `/c/{slug}/`.
export function getCommunityPage(slug) {
    const path = slug === 'main' ? '/c' : `/c/${slug}`;
    return http.get(`${BASE_URL}${path}`, { ...httpOptions, tags: { name: 'community' } });
}

export function getHubPage(hub, community = 'main') {
    const path = community === 'main' ? `/h/${hub}` : `/c/${community}/h/${hub}`;
    return http.get(`${BASE_URL}${path}`, { ...httpOptions, tags: { name: 'hub' } });
}

export function getSpacePage(hub, space, community = 'main') {
    const path = community === 'main'
        ? `/h/${hub}/${space}`
        : `/c/${community}/h/${hub}/${space}`;
    return http.get(`${BASE_URL}${path}`, { ...httpOptions, tags: { name: 'space' } });
}

export function getListingPage(path) {
    return http.get(`${BASE_URL}${path}`, { ...httpOptions, tags: { name: `listing-${path}` } });
}

// Discussion-detail pages have slugs ending in `~<id>` and live under the
// hub path. Match both `/h/hub/space/slug~ID` (default community) and the
// explicit `/c/community/h/hub/space/slug~ID` form.
const DETAIL_SLUG_RE = /href="((?:\/c\/[^"\/?#]+)?\/h\/[^"\/?#]+\/[^"\/?#]+\/[^"\/?#]+~[A-Za-z0-9]+)"/g;

export function pickDiscussionLink(spacePageBody) {
    const matches = [...spacePageBody.matchAll(DETAIL_SLUG_RE)];
    if (matches.length === 0) return null;
    return matches[Math.floor(Math.random() * matches.length)][1];
}

export function getDiscussionDetail(path) {
    return http.get(`${BASE_URL}${path}`, { ...httpOptions, tags: { name: 'discussion-detail' } });
}

// --- BFF JSON endpoints (used by the SPA-ish parts of the site) ---

export function getActivitySparkline() {
    // Endpoint requires entityType (platform/community/hub/space/discussion) and days.
    // Use platform-global / 30 days to mimic the homepage sparkline.
    return http.get(`${BASE_URL}/bff/activity/sparkline?entityType=platform&days=30`, {
        ...httpOptions, tags: { name: 'bff-sparkline' }
    });
}

export function getNotifications() {
    // Endpoint requires offset + pageSize query params.
    return http.get(`${BASE_URL}/bff/notifications?offset=0&pageSize=20`, {
        ...httpOptions, tags: { name: 'bff-notifications' }
    });
}

export function getUnreadCount() {
    return http.get(`${BASE_URL}/bff/notifications/unread-count`, {
        ...httpOptions, tags: { name: 'bff-unread-count' }
    });
}

// --- Search (BFF) ---

export function searchSpaces(q) {
    return http.get(`${BASE_URL}/bff/search/spaces?q=${encodeURIComponent(q)}`, {
        ...httpOptions, tags: { name: 'search-spaces' }
    });
}

export function searchDiscussions(q) {
    return http.get(`${BASE_URL}/bff/search/discussions?q=${encodeURIComponent(q)}`, {
        ...httpOptions, tags: { name: 'search-discussions' }
    });
}

export function searchPosts(q) {
    return http.get(`${BASE_URL}/bff/search/posts?q=${encodeURIComponent(q)}`, {
        ...httpOptions, tags: { name: 'search-posts' }
    });
}

// --- Htmx-style partials (driven by the discussion-detail page) ---

export function getTrendingSpacesPartial() {
    return http.get(`${BASE_URL}/partials/trending-spaces`, {
        ...httpOptions, tags: { name: 'partial-trending-spaces' }
    });
}

export function getTrendingContributorsPartial() {
    return http.get(`${BASE_URL}/partials/trending-contributors`, {
        ...httpOptions, tags: { name: 'partial-trending-contributors' }
    });
}

export function getPlatformStatsPartial() {
    return http.get(`${BASE_URL}/partials/platform-stats`, {
        ...httpOptions, tags: { name: 'partial-platform-stats' }
    });
}

// --- Authenticated writes ---

// Extract the antiforgery token + hidden SpaceId from a `/new/standard` page
// load. Both are needed to POST a valid form.
const TOKEN_RE = /name="__RequestVerificationToken"[^>]*value="([^"]+)"/;
const SPACE_ID_RE = /name="SpaceId"[^>]*value="([^"]+)"/;

export function loadNewDiscussionForm(spaceId) {
    const url = spaceId
        ? `${BASE_URL}/new/standard?spaceId=${encodeURIComponent(spaceId)}`
        : `${BASE_URL}/new/standard`;
    return http.get(url, { ...httpOptions, tags: { name: 'new-discussion-form' } });
}

export function extractFormTokens(body) {
    const t = TOKEN_RE.exec(body);
    const s = SPACE_ID_RE.exec(body);
    return {
        token: t ? t[1] : null,
        spaceId: s ? s[1] : null,
    };
}

export function createStandardDiscussion(title, content, spaceId, token) {
    return http.post(
        `${BASE_URL}/new/standard`,
        {
            'NewTitle': title,
            'NewContent': content,
            'SpaceId': spaceId,
            'IsAdult': 'false',
            '__RequestVerificationToken': token,
        },
        { ...httpOptions, redirects: 0, tags: { name: 'create-discussion' } }
    );
}
