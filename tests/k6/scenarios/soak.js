// Soak scenario. Sustained moderate load over a long window. Designed to
// surface memory leaks (watch in-use heap on Snakk Profiles), connection
// pool exhaustion, and slow GC pauses. Pairs naturally with the Pyroscope
// flame-graph dashboards — leave it running while watching memory:inuse_space.
//
// Run (default: 30 VUs, 1 hour):
//   docker compose --profile loadtest run --rm k6 run \
//     -e VUS=30 -e DURATION=1h \
//     -o experimental-opentelemetry /scripts/scenarios/soak.js

import { sleep } from 'k6';
import { pickRandom, COMMUNITIES, DEFAULT_HUB_SPACES, SEARCH_TERMS, tolerantThresholds } from '../lib/config.js';
import { checkOk } from '../lib/metrics.js';
import {
    getHomePage, getCommunityPage, getSpacePage,
    pickDiscussionLink, getDiscussionDetail,
    searchDiscussions, getActivitySparkline,
} from '../lib/endpoints.js';

export const options = {
    stages: [
        { duration: '2m',  target: parseInt(__ENV.VUS || '30') },
        { duration: __ENV.DURATION || '1h', target: parseInt(__ENV.VUS || '30') },
        { duration: '1m',  target: 0 },
    ],
    thresholds: {
        ...tolerantThresholds,
        // soak runs long — keep latency thresholds firm
        http_req_duration: ['p(95)<2000', 'p(99)<4000'],
    },
};

export default function () {
    checkOk(getHomePage(), 'home', '<!DOCTYPE');
    getActivitySparkline();
    sleep(Math.random() * 2 + 1);

    const community = pickRandom(COMMUNITIES);
    checkOk(getCommunityPage(community), 'community', '<!DOCTYPE');
    sleep(Math.random() * 2);

    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);
    const spacePage = getSpacePage(hub, space);
    const link = pickDiscussionLink(spacePage.body);
    if (link) {
        sleep(Math.random() * 2 + 1);
        getDiscussionDetail(link);
    }

    sleep(Math.random() * 3);

    // Mix in a search every few iterations to keep the search index warm.
    if (Math.random() < 0.3) {
        searchDiscussions(pickRandom(SEARCH_TERMS));
    }

    sleep(Math.random() * 4 + 2);
}
