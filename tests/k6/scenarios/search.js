// Search-heavy scenario. Hammers the three BFF search endpoints with terms
// that exist in seeded data so results aren't empty (which would be a faster
// path than realistic queries).
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     -e VUS=30 -e DURATION=3m \
//     -o experimental-opentelemetry /scripts/scenarios/search.js

import { sleep } from 'k6';
import { pickRandom, SEARCH_TERMS, tolerantThresholds } from '../lib/config.js';
import { checkOk } from '../lib/metrics.js';
import { searchSpaces, searchDiscussions, searchPosts } from '../lib/endpoints.js';

export const options = {
    stages: [
        { duration: '20s', target: parseInt(__ENV.VUS || '30') },
        { duration: __ENV.DURATION || '3m', target: parseInt(__ENV.VUS || '30') },
        { duration: '20s', target: 0 },
    ],
    thresholds: {
        ...tolerantThresholds,
        'http_req_duration{name:search-discussions}': ['p(95)<800'],
        'http_req_duration{name:search-posts}':       ['p(95)<800'],
        'http_req_duration{name:search-spaces}':      ['p(95)<400'],
    },
};

export default function () {
    const term = pickRandom(SEARCH_TERMS);
    checkOk(searchSpaces(term),      'search-spaces');
    checkOk(searchDiscussions(term), 'search-discussions');
    checkOk(searchPosts(term),       'search-posts');
    sleep(Math.random() * 1.5 + 0.5);
}
