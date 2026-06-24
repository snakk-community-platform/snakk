// Anonymous browse scenario. Simulates an unauthenticated reader pattern:
// land on home, drill into a community → hub → space → discussion, and pull
// the htmx-style partials each page expects.
//
// Run:
//   docker compose --profile loadtest run --rm k6 run \
//     -e VUS=50 -e DURATION=5m \
//     -o experimental-opentelemetry /scripts/scenarios/browse.js

import { sleep, group, check } from 'k6';
import { pickRandom, COMMUNITIES, DEFAULT_HUB_SPACES, ok, tolerantThresholds } from '../lib/config.js';
import { checkOk } from '../lib/metrics.js';
import {
    getHomePage,
    getCommunityPage,
    getHubPage,
    getSpacePage,
    pickDiscussionLink,
    getDiscussionDetail,
    getActivitySparkline,
    getTrendingSpacesPartial,
    getTrendingContributorsPartial,
    getPlatformStatsPartial,
} from '../lib/endpoints.js';

export const options = {
    stages: [
        { duration: '30s', target: parseInt(__ENV.VUS || '50') }, // ramp up
        { duration: __ENV.DURATION || '5m', target: parseInt(__ENV.VUS || '50') },
        { duration: '30s', target: 0 },                            // ramp down
    ],
    thresholds: {
        ...tolerantThresholds,
        // tighten the page-load expectations for browse-specific endpoints
        'http_req_duration{name:discussion-detail}': ['p(95)<2000'],
    },
};

export default function () {
    group('landing', () => {
        checkOk(getHomePage(), 'home', '<!DOCTYPE');
        getActivitySparkline();
        getPlatformStatsPartial();
        getTrendingSpacesPartial();
        getTrendingContributorsPartial();
    });

    sleep(Math.random() * 2 + 1); // 1-3s think time

    const community = pickRandom(COMMUNITIES);
    const { hub, space } = pickRandom(DEFAULT_HUB_SPACES);

    group('drill-down', () => {
        checkOk(getCommunityPage(community), 'community', '<!DOCTYPE');
        sleep(Math.random() * 2);

        checkOk(getHubPage(hub), 'hub', '<!DOCTYPE');
        sleep(Math.random() * 2);

        const spacePage = getSpacePage(hub, space);
        checkOk(spacePage, 'space', '<!DOCTYPE');

        const link = pickDiscussionLink(spacePage.body);
        if (link) {
            sleep(Math.random() * 2 + 1);
            checkOk(getDiscussionDetail(link), 'discussion-detail', '<!DOCTYPE');
        }
    });

    sleep(Math.random() * 3 + 1);
}
