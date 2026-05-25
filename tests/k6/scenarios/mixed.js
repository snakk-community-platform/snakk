// Mixed-workload scenario — the most realistic shape. Uses k6 scenarios
// to run multiple traffic profiles concurrently, weighted to look like a
// modest community platform:
//
//   - browsers:  ~70 % of VUs, anonymous reads
//   - searchers: ~15 % of VUs, search queries
//   - writers:   ~10 % of VUs, authenticated post creation
//   - readers:   ~5  % of VUs, authenticated reads (notifications etc.)
//
// Run (defaults to a 5-min load with ~50 concurrent users total):
//   docker compose --profile loadtest run --rm k6 run \
//     -o experimental-opentelemetry /scripts/scenarios/mixed.js

import { sleep, check, group } from 'k6';
import { pickRandom, COMMUNITIES, DEFAULT_HUB_SPACES, SEARCH_TERMS, TEST_USERS } from '../lib/config.js';
import { checkOk } from '../lib/metrics.js';
import { login } from '../lib/auth.js';
import {
    getHomePage,
    getCommunityPage,
    getHubPage,
    getSpacePage,
    pickDiscussionLink,
    getDiscussionDetail,
    searchSpaces,
    searchDiscussions,
    searchPosts,
    getNotifications,
    getUnreadCount,
    loadNewDiscussionForm,
    extractFormTokens,
    createStandardDiscussion,
} from '../lib/endpoints.js';

const TOTAL_VUS = parseInt(__ENV.VUS || '50');
const DURATION  = __ENV.DURATION || '5m';

export const options = {
    scenarios: {
        browsers: {
            executor: 'ramping-vus',
            exec: 'browse',
            startVUs: 0,
            stages: [
                { duration: '20s', target: Math.round(TOTAL_VUS * 0.70) },
                { duration: DURATION, target: Math.round(TOTAL_VUS * 0.70) },
                { duration: '20s', target: 0 },
            ],
            gracefulRampDown: '15s',
        },
        searchers: {
            executor: 'ramping-vus',
            exec: 'search',
            startVUs: 0,
            stages: [
                { duration: '20s', target: Math.round(TOTAL_VUS * 0.15) },
                { duration: DURATION, target: Math.round(TOTAL_VUS * 0.15) },
                { duration: '20s', target: 0 },
            ],
            gracefulRampDown: '15s',
        },
        writers: {
            executor: 'ramping-vus',
            exec: 'write',
            startVUs: 0,
            stages: [
                { duration: '20s', target: Math.max(1, Math.round(TOTAL_VUS * 0.10)) },
                { duration: DURATION, target: Math.max(1, Math.round(TOTAL_VUS * 0.10)) },
                { duration: '20s', target: 0 },
            ],
            gracefulRampDown: '15s',
        },
        authReaders: {
            executor: 'ramping-vus',
            exec: 'authRead',
            startVUs: 0,
            stages: [
                { duration: '20s', target: Math.max(1, Math.round(TOTAL_VUS * 0.05)) },
                { duration: DURATION, target: Math.max(1, Math.round(TOTAL_VUS * 0.05)) },
                { duration: '20s', target: 0 },
            ],
            gracefulRampDown: '15s',
        },
    },
    thresholds: {
        checks: ['rate>0.90'],   // mixed workload has wider variance; 90% is realistic
        'http_req_duration{name:home}':              ['p(95)<1500'],
        'http_req_duration{name:discussion-detail}': ['p(95)<2000'],
        'http_req_duration{name:search-discussions}':['p(95)<1000'],
        'http_req_duration{name:create-discussion}': ['p(95)<3500'],
    },
};

// --- per-scenario VU functions ---

export function browse() {
    checkOk(getHomePage(), 'home', '<!DOCTYPE');
    sleep(Math.random() * 1.5);

    const community = pickRandom(COMMUNITIES);
    checkOk(getCommunityPage(community), 'community', '<!DOCTYPE');
    sleep(Math.random() * 1.5);

    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    checkOk(getHubPage(hub), 'hub', '<!DOCTYPE');
    sleep(Math.random() * 1.5);

    const spacePage = getSpacePage(hub, space);
    checkOk(spacePage, 'space', '<!DOCTYPE');

    const link = pickDiscussionLink(spacePage.body);
    if (link) {
        sleep(Math.random() * 2);
        checkOk(getDiscussionDetail(link), 'discussion-detail', '<!DOCTYPE');
    }
    sleep(Math.random() * 3 + 1);
}

export function search() {
    const term = pickRandom(SEARCH_TERMS);
    checkOk(searchSpaces(term),      'search-spaces');
    sleep(Math.random() * 0.5);
    checkOk(searchDiscussions(term), 'search-discussions');
    sleep(Math.random() * 0.5);
    checkOk(searchPosts(term),       'search-posts');
    sleep(Math.random() * 1.5 + 0.5);
}

export function write() {
    const user = pickRandom(TEST_USERS);
    if (!login(user)) return;

    const form = loadNewDiscussionForm();
    if (form.status !== 200) return;

    const { token, spaceId } = extractFormTokens(form.body);
    if (!token || !spaceId) return;

    createStandardDiscussion(
        `k6 mixed ${user.name} ${Date.now()}`,
        'Created by k6 mixed-workload scenario.',
        spaceId, token,
    );
    sleep(Math.random() * 5 + 3);
}

export function authRead() {
    const user = pickRandom(TEST_USERS);
    if (!login(user)) return;

    getUnreadCount();
    sleep(Math.random() * 1);
    getNotifications();
    sleep(Math.random() * 4 + 2);
}
